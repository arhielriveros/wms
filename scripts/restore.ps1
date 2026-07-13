[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$BackupFile,
    [string]$EnvFile = ".env",
    [ValidateSet("development", "staging")]
    [string]$EnvironmentName = "development",
    [switch]$ConfirmRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
if (-not $ConfirmRestore) { throw "Restore is destructive. Re-run with -ConfirmRestore after verifying target and backup." }

function Read-EnvFile([string]$Path) {
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith("#")) { continue }
        $pair = $trimmed.Split("=", 2)
        if ($pair.Count -eq 2) { $values[$pair[0]] = $pair[1] }
    }
    return $values
}

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$backupPath = (Resolve-Path -LiteralPath $BackupFile).Path
$envPath = if ([IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $root $EnvFile }
if (-not (Test-Path -LiteralPath $envPath)) { throw "Environment file not found: $envPath" }
$envMap = Read-EnvFile $envPath
foreach ($key in @("POSTGRES_USER", "POSTGRES_DB")) {
    if (-not $envMap.ContainsKey($key)) { throw "$key is required in $envPath" }
}

$manifestPath = "$backupPath.json"
if (Test-Path -LiteralPath $manifestPath) {
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $actual = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
    if ($actual -ne $manifest.sha256) { throw "Backup checksum mismatch." }
}

$remote = "/tmp/wms-restore-$([guid]::NewGuid().ToString('N')).dump"
Push-Location $root
try {
    $container = (& docker compose --env-file $envPath -f docker-compose.yml ps -q postgres).Trim()
    if (-not $container) { throw "PostgreSQL container is not running." }
    & docker compose --env-file $envPath -f docker-compose.yml stop keycloak
    try {
        & docker cp $backupPath "${container}:$remote"
        if ($LASTEXITCODE -ne 0) { throw "Could not copy backup into PostgreSQL container." }
        & docker exec $container pg_restore --username $envMap.POSTGRES_USER --dbname $envMap.POSTGRES_DB --clean --if-exists --no-owner --exit-on-error $remote
        if ($LASTEXITCODE -ne 0) { throw "pg_restore failed; inspect PostgreSQL logs." }
    }
    finally {
        & docker exec $container rm -f $remote 2>$null
        & docker compose --env-file $envPath -f docker-compose.yml start keycloak
    }
}
finally { Pop-Location }
Write-Output "Restore completed for $EnvironmentName from $backupPath"
