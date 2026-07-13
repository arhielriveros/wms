[CmdletBinding()]
param(
    [long]$ExpectedMovementCount = 5000000,
    [string]$EnvFile = ".env.example",
    [string]$ComposeFile = "docker-compose.smoke.yml",
    [string]$ProjectName = "wms-smoke",
    [string]$OutputDirectory = ".backups",
    [switch]$EnforceRto
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
$composePath = if ([IO.Path]::IsPathRooted($ComposeFile)) { $ComposeFile } else { Join-Path $root $ComposeFile }
$outputPath = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $root $OutputDirectory }
[IO.Directory]::CreateDirectory($outputPath) | Out-Null
$envMap = Read-EnvFile $envPath
foreach ($key in @("POSTGRES_USER", "POSTGRES_DB")) {
    if (-not $envMap.ContainsKey($key)) { throw "$key is required in $envPath" }
}

$restoreDatabase = "wms_restore_drill"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$remoteDump = "/tmp/wms-recovery-$stamp.dump"
$localDump = Join-Path $outputPath "wms-recovery-$stamp.dump"
$manifest = "$localDump.json"
$container = $null

Push-Location $root
try {
    $container = (& docker compose --env-file $envPath --project-name $ProjectName --file $composePath ps -q postgres).Trim()
    if (-not $container) { throw "PostgreSQL container is not running for project $ProjectName." }

    $backupWatch = [Diagnostics.Stopwatch]::StartNew()
    & docker exec $container pg_dump --username $envMap.POSTGRES_USER --dbname $envMap.POSTGRES_DB --format custom --compress 1 --file $remoteDump
    if ($LASTEXITCODE -ne 0) { throw "pg_dump failed." }
    & docker cp "${container}:$remoteDump" $localDump
    if ($LASTEXITCODE -ne 0) { throw "Could not copy the recovery dump." }
    $backupWatch.Stop()

    & docker exec $container dropdb --username $envMap.POSTGRES_USER --if-exists --force $restoreDatabase
    if ($LASTEXITCODE -ne 0) { throw "Could not reset isolated restore database." }
    & docker exec $container createdb --username $envMap.POSTGRES_USER $restoreDatabase
    if ($LASTEXITCODE -ne 0) { throw "Could not create isolated restore database." }
    & docker exec $container psql --username $envMap.POSTGRES_USER --dbname postgres --set ON_ERROR_STOP=1 --command "ALTER DATABASE $restoreDatabase SET maintenance_work_mem = '512MB'; ALTER DATABASE $restoreDatabase SET max_parallel_maintenance_workers = 4; ALTER DATABASE $restoreDatabase SET synchronous_commit = off;"
    if ($LASTEXITCODE -ne 0) { throw "Could not apply isolated restore tuning." }

    $restoreWatch = [Diagnostics.Stopwatch]::StartNew()
    & docker exec $container pg_restore --username $envMap.POSTGRES_USER --dbname $restoreDatabase --no-owner --no-privileges --exit-on-error --jobs 4 $remoteDump
    if ($LASTEXITCODE -ne 0) { throw "pg_restore failed." }
    $restoreWatch.Stop()

    $validationWatch = [Diagnostics.Stopwatch]::StartNew()
    $validation = (& docker exec $container psql --username $envMap.POSTGRES_USER --dbname $restoreDatabase --no-align --tuples-only --field-separator "|" --set ON_ERROR_STOP=1 --command "SELECT count(*) FILTER (WHERE stock_dimension_id = '77777777-7777-4777-8777-777777777777'), coalesce(sum(quantity) FILTER (WHERE stock_dimension_id = '77777777-7777-4777-8777-777777777777'), 0), count(*) FILTER (WHERE tenant_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa') FROM inventory.movement;" | Out-String).Trim().Split('|')
    if ($validation.Count -ne 3) { throw "Recovery validation returned an unexpected result." }
    $count, $net, $tenantB = $validation
    $validationWatch.Stop()
    if ([long]$count -ne $ExpectedMovementCount) { throw "Restored movement count mismatch: $count." }
    if ([decimal]$net -ne 0) { throw "Restored ledger net quantity mismatch: $net." }
    if ([long]$tenantB -ne 0) { throw "Restored tenant B historical count must be zero: $tenantB." }

    $recoverySeconds = $restoreWatch.Elapsed.TotalSeconds + $validationWatch.Elapsed.TotalSeconds
    $rtoMet = $recoverySeconds -lt 60
    $result = [ordered]@{
        status = if ($rtoMet) { "PASS" } else { "RTO_MISSED" }
        createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        sourceDatabase = $envMap.POSTGRES_DB
        restoredDatabase = $restoreDatabase
        movementCount = [long]$count
        netQuantity = [decimal]$net
        tenantBCount = [long]$tenantB
        backupSeconds = [math]::Round($backupWatch.Elapsed.TotalSeconds, 3)
        restoreSeconds = [math]::Round($restoreWatch.Elapsed.TotalSeconds, 3)
        validationSeconds = [math]::Round($validationWatch.Elapsed.TotalSeconds, 3)
        recoverySeconds = [math]::Round($recoverySeconds, 3)
        rtoTargetSeconds = 60
        rtoMet = $rtoMet
        dumpBytes = (Get-Item -LiteralPath $localDump).Length
        sha256 = (Get-FileHash -LiteralPath $localDump -Algorithm SHA256).Hash
    }
    $result | ConvertTo-Json | Set-Content -LiteralPath $manifest -Encoding UTF8
    [pscustomobject]$result
    if ($EnforceRto -and -not $rtoMet) { throw "Recovery plus validation took $([math]::Round($recoverySeconds, 3)) seconds; RTO target is under 60 seconds." }
}
finally {
    if ($container) {
        & docker exec $container dropdb --username $envMap.POSTGRES_USER --if-exists --force $restoreDatabase 2>$null
        & docker exec $container rm -f $remoteDump 2>$null
    }
    Pop-Location
}
