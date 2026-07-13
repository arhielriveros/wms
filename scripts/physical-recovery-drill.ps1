[CmdletBinding()]
param(
    [long]$ExpectedMovementCount = 5000000,
    [int]$RtoTargetSeconds = 60,
    [string]$EnvFile = ".env.example",
    [string]$ComposeFile = "docker-compose.smoke.yml",
    [string]$ProjectName = "wms-smoke",
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
$composePath = if ([IO.Path]::IsPathRooted($ComposeFile)) { $ComposeFile } else { Join-Path $root $ComposeFile }
$outputPath = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $root $OutputDirectory }
[IO.Directory]::CreateDirectory($outputPath) | Out-Null
$envMap = Read-EnvFile $envPath
foreach ($key in @("POSTGRES_USER", "POSTGRES_DB", "POSTGRES_PASSWORD")) {
    if (-not $envMap.ContainsKey($key)) { throw "$key is required in $envPath" }
}

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$baseVolume = "wms-physical-base-$stamp"
$restoreVolume = "wms-physical-restore-$stamp"
$restoreContainer = "wms-physical-restore-$stamp"
$network = "${ProjectName}_default"
$manifest = Join-Path $outputPath "wms-physical-recovery-$stamp.json"
$containerStarted = $false
$hbaModified = $false
$sourceContainer = $null
$hbaPath = $null

Push-Location $root
try {
    $sourceContainer = (& docker compose --env-file $envPath --project-name $ProjectName --file $composePath ps -q postgres).Trim()
    if (-not $sourceContainer) { throw "PostgreSQL container is not running for project $ProjectName." }
    $hbaPath = (& docker exec $sourceContainer psql --username $envMap.POSTGRES_USER --dbname $envMap.POSTGRES_DB --no-align --tuples-only --command "SHOW hba_file;" | Out-String).Trim()
    if (-not $hbaPath) { throw "Could not resolve pg_hba.conf path." }
    & docker exec $sourceContainer sh -c "echo 'host replication $($envMap.POSTGRES_USER) samenet scram-sha-256 # wms-physical-drill' >> '$hbaPath'"
    if ($LASTEXITCODE -ne 0) { throw "Could not add temporary replication access." }
    $hbaModified = $true
    & docker exec $sourceContainer psql --username $envMap.POSTGRES_USER --dbname $envMap.POSTGRES_DB --set ON_ERROR_STOP=1 --command "SELECT pg_reload_conf();" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not reload temporary replication access." }

    & docker volume create $baseVolume | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not create physical base-backup volume." }
    & docker run --rm --volume "${baseVolume}:/backup" postgres:17-alpine sh -c "chown postgres:postgres /backup"
    if ($LASTEXITCODE -ne 0) { throw "Could not prepare physical base-backup volume." }

    $backupWatch = [Diagnostics.Stopwatch]::StartNew()
    & docker run --rm --user postgres --network $network --env "PGPASSWORD=$($envMap.POSTGRES_PASSWORD)" --volume "${baseVolume}:/backup" postgres:17-alpine pg_basebackup --host postgres --username $envMap.POSTGRES_USER --pgdata /backup --format plain --wal-method stream --checkpoint fast --no-password
    if ($LASTEXITCODE -ne 0) { throw "pg_basebackup failed." }
    $backupWatch.Stop()

    & docker volume create $restoreVolume | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not create physical restore volume." }
    $recoveryWatch = [Diagnostics.Stopwatch]::StartNew()
    & docker run --rm --volume "${baseVolume}:/source:ro" --volume "${restoreVolume}:/target" postgres:17-alpine sh -c "cp -a /source/. /target/"
    if ($LASTEXITCODE -ne 0) { throw "Could not copy the physical base backup into the restore volume." }

    & docker run --detach --name $restoreContainer --network $network --volume "${restoreVolume}:/var/lib/postgresql/data" postgres:17-alpine | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not start the physical restore container." }
    $containerStarted = $true

    $ready = $false
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($RtoTargetSeconds)
    do {
        & docker exec $restoreContainer pg_isready --username $envMap.POSTGRES_USER --dbname $envMap.POSTGRES_DB 2>$null | Out-Null
        $ready = $LASTEXITCODE -eq 0
        if (-not $ready) { Start-Sleep -Milliseconds 500 }
    } while (-not $ready -and [DateTimeOffset]::UtcNow -lt $deadline)
    if (-not $ready) { throw "Physical restore did not become ready before the RTO deadline." }

    $validation = (& docker exec $restoreContainer psql --username $envMap.POSTGRES_USER --dbname $envMap.POSTGRES_DB --no-align --tuples-only --field-separator "|" --set ON_ERROR_STOP=1 --command "SELECT count(*) FILTER (WHERE stock_dimension_id = '77777777-7777-4777-8777-777777777777'), coalesce(sum(quantity) FILTER (WHERE stock_dimension_id = '77777777-7777-4777-8777-777777777777'), 0), count(*) FILTER (WHERE tenant_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa') FROM inventory.movement;" | Out-String).Trim().Split('|')
    if ($LASTEXITCODE -ne 0 -or $validation.Count -ne 3) { throw "Physical recovery validation failed." }
    $count, $net, $tenantB = $validation
    $recoveryWatch.Stop()

    if ([long]$count -ne $ExpectedMovementCount) { throw "Physical recovery movement count mismatch: $count." }
    if ([decimal]$net -ne 0) { throw "Physical recovery ledger net mismatch: $net." }
    if ([long]$tenantB -ne 0) { throw "Physical recovery tenant B count must be zero: $tenantB." }

    $rtoMet = $recoveryWatch.Elapsed.TotalSeconds -lt $RtoTargetSeconds
    $result = [ordered]@{
        status = if ($rtoMet) { "PASS" } else { "RTO_MISSED" }
        createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        method = "pg_basebackup-physical-copy"
        movementCount = [long]$count
        netQuantity = [decimal]$net
        tenantBCount = [long]$tenantB
        baseBackupSeconds = [math]::Round($backupWatch.Elapsed.TotalSeconds, 3)
        recoverySeconds = [math]::Round($recoveryWatch.Elapsed.TotalSeconds, 3)
        rtoTargetSeconds = $RtoTargetSeconds
        rtoMet = $rtoMet
    }
    $result | ConvertTo-Json | Set-Content -LiteralPath $manifest -Encoding UTF8
    [pscustomobject]$result
    if (-not $rtoMet) { throw "Physical recovery took $([math]::Round($recoveryWatch.Elapsed.TotalSeconds, 3)) seconds; target is under $RtoTargetSeconds seconds." }
}
finally {
    if ($containerStarted) { & docker rm --force $restoreContainer 2>$null | Out-Null }
    & docker volume rm --force $restoreVolume 2>$null | Out-Null
    & docker volume rm --force $baseVolume 2>$null | Out-Null
    if ($hbaModified -and $sourceContainer -and $hbaPath) {
        & docker exec $sourceContainer sed -i "/# wms-physical-drill$/d" $hbaPath 2>$null
        & docker exec $sourceContainer psql --username $envMap.POSTGRES_USER --dbname $envMap.POSTGRES_DB --command "SELECT pg_reload_conf();" 2>$null | Out-Null
    }
    Pop-Location
}
