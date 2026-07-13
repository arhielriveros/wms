[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("blue", "green")]
    [string]$TargetSlot,
    [Parameter(Mandatory)]
    [ValidatePattern("^[A-Za-z0-9._-]+$")]
    [string]$ImageTag,
    [string]$EnvFile = ".env",
    [string]$BaseComposeFile = "docker-compose.yml",
    [string]$OverlayFile = "infra/deployment/docker-compose.blue-green.yml",
    [string]$ProjectName = "wms",
    [string]$ApiImage = "",
    [string]$WorkerImage = "",
    [int]$BluePort = 18080,
    [int]$GreenPort = 18081,
    [int]$RouterPort = 18082,
    [int]$TimeoutSeconds = 180,
    [string[]]$PrerequisiteServices = @("postgres", "rabbitmq", "redis", "keycloak", "minio", "mock-erp", "otel-collector"),
    [switch]$BuildLocalImages,
    [switch]$KeepPrevious
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FromRoot([string]$Path, [string]$Root) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $Root $Path
}

function Invoke-Compose([string[]]$Arguments) {
    & docker compose @script:composeArguments @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed: $($Arguments -join ' ')" }
}

function Get-ComposeContainer([string]$Service) {
    $value = & docker compose @script:composeArguments ps --quiet $Service
    if ($LASTEXITCODE -ne 0) { throw "Could not resolve container for $Service." }
    return ($value | Out-String).Trim()
}

function Wait-Endpoint([string]$Uri, [string]$ExpectedSlot, [int]$Timeout) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($Timeout)
    do {
        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 3
            $slot = [string]$response.Headers["X-WMS-Deployment-Slot"]
            if ($response.StatusCode -eq 200 -and (-not $ExpectedSlot -or $slot -eq $ExpectedSlot)) { return $response }
        }
        catch { }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Endpoint $Uri did not become healthy for slot '$ExpectedSlot' within $Timeout seconds."
}

