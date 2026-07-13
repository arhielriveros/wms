[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("blue", "green")]
    [string]$TargetSlot,
    [Parameter(Mandatory)]
    [ValidatePattern("^[A-Za-z0-9._-]+$")]
    [string]$ImageTag,
    [string]$EnvFile = ".env",
    [switch]$KeepPrevious
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$envPath = if ([IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $root $EnvFile }
$overlay = Join-Path $root "infra/deployment/docker-compose.blue-green.yml"
if (-not (Test-Path -LiteralPath $envPath)) { throw "Environment file not found: $envPath" }

$env:WMS_IMAGE_TAG = $ImageTag
$apiService = "api-$TargetSlot"
$workerService = "worker-$TargetSlot"
$port = if ($TargetSlot -eq "blue") { 18080 } else { 18081 }
$healthUri = "http://127.0.0.1:$port/health/ready"
$runtimeDir = Join-Path $root ".runtime"
$activeFile = Join-Path $runtimeDir "active-slot"
$previous = if (Test-Path -LiteralPath $activeFile) { (Get-Content -Raw $activeFile).Trim() } else { "" }

Push-Location $root
try {
    & docker compose --env-file $envPath -f docker-compose.yml -f $overlay --profile $TargetSlot up -d $apiService $workerService
    if ($LASTEXITCODE -ne 0) { throw "Could not start target slot." }
    $healthy = $false
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $healthUri -TimeoutSec 3 -UseBasicParsing
            if ($response.StatusCode -eq 200) { $healthy = $true; break }
        }
        catch { Start-Sleep -Seconds 2 }
    }
    if (-not $healthy) {
        & docker compose --env-file $envPath -f docker-compose.yml -f $overlay --profile $TargetSlot stop $apiService $workerService
        throw "Target slot failed readiness; previous slot was preserved."
    }
    [IO.Directory]::CreateDirectory($runtimeDir) | Out-Null
    Set-Content -LiteralPath $activeFile -Value $TargetSlot -Encoding ascii
    Write-Output "Target $TargetSlot is healthy. Switch the load balancer to port $port."
    if ($previous -and $previous -ne $TargetSlot -and -not $KeepPrevious) {
        & docker compose --env-file $envPath -f docker-compose.yml -f $overlay --profile $previous stop "api-$previous" "worker-$previous"
    }
}
finally {
    Pop-Location
    Remove-Item Env:WMS_IMAGE_TAG -ErrorAction SilentlyContinue
}
