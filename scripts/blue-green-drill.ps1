[CmdletBinding()]
# TEST-OPS-0003: verified blue/green switch and rollback through a stable ingress.
param(
    [string]$EnvFile = ".env.example",
    [ValidatePattern("^[a-z0-9][a-z0-9_-]+$")]
    [string]$ProjectName = "wms-blue-green-drill",
    [int]$BluePort = 28080,
    [int]$GreenPort = 28081,
    [int]$RouterPort = 28082,
    [int]$TimeoutSeconds = 180,
    [int]$ProbeSeconds = 20,
    [string]$OutputDirectory = ".backups",
    [string]$ApiSourceImage = "",
    [string]$WorkerSourceImage = "",
    [switch]$KeepEnvironment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$envPath = if ([IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $root $EnvFile }
$baseCompose = Join-Path $root "infra/deployment/docker-compose.blue-green.drill.yml"
$overlayCompose = Join-Path $root "infra/deployment/docker-compose.blue-green.yml"
$deployScript = Join-Path $PSScriptRoot "blue-green.ps1"
$outputPath = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $root $OutputDirectory }
foreach ($requiredPath in @($envPath, $baseCompose, $overlayCompose, $deployScript)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Required drill file not found: $requiredPath" }
}
[IO.Directory]::CreateDirectory($outputPath) | Out-Null

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$tag = "drill-$stamp"
$apiImage = "wms-blue-green-api-$stamp"
$workerImage = "wms-blue-green-worker-$stamp"
$runtimeDir = Join-Path $root ".runtime/blue-green/$ProjectName"
$activeFile = Join-Path $runtimeDir "active-slot"
$routerUri = "http://127.0.0.1:$RouterPort/health/ready"
$manifest = Join-Path $outputPath "wms-blue-green-$stamp.json"
$probeJob = $null
$cleanupArguments = @("compose", "--env-file", $envPath, "--project-name", $ProjectName, "--file", $baseCompose)

$commonParameters = @{
    ImageTag = $tag
    EnvFile = $envPath
    BaseComposeFile = $baseCompose
    OverlayFile = $overlayCompose
    ProjectName = $ProjectName
    ApiImage = $apiImage
    WorkerImage = $workerImage
    BluePort = $BluePort
    GreenPort = $GreenPort
    RouterPort = $RouterPort
    TimeoutSeconds = $TimeoutSeconds
    PrerequisiteServices = @("postgres", "mock-erp")
    KeepPrevious = $true
}

Push-Location $root
try {
    $initialErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & docker @cleanupArguments down --volumes --remove-orphans 2>$null | Out-Null
    $initialCleanupExitCode = $LASTEXITCODE
    $ErrorActionPreference = $initialErrorAction
    if ($initialCleanupExitCode -ne 0) { throw "Could not clean the previous blue/green drill project." }
    if (Test-Path -LiteralPath $runtimeDir) { Remove-Item -LiteralPath $runtimeDir -Recurse -Force }

    if ([bool]$ApiSourceImage -xor [bool]$WorkerSourceImage) { throw "ApiSourceImage and WorkerSourceImage must be provided together." }
    if ($ApiSourceImage) {
        & docker image inspect $ApiSourceImage $WorkerSourceImage | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "One or more source images were not found." }
        & docker tag $ApiSourceImage "${apiImage}:$tag"
        if ($LASTEXITCODE -ne 0) { throw "Could not tag the source API image." }
        & docker tag $WorkerSourceImage "${workerImage}:$tag"
        if ($LASTEXITCODE -ne 0) { throw "Could not tag the source worker image." }
        $blueOutput = @(& $deployScript -TargetSlot blue @commonParameters)
    }
    else {
        $blueOutput = @(& $deployScript -TargetSlot blue -BuildLocalImages @commonParameters)
    }
    $blue = $blueOutput | Where-Object { $_ -is [psobject] -and $_.PSObject.Properties["activeSlot"] } | Select-Object -Last 1
    if (-not $blue -or $blue.status -ne "PASS" -or $blue.activeSlot -ne "blue") { throw "Initial blue deployment did not pass." }

    $probeJob = Start-Job -ScriptBlock {
        param($Uri, $DurationSeconds)
        Add-Type -AssemblyName System.Net.Http
        $client = [Net.Http.HttpClient]::new()
        $client.Timeout = [TimeSpan]::FromSeconds(2)
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $success = 0
        $failure = 0
        try {
            while ($watch.Elapsed.TotalSeconds -lt $DurationSeconds) {
                try {
                    $response = $client.GetAsync($Uri).GetAwaiter().GetResult()
                    if ($response.IsSuccessStatusCode) { $success++ } else { $failure++ }
                    $response.Dispose()
                }
                catch { $failure++ }
                Start-Sleep -Milliseconds 100
            }
        }
        finally { $client.Dispose() }
        [pscustomobject]@{ success = $success; failure = $failure; durationSeconds = [math]::Round($watch.Elapsed.TotalSeconds, 3) }
    } -ArgumentList $routerUri, $ProbeSeconds

    $greenOutput = @(& $deployScript -TargetSlot green @commonParameters)
    $green = $greenOutput | Where-Object { $_ -is [psobject] -and $_.PSObject.Properties["activeSlot"] } | Select-Object -Last 1
    if (-not $green -or $green.status -ne "PASS" -or $green.activeSlot -ne "green" -or -not $green.previousPreserved) { throw "Green switch did not preserve blue." }

    $rollbackOutput = @(& $deployScript -TargetSlot blue @commonParameters)
    $rollback = $rollbackOutput | Where-Object { $_ -is [psobject] -and $_.PSObject.Properties["activeSlot"] } | Select-Object -Last 1
    if (-not $rollback -or $rollback.status -ne "PASS" -or $rollback.activeSlot -ne "blue" -or -not $rollback.previousPreserved) { throw "Rollback to blue did not preserve green." }

    Wait-Job -Job $probeJob -Timeout ($ProbeSeconds + 30) | Out-Null
    if ($probeJob.State -ne "Completed") { throw "Continuity probe did not complete." }
    $probe = Receive-Job -Job $probeJob
    if (-not $probe -or $probe.failure -ne 0 -or $probe.success -lt 10) { throw "Traffic continuity failed: success=$($probe.success), failure=$($probe.failure)." }

    $response = Invoke-WebRequest -Uri $routerUri -UseBasicParsing -TimeoutSec 5
    $routedSlot = [string]$response.Headers["X-WMS-Deployment-Slot"]
    $activeSlot = if (Test-Path -LiteralPath $activeFile) { (Get-Content -Raw $activeFile).Trim() } else { "" }
    $blueWorker = (& docker ps --filter "label=com.docker.compose.project=$ProjectName" --filter "label=com.docker.compose.service=worker-blue" --format "{{.ID}}" | Out-String).Trim()
    $greenWorker = (& docker ps --filter "label=com.docker.compose.project=$ProjectName" --filter "label=com.docker.compose.service=worker-green" --format "{{.ID}}" | Out-String).Trim()
    if ($response.StatusCode -ne 200 -or $routedSlot -ne "blue" -or $activeSlot -ne "blue" -or -not $blueWorker -or -not $greenWorker) {
        throw "Final rollback validation failed: routed=$routedSlot active=$activeSlot blueWorker=$([bool]$blueWorker) greenWorker=$([bool]$greenWorker)."
    }

    $result = [ordered]@{
        status = "PASS"
        createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        testId = "TEST-OPS-0003"
        imageTag = $tag
        initialSlot = "blue"
        switchedSlot = "green"
        finalSlot = "blue"
        greenSwitchSeconds = $green.switchSeconds
        rollbackSeconds = $rollback.switchSeconds
        continuityRequests = [int]$probe.success
        continuityFailures = [int]$probe.failure
        blueWorkerRunning = [bool]$blueWorker
        greenWorkerRunning = [bool]$greenWorker
        stableIngress = $routerUri
    }
    $result | ConvertTo-Json | Set-Content -LiteralPath $manifest -Encoding UTF8
    [pscustomobject]$result
}
catch {
    & docker @cleanupArguments logs --no-color --tail 200 2>$null | Write-Warning
    throw
}
finally {
    if ($probeJob) {
        if ($probeJob.State -eq "Running") { Stop-Job -Job $probeJob }
        Remove-Job -Job $probeJob -Force -ErrorAction SilentlyContinue
    }
    if (-not $KeepEnvironment) {
        $cleanupErrorAction = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            & docker @cleanupArguments down --volumes --remove-orphans 2>$null | Out-Null
            & docker image rm --force "${apiImage}:$tag" "${workerImage}:$tag" 2>$null | Out-Null
            if (Test-Path -LiteralPath $runtimeDir) { Remove-Item -LiteralPath $runtimeDir -Recurse -Force }
        }
        finally { $ErrorActionPreference = $cleanupErrorAction }
    }
    Pop-Location
}
