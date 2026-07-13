[CmdletBinding()]
param(
    [string]$EnvFile = ".env",
    [switch]$Pull
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$composeFile = Join-Path $root "docker-compose.yml"
$envPath = if ([IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $root $EnvFile }

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw "Docker CLI is required." }
& docker compose version | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Docker Compose v2 is required." }

if (-not (Test-Path -LiteralPath $envPath)) {
    Copy-Item -LiteralPath (Join-Path $root ".env.example") -Destination $envPath -ErrorAction Stop
    Write-Warning "Created $envPath from .env.example. Replace placeholders before shared use."
}

Push-Location $root
try {
    & docker compose --env-file $envPath -f $composeFile config --quiet
    if ($LASTEXITCODE -ne 0) { throw "Compose configuration is invalid." }
    if ($Pull) {
        & docker compose --env-file $envPath -f $composeFile pull
        if ($LASTEXITCODE -ne 0) { throw "Image pull failed." }
    }
    & docker compose --env-file $envPath -f $composeFile up -d --wait
    if ($LASTEXITCODE -ne 0) { throw "Infrastructure did not become healthy." }
    & docker compose --env-file $envPath -f $composeFile ps
}
finally { Pop-Location }
