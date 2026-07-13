[CmdletBinding()]
param(
    [ValidateRange(2, 10000000)]
    [int]$MovementCount = 5000000,
    [string]$EnvFile = ".env.example",
    [string]$ComposeFile = "docker-compose.smoke.yml",
    [string]$ProjectName = "wms-smoke"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
if ($MovementCount % 2 -ne 0) { throw "MovementCount must be even so the synthetic ledger has a zero net quantity." }

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

function Invoke-Psql([string]$Container, [string]$User, [string]$Database, [string]$Query) {
    $value = & docker exec $Container psql --username $User --dbname $Database --no-align --tuples-only --set ON_ERROR_STOP=1 --command $Query
    if ($LASTEXITCODE -ne 0) { throw "psql query failed." }
    return ($value | Out-String).Trim()
}

function Measure-ServerQuery([string]$Container, [string]$User, [string]$Database, [string]$Query) {
    $json = Invoke-Psql $Container $User $Database "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) $Query"
    $plan = $json | ConvertFrom-Json
    return [decimal]$plan.'Execution Time'
}

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$envPath = if ([IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $root $EnvFile }
$composePath = if ([IO.Path]::IsPathRooted($ComposeFile)) { $ComposeFile } else { Join-Path $root $ComposeFile }
$sqlPath = Join-Path $root "tests/performance/historical-volume.sql"
$envMap = Read-EnvFile $envPath
foreach ($key in @("POSTGRES_USER", "POSTGRES_DB")) {
    if (-not $envMap.ContainsKey($key)) { throw "$key is required in $envPath" }
}

Push-Location $root
try {
    $container = (& docker compose --env-file $envPath --project-name $ProjectName --file $composePath ps -q postgres).Trim()
    if (-not $container) { throw "PostgreSQL container is not running for project $ProjectName." }

    $loadWatch = [Diagnostics.Stopwatch]::StartNew()
    Get-Content -Raw -LiteralPath $sqlPath |
        & docker exec --interactive $container psql --username $envMap.POSTGRES_USER --dbname $envMap.POSTGRES_DB --set ON_ERROR_STOP=1 --set "movement_count=$MovementCount"
    if ($LASTEXITCODE -ne 0) { throw "Historical movement load failed." }
    $loadWatch.Stop()

    $tenantId = "11111111-1111-1111-1111-111111111111"
    $dimensionId = "77777777-7777-4777-8777-777777777777"
    $count = [long](Invoke-Psql $container $envMap.POSTGRES_USER $envMap.POSTGRES_DB "SELECT count(*) FROM inventory.movement WHERE tenant_id = '$tenantId' AND stock_dimension_id = '$dimensionId';")
    $net = [decimal](Invoke-Psql $container $envMap.POSTGRES_USER $envMap.POSTGRES_DB "SELECT coalesce(sum(quantity), 0) FROM inventory.movement WHERE tenant_id = '$tenantId' AND stock_dimension_id = '$dimensionId';")
    $tenantB = [long](Invoke-Psql $container $envMap.POSTGRES_USER $envMap.POSTGRES_DB "SELECT count(*) FROM inventory.movement WHERE tenant_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';")

    $latestWatch = [Diagnostics.Stopwatch]::StartNew()
    $latestServerMs = Measure-ServerQuery $container $envMap.POSTGRES_USER $envMap.POSTGRES_DB "SELECT id FROM inventory.movement WHERE tenant_id = '$tenantId' AND stock_dimension_id = '$dimensionId' ORDER BY occurred_at DESC, id DESC LIMIT 100;"
    $latestWatch.Stop()

    $lookupSequence = [math]::Floor($MovementCount / 2)
    $lookupSuffix = ([Convert]::ToString($lookupSequence, 16)).PadLeft(12, '0')
    $lookupWatch = [Diagnostics.Stopwatch]::StartNew()
    $lookupServerMs = Measure-ServerQuery $container $envMap.POSTGRES_USER $envMap.POSTGRES_DB "SELECT id FROM inventory.movement WHERE tenant_id = '$tenantId' AND command_id = '71000000-0000-4000-8000-$lookupSuffix';"
    $lookupWatch.Stop()

    if ($count -ne $MovementCount) { throw "Expected $MovementCount historical movements, found $count." }
    if ($net -ne 0) { throw "Synthetic historical ledger must net to zero; found $net." }
    if ($tenantB -ne 0) { throw "Historical fixture leaked into tenant B." }

    [pscustomobject]@{
        Status = "PASS"
        MovementCount = $count
        LoadSeconds = [math]::Round($loadWatch.Elapsed.TotalSeconds, 3)
        Latest100ServerMilliseconds = [math]::Round($latestServerMs, 3)
        Latest100ClientMilliseconds = [math]::Round($latestWatch.Elapsed.TotalMilliseconds, 3)
        CommandLookupServerMilliseconds = [math]::Round($lookupServerMs, 3)
        CommandLookupClientMilliseconds = [math]::Round($lookupWatch.Elapsed.TotalMilliseconds, 3)
        NetQuantity = $net
        TenantBCount = $tenantB
    }
}
finally { Pop-Location }