function Write-RouterConfig([string]$Slot, [string]$Path) {
    $content = @"
server {
    listen 8080;
    server_name _;
    location / {
        proxy_pass http://api-$Slot`:8080;
        proxy_http_version 1.1;
        proxy_set_header Host `$host;
        proxy_set_header X-Forwarded-For `$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto `$scheme;
        proxy_connect_timeout 2s;
        proxy_read_timeout 30s;
        proxy_next_upstream off;
        add_header X-WMS-Deployment-Slot $Slot always;
    }
}
"@
    [IO.File]::WriteAllText($Path, $content, [Text.UTF8Encoding]::new($false))
}

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$envPath = Resolve-FromRoot $EnvFile $root
$baseComposePath = Resolve-FromRoot $BaseComposeFile $root
$overlayPath = Resolve-FromRoot $OverlayFile $root
foreach ($requiredPath in @($envPath, $baseComposePath, $overlayPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Required deployment file not found: $requiredPath" }
}

$runtimeDir = Join-Path $root ".runtime/blue-green/$ProjectName"
[IO.Directory]::CreateDirectory($runtimeDir) | Out-Null
$activeFile = Join-Path $runtimeDir "active-slot"
$routerConfigPath = Join-Path $runtimeDir "nginx.conf"
$previous = if (Test-Path -LiteralPath $activeFile) { (Get-Content -Raw $activeFile).Trim() } else { "" }
$apiService = "api-$TargetSlot"
$workerService = "worker-$TargetSlot"
$directPort = if ($TargetSlot -eq "blue") { $BluePort } else { $GreenPort }
$directUri = "http://127.0.0.1:$directPort/health/ready"
$routerUri = "http://127.0.0.1:$RouterPort/health/ready"
$script:composeArguments = @("--env-file", $envPath, "--project-name", $ProjectName, "--file", $baseComposePath, "--file", $overlayPath)

$environmentValues = [ordered]@{
    WMS_IMAGE_TAG = $ImageTag
    WMS_BLUE_PORT = $BluePort.ToString()
    WMS_GREEN_PORT = $GreenPort.ToString()
    WMS_ROUTER_PORT = $RouterPort.ToString()
    WMS_ROUTER_CONFIG_PATH = $routerConfigPath
}
if ($ApiImage) { $environmentValues.WMS_API_IMAGE = $ApiImage }
if ($WorkerImage) { $environmentValues.WMS_WORKER_IMAGE = $WorkerImage }
$savedEnvironment = @{}
foreach ($entry in $environmentValues.GetEnumerator()) {
    $existing = [Environment]::GetEnvironmentVariable($entry.Key, "Process")
    $savedEnvironment[$entry.Key] = $existing
    [Environment]::SetEnvironmentVariable($entry.Key, [string]$entry.Value, "Process")
}

$targetStarted = $false
$routingChanged = $false
$routerWasRunning = $false
$switchWatch = [Diagnostics.Stopwatch]::new()

Push-Location $root
try {
    if ($BuildLocalImages) {
        if (-not $ApiImage -or -not $WorkerImage) { throw "ApiImage and WorkerImage are required with BuildLocalImages." }
        & docker build --file infra/docker/backend.Dockerfile --build-arg WMS_PROJECT=Wms.Api --tag "${ApiImage}:$ImageTag" .
        if ($LASTEXITCODE -ne 0) { throw "Could not build local API image." }
        & docker build --file infra/docker/backend.Dockerfile --build-arg WMS_PROJECT=Wms.Worker --tag "${WorkerImage}:$ImageTag" .
        if ($LASTEXITCODE -ne 0) { throw "Could not build local worker image." }
    }

    if ($PrerequisiteServices.Count -gt 0) {
        Invoke-Compose -Arguments (@("up", "--detach", "--wait", "--wait-timeout", $TimeoutSeconds.ToString()) + $PrerequisiteServices)
    }

    Invoke-Compose -Arguments @("--profile", $TargetSlot, "up", "--detach", "--wait", "--wait-timeout", $TimeoutSeconds.ToString(), $apiService, $workerService)
    $targetStarted = $true
    Wait-Endpoint -Uri $directUri -ExpectedSlot "" -Timeout $TimeoutSeconds | Out-Null

    $workerContainer = Get-ComposeContainer $workerService
    if (-not $workerContainer) { throw "Worker $workerService did not start." }
    $workerRunning = (& docker inspect --format "{{.State.Running}}" $workerContainer | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $workerRunning -ne "true") { throw "Worker $workerService is not running." }

    $routerContainer = Get-ComposeContainer "traffic-router"
    $routerWasRunning = [bool]$routerContainer
    if ($previous -and -not (Test-Path -LiteralPath $routerConfigPath)) { Write-RouterConfig -Slot $previous -Path $routerConfigPath }

    $switchWatch.Start()
    Write-RouterConfig -Slot $TargetSlot -Path $routerConfigPath
    $routingChanged = $true
    if ($routerWasRunning) {
        Invoke-Compose -Arguments @("--profile", $TargetSlot, "exec", "--no-TTY", "traffic-router", "nginx", "-t")
        Invoke-Compose -Arguments @("--profile", $TargetSlot, "exec", "--no-TTY", "traffic-router", "nginx", "-s", "reload")
    }
    else {
        Invoke-Compose -Arguments @("--profile", $TargetSlot, "up", "--detach", "--wait", "--wait-timeout", $TimeoutSeconds.ToString(), "traffic-router")
    }
    Wait-Endpoint -Uri $routerUri -ExpectedSlot $TargetSlot -Timeout $TimeoutSeconds | Out-Null
    $switchWatch.Stop()

    Set-Content -LiteralPath $activeFile -Value $TargetSlot -Encoding ascii
    if ($previous -and $previous -ne $TargetSlot -and -not $KeepPrevious) {
        Invoke-Compose -Arguments @("--profile", $previous, "stop", "api-$previous", "worker-$previous")
    }

    [pscustomobject]@{
        status = "PASS"
        previousSlot = $previous
        activeSlot = $TargetSlot
        imageTag = $ImageTag
        directReadiness = $directUri
        routerReadiness = $routerUri
        switchSeconds = [math]::Round($switchWatch.Elapsed.TotalSeconds, 3)
        previousPreserved = [bool]($previous -and $KeepPrevious)
        workerRunning = $true
    }
}
catch {
    if ($routingChanged) {
        if ($previous) {
            Write-RouterConfig -Slot $previous -Path $routerConfigPath
            try {
                Invoke-Compose -Arguments @("--profile", $previous, "exec", "--no-TTY", "traffic-router", "nginx", "-s", "reload")
                Wait-Endpoint -Uri $routerUri -ExpectedSlot $previous -Timeout ([Math]::Min($TimeoutSeconds, 30)) | Out-Null
                Set-Content -LiteralPath $activeFile -Value $previous -Encoding ascii
            }
            catch { Write-Warning "Automatic traffic rollback to $previous could not be verified: $($_.Exception.Message)" }
        }
        elseif ($routerWasRunning -or (Get-ComposeContainer "traffic-router")) {
            try { Invoke-Compose -Arguments @("--profile", $TargetSlot, "stop", "traffic-router") } catch { }
        }
    }
    if ($targetStarted -and $TargetSlot -ne $previous) {
        try { Invoke-Compose -Arguments @("--profile", $TargetSlot, "stop", $apiService, $workerService) } catch { }
    }
    throw
}
finally {
    Pop-Location
    foreach ($entry in $environmentValues.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $savedEnvironment[$entry.Key], "Process")
    }
}
