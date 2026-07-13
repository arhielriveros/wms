[CmdletBinding()]
param(
    [string]$EnvFile = ".env",
    [string]$OutputDirectory = ".backups"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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
$envPath = if ([IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $root $EnvFile }
if (-not (Test-Path -LiteralPath $envPath)) { throw "Environment file not found: $envPath" }
$envMap = Read-EnvFile $envPath
foreach ($key in @("POSTGRES_USER", "POSTGRES_DB")) {
    if (-not $envMap.ContainsKey($key)) { throw "$key is required in $envPath" }
}

$outputPath = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $root $OutputDirectory }
[IO.Directory]::CreateDirectory($outputPath) | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$file = Join-Path $outputPath "wms-$stamp.dump"
$remote = "/tmp/wms-$stamp.dump"

Push-Location $root
try {
    $container = (& docker compose --env-file $envPath -f docker-compose.yml ps -q postgres).Trim()
    if (-not $container) { throw "PostgreSQL container is not running." }
    try {
        & docker exec $container pg_dump --username $envMap.POSTGRES_USER --dbname $envMap.POSTGRES_DB --format custom --file $remote
        if ($LASTEXITCODE -ne 0) { throw "pg_dump failed." }
        & docker cp "${container}:$remote" $file
        if ($LASTEXITCODE -ne 0) { throw "docker cp failed." }
    }
    finally { & docker exec $container rm -f $remote 2>$null }
}
finally { Pop-Location }

$hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
@{
    file = [IO.Path]::GetFileName($file)
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    database = $envMap.POSTGRES_DB
    sha256 = $hash
} | ConvertTo-Json | Set-Content -LiteralPath "$file.json" -Encoding utf8NoBOM
Write-Output "Backup created: $file"
