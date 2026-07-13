[CmdletBinding()]
# TEST-OPS-0001: physical backup plus WAL replay to a selected recovery target.
param(
    [int]$RpoTargetSeconds = 300,
    [int]$RtoTargetSeconds = 60,
    [string]$EnvFile = ".env.example",
    [string]$ComposeFile = "docker-compose.smoke.yml",
    [string]$ProjectName = "wms-smoke",
    [string]$WalVolumeKey = "smoke-wal-archive",
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

function Invoke-SourceSql([string]$Container, [string]$User, [string]$Database, [string]$Query) {
    $value = & docker exec $Container psql --username $User --dbname $Database --quiet --no-align --tuples-only --set ON_ERROR_STOP=1 --command $Query
    if ($LASTEXITCODE -ne 0) { throw "Source PostgreSQL query failed." }
    return ($value | Out-String).Trim()
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
$baseVolume = "wms-pitr-base-$stamp"
$restoreVolume = "wms-pitr-restore-$stamp"
$restoreContainer = "wms-pitr-restore-$stamp"
$network = "${ProjectName}_default"
$manifest = Join-Path $outputPath "wms-pitr-recovery-$stamp.json"
$sourceContainer = $null
$walVolume = $null
$hbaPath = $null
$hbaModified = $false
$restoreStarted = $false

Push-Location $root
try {
    $sourceContainer = (& docker compose --env-file $envPath --project-name $ProjectName --file $composePath ps -q postgres).Trim()
    if (-not $sourceContainer) { throw "PostgreSQL container is not running for project $ProjectName." }
    $walVolume = (& docker volume ls --filter "label=com.docker.compose.project=$ProjectName" --filter "label=com.docker.compose.volume=$WalVolumeKey" --format "{{.Name}}" | Out-String).Trim()
    if (-not $walVolume) { throw "WAL archive volume $WalVolumeKey was not found." }

    $archiveMode = Invoke-SourceSql $sourceContainer $envMap.POSTGRES_USER $envMap.POSTGRES_DB "SELECT current_setting('archive_mode') || '|' || current_setting('archive_timeout');"
    if (-not $archiveMode.StartsWith("on|")) { throw "PostgreSQL WAL archiving is not enabled: $archiveMode." }

    $hbaPath = Invoke-SourceSql $sourceContainer $envMap.POSTGRES_USER $envMap.POSTGRES_DB "SHOW hba_file;"
    & docker exec $sourceContainer sh -c "echo 'host replication $($envMap.POSTGRES_USER) samenet scram-sha-256 # wms-pitr-drill' >> '$hbaPath'"
    if ($LASTEXITCODE -ne 0) { throw "Could not add temporary PITR replication access." }
    $hbaModified = $true
    Invoke-SourceSql $sourceContainer $envMap.POSTGRES_USER $envMap.POSTGRES_DB "SELECT pg_reload_conf();" | Out-Null

    Invoke-SourceSql $sourceContainer $envMap.POSTGRES_USER $envMap.POSTGRES_DB "SET client_min_messages = warning; DROP SCHEMA IF EXISTS recovery_drill CASCADE; CREATE SCHEMA recovery_drill; CREATE TABLE recovery_drill.probe (name text PRIMARY KEY, committed_at timestamptz NOT NULL); INSERT INTO recovery_drill.probe VALUES ('baseline', clock_timestamp()); CHECKPOINT; SELECT pg_switch_wal();" | Out-Null

    & docker volume create $baseVolume | Out-Null
    & docker run --rm --volume "${baseVolume}:/backup" postgres:17-alpine sh -c "chown postgres:postgres /backup"
    if ($LASTEXITCODE -ne 0) { throw "Could not prepare PITR base-backup volume." }
    $backupWatch = [Diagnostics.Stopwatch]::StartNew()
    & docker run --rm --user postgres --network $network --env "PGPASSWORD=$($envMap.POSTGRES_PASSWORD)" --volume "${baseVolume}:/backup" postgres:17-alpine pg_basebackup --host postgres --username $envMap.POSTGRES_USER --pgdata /backup --format plain --wal-method stream --checkpoint fast --no-password
    if ($LASTEXITCODE -ne 0) { throw "PITR pg_basebackup failed." }
    $backupWatch.Stop()

    $keepTime = Invoke-SourceSql $sourceContainer $envMap.POSTGRES_USER $envMap.POSTGRES_DB "INSERT INTO recovery_drill.probe VALUES ('keep', clock_timestamp()) RETURNING to_char(committed_at AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS.US') || '+00';"
    Start-Sleep -Seconds 2
    $targetTime = Invoke-SourceSql $sourceContainer $envMap.POSTGRES_USER $envMap.POSTGRES_DB "SELECT to_char(clock_timestamp() AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS.US') || '+00';"
    Start-Sleep -Seconds 2
    $excludeTime = Invoke-SourceSql $sourceContainer $envMap.POSTGRES_USER $envMap.POSTGRES_DB "INSERT INTO recovery_drill.probe VALUES ('exclude', clock_timestamp()) RETURNING to_char(committed_at AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS.US') || '+00';"
    Invoke-SourceSql $sourceContainer $envMap.POSTGRES_USER $envMap.POSTGRES_DB "SELECT pg_switch_wal();" | Out-Null

    $archiveDeadline = [DateTimeOffset]::UtcNow.AddSeconds(60)
    $archived = $false
    do {
        $lastArchived = Invoke-SourceSql $sourceContainer $envMap.POSTGRES_USER $envMap.POSTGRES_DB "SELECT coalesce(to_char(last_archived_time AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS.US') || '+00', '') FROM pg_stat_archiver;"
        $archived = $lastArchived -and [DateTimeOffset]::Parse($lastArchived) -ge [DateTimeOffset]::Parse($excludeTime)
        if (-not $archived) { Start-Sleep -Milliseconds 500 }
    } while (-not $archived -and [DateTimeOffset]::UtcNow -lt $archiveDeadline)
    if (-not $archived) { throw "WAL containing the post-target transaction was not archived within 60 seconds." }

    & docker volume create $restoreVolume | Out-Null
    $recoveryWatch = [Diagnostics.Stopwatch]::StartNew()
    & docker run --rm --volume "${baseVolume}:/source:ro" --volume "${restoreVolume}:/target" postgres:17-alpine sh -c "cp -a /source/. /target/"
    if ($LASTEXITCODE -ne 0) { throw "Could not copy PITR base backup." }
    & docker run --rm --user postgres --volume "${restoreVolume}:/target" postgres:17-alpine touch /target/recovery.signal
    if ($LASTEXITCODE -ne 0) { throw "Could not configure PITR recovery target." }

    & docker run --detach --name $restoreContainer --network $network --volume "${restoreVolume}:/var/lib/postgresql/data" --volume "${walVolume}:/wal-archive:ro" postgres:17-alpine postgres -c "restore_command=cp /wal-archive/%f %p" -c "recovery_target_time=$targetTime" -c "recovery_target_action=promote" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not start PITR restore container." }
    $restoreStarted = $true

    $ready = $false
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($RtoTargetSeconds)
    do {
        $running = (& docker inspect --format "{{.State.Running}}" $restoreContainer | Out-String).Trim()
        if ($running -ne "true") {
            $previousErrorAction = $ErrorActionPreference
            $ErrorActionPreference = "Continue"
            $restoreLogs = (& docker logs $restoreContainer 2>&1 | Out-String).Trim()
            $ErrorActionPreference = $previousErrorAction
            throw "PITR restore container exited before readiness. Logs: $restoreLogs"
        }
        & docker exec $restoreContainer pg_isready --username $envMap.POSTGRES_USER --dbname $envMap.POSTGRES_DB 2>$null | Out-Null
        $ready = $LASTEXITCODE -eq 0
        if (-not $ready) { Start-Sleep -Milliseconds 500 }
    } while (-not $ready -and [DateTimeOffset]::UtcNow -lt $deadline)
    if (-not $ready) { throw "PITR restore did not become ready within $RtoTargetSeconds seconds." }

    $probe = (& docker exec $restoreContainer psql --username $envMap.POSTGRES_USER --dbname $envMap.POSTGRES_DB --no-align --tuples-only --field-separator "|" --set ON_ERROR_STOP=1 --command "SELECT count(*) FILTER (WHERE name = 'baseline'), count(*) FILTER (WHERE name = 'keep'), count(*) FILTER (WHERE name = 'exclude'), to_char(max(committed_at) AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS.US') || '+00' FROM recovery_drill.probe;" | Out-String).Trim().Split('|')
    if ($LASTEXITCODE -ne 0 -or $probe.Count -ne 4) { throw "PITR probe validation failed." }
    $baselineCount, $keepCount, $excludeCount, $lastRecoveredTime = $probe
    $recoveryWatch.Stop()
    if ($baselineCount -ne "1" -or $keepCount -ne "1" -or $excludeCount -ne "0") { throw "PITR target is incorrect: baseline=$baselineCount keep=$keepCount exclude=$excludeCount." }

    $rpoSeconds = ([DateTimeOffset]::Parse($targetTime) - [DateTimeOffset]::Parse($lastRecoveredTime)).TotalSeconds
    $rpoMet = $rpoSeconds -le $RpoTargetSeconds
    $rtoMet = $recoveryWatch.Elapsed.TotalSeconds -lt $RtoTargetSeconds
    $result = [ordered]@{
        status = if ($rpoMet -and $rtoMet) { "PASS" } else { "FAILED" }
        createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        method = "pg_basebackup-wal-pitr"
        archiveMode = $archiveMode
        baseBackupSeconds = [math]::Round($backupWatch.Elapsed.TotalSeconds, 3)
        recoverySeconds = [math]::Round($recoveryWatch.Elapsed.TotalSeconds, 3)
        rtoTargetSeconds = $RtoTargetSeconds
        rtoMet = $rtoMet
        targetTimeUtc = $targetTime
        keepCommittedAtUtc = $keepTime
        excludedCommittedAtUtc = $excludeTime
        lastRecoveredAtUtc = $lastRecoveredTime
        rpoSeconds = [math]::Round($rpoSeconds, 3)
        rpoTargetSeconds = $RpoTargetSeconds
        rpoMet = $rpoMet
        baselineRecovered = $baselineCount -eq "1"
        keepRecovered = $keepCount -eq "1"
        postTargetExcluded = $excludeCount -eq "0"
    }
    $result | ConvertTo-Json | Set-Content -LiteralPath $manifest -Encoding UTF8
    [pscustomobject]$result
    if (-not $rpoMet -or -not $rtoMet) { throw "PITR recovery did not meet RPO/RTO targets." }
}
finally {
    if ($restoreStarted) { & docker rm --force $restoreContainer 2>$null | Out-Null }
    & docker volume rm --force $restoreVolume 2>$null | Out-Null
    & docker volume rm --force $baseVolume 2>$null | Out-Null
    if ($sourceContainer) {
        & docker exec $sourceContainer psql --username $envMap.POSTGRES_USER --dbname $envMap.POSTGRES_DB --command "SET client_min_messages = warning; DROP SCHEMA IF EXISTS recovery_drill CASCADE;" 2>$null | Out-Null
    }
    if ($hbaModified -and $sourceContainer -and $hbaPath) {
        & docker exec $sourceContainer sed -i "/# wms-pitr-drill$/d" $hbaPath 2>$null
        & docker exec $sourceContainer psql --username $envMap.POSTGRES_USER --dbname $envMap.POSTGRES_DB --command "SELECT pg_reload_conf();" 2>$null | Out-Null
    }
    Pop-Location
}
