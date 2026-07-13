[CmdletBinding()]
# TEST-OPS-0004: real API/worker metrics, traces and logs plus operational dependency checks.
param(
    [string]$EnvFile = ".env.example",
    [ValidatePattern("^[a-z0-9][a-z0-9_-]+$")]
    [string]$ProjectName = "wms-observability",
    [int]$TimeoutSeconds = 300,
    [string]$OutputDirectory = ".backups",
    [switch]$KeepEnvironment
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

function Wait-Until([scriptblock]$Probe, [string]$Description, [int]$Seconds = $TimeoutSeconds) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($Seconds)
    do {
        try {
            $value = & $Probe
            if ($null -ne $value) { return $value }
        }
        catch { }
        Start-Sleep -Milliseconds 1000
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Timed out waiting for $Description."
}

function Invoke-Json([string]$Uri, [hashtable]$Headers = @{}) {
    return Invoke-RestMethod -Method Get -Uri $Uri -Headers $Headers -UseBasicParsing -TimeoutSec 15
}

function Find-Metric([string]$Fragment) {
    $catalog = Invoke-Json "$script:prometheusBase/api/v1/label/__name__/values"
    $match = @($catalog.data | Where-Object { $_ -like "*$Fragment*" }) | Select-Object -First 1
    if (-not $match) { return $null }
    $query = [Uri]::EscapeDataString("sum($match)")
    $sample = Invoke-Json "$script:prometheusBase/api/v1/query?query=$query"
    if ($sample.status -ne "success" -or @($sample.data.result).Count -eq 0) { return $null }
    if ([double]$sample.data.result[0].value[1] -le 0) { return $null }
    return [string]$match
}

function Find-Traces([string]$ServiceName) {
    $traceQl = [Uri]::EscapeDataString("{ resource.service.name = `"$ServiceName`" }")
    $response = Invoke-Json "$script:tempoBase/api/search?q=$traceQl&limit=20"
    $count = @($response.traces).Count
    if ($count -eq 0) { return $null }
    return $count
}

function Find-Logs([string]$ServiceName) {
    $query = [Uri]::EscapeDataString("{service_name=`"$ServiceName`"}")
    $end = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $start = $end - 600
    $response = Invoke-Json "$script:lokiBase/loki/api/v1/query_range?query=$query&start=$($start)000000000&end=$($end)000000000&limit=50"
    $count = @($response.data.result).Count
    if ($count -eq 0) { return $null }
    return $count
}

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$envPath = if ([IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $root $EnvFile }
$composePath = Join-Path $root "docker-compose.yml"
$outputPath = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $root $OutputDirectory }
foreach ($requiredPath in @($envPath, $composePath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Required observability drill file not found: $requiredPath" }
}
[IO.Directory]::CreateDirectory($outputPath) | Out-Null
$envMap = Read-EnvFile $envPath
$stamp = Get-Date -Format "yyyyMMddHHmmss"
$manifest = Join-Path $outputPath "wms-observability-$stamp.json"
$startedAt = [DateTimeOffset]::UtcNow
$script:checks = [ordered]@{}

$ports = [ordered]@{
    POSTGRES_PORT = "25432"; RABBITMQ_PORT = "25672"; RABBITMQ_MANAGEMENT_PORT = "25673"; REDIS_PORT = "26379"
    KEYCLOAK_PORT = "28080"; KEYCLOAK_MANAGEMENT_PORT = "29000"; MINIO_API_PORT = "29001"; MINIO_CONSOLE_PORT = "29002"
    MOCK_ERP_PORT = "29999"; OTEL_GRPC_PORT = "24317"; OTEL_HTTP_PORT = "24318"; OTEL_HEALTH_PORT = "23133"
    OTEL_METRICS_PORT = "29464"; PROMETHEUS_PORT = "29090"; LOKI_PORT = "23100"; TEMPO_PORT = "23200"
    GRAFANA_PORT = "23001"; WMS_API_PORT = "28081"; WMS_WEB_PORT = "23000"
}
$savedEnvironment = @{}
foreach ($entry in $ports.GetEnumerator()) {
    $savedEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, "Process")
    [Environment]::SetEnvironmentVariable($entry.Key, [string]$entry.Value, "Process")
}

$script:apiBase = "http://127.0.0.1:$($ports.WMS_API_PORT)"
$script:rabbitBase = "http://127.0.0.1:$($ports.RABBITMQ_MANAGEMENT_PORT)"
$script:collectorBase = "http://127.0.0.1:$($ports.OTEL_HEALTH_PORT)"
$script:prometheusBase = "http://127.0.0.1:$($ports.PROMETHEUS_PORT)"
$script:lokiBase = "http://127.0.0.1:$($ports.LOKI_PORT)"
$script:tempoBase = "http://127.0.0.1:$($ports.TEMPO_PORT)"
$script:grafanaBase = "http://127.0.0.1:$($ports.GRAFANA_PORT)"
$composeArguments = @("compose", "--env-file", $envPath, "--project-name", $ProjectName, "--file", $composePath, "--profile", "app")

Push-Location $root
try {
    $nativeErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & docker @composeArguments down --volumes --remove-orphans 2>$null | Out-Null
    $ErrorActionPreference = $nativeErrorAction

    $infrastructure = @("postgres", "rabbitmq", "rabbitmq-init", "redis", "keycloak", "minio", "mock-erp", "loki", "tempo", "otel-collector", "prometheus", "grafana")
    & docker @composeArguments up --detach @infrastructure
    if ($LASTEXITCODE -ne 0) { throw "Could not start the isolated observability infrastructure." }

    & docker @composeArguments run --rm minio-init | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "MinIO evidence bucket initialization failed." }
    $script:checks.minioBucket = "PASS"

    Wait-Until { try { Invoke-Json "$script:collectorBase/" } catch { $null } } "OpenTelemetry Collector" 90 | Out-Null
    $script:checks.collectorReady = "PASS"

    $rabbitToken = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($envMap.RABBITMQ_USER):$($envMap.RABBITMQ_PASSWORD)"))
    $rabbitHeaders = @{ Authorization = "Basic $rabbitToken" }
    Wait-Until { try { Invoke-Json "$script:rabbitBase/api/overview" $rabbitHeaders } catch { $null } } "RabbitMQ management API" 60 | Out-Null
    $deadLetterQueue = Wait-Until { try { Invoke-Json "$script:rabbitBase/api/queues/%2F/wms.dlq" $rabbitHeaders } catch { $null } } "RabbitMQ dead-letter queue" 60
    if ($deadLetterQueue.name -ne "wms.dlq") { throw "RabbitMQ dead-letter queue is not provisioned." }
    $script:checks.rabbitMq = "PASS"

    $redisProbe = & docker @composeArguments exec -T -e "REDISCLI_AUTH=$($envMap.REDIS_PASSWORD)" redis redis-cli PING
    if ($LASTEXITCODE -ne 0 -or ($redisProbe -join "").Trim() -ne "PONG") { throw "Redis authenticated PING failed." }
    $script:checks.redis = "PASS"

    $grafanaToken = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($envMap.GRAFANA_ADMIN_USER):$($envMap.GRAFANA_ADMIN_PASSWORD)"))
    $grafanaHeaders = @{ Authorization = "Basic $grafanaToken" }
    $grafanaHealth = Wait-Until { try { Invoke-Json "$script:grafanaBase/api/health" $grafanaHeaders } catch { $null } } "Grafana API" 60
    if ($grafanaHealth.database -ne "ok") { throw "Grafana database is not ready." }
    foreach ($required in @("Prometheus", "Loki", "Tempo")) {
        $datasource = Wait-Until {
            try { Invoke-Json "$script:grafanaBase/api/datasources/name/$required" $grafanaHeaders } catch { $null }
        } "Grafana datasource $required" 60
        if ($datasource.name -ne $required) { throw "Grafana datasource $required is not provisioned." }
    }
    $dashboard = Wait-Until {
        try { Invoke-Json "$script:grafanaBase/api/dashboards/uid/wms-overview" $grafanaHeaders } catch { $null }
    } "WMS Grafana dashboard" 60
    if ($dashboard.dashboard.uid -ne "wms-overview") { throw "WMS Grafana dashboard is not provisioned." }
    $script:checks.grafanaProvisioning = "PASS"

    & docker @composeArguments up --detach --build --wait --wait-timeout $TimeoutSeconds api worker
    if ($LASTEXITCODE -ne 0) { throw "Could not start the instrumented API and worker." }
    Wait-Until { try { Invoke-Json "$script:apiBase/health/ready" } catch { $null } } "WMS API" 90 | Out-Null
    $script:checks.apiReady = "PASS"
    $script:checks.workerReady = "PASS"

    $tenantId = "11111111-1111-1111-1111-111111111111"
    $headers = @{ "X-Tenant-Id" = $tenantId; "X-User-Id" = "observability-drill"; "X-Scopes" = "wms.inventory.read wms.supervisor.read" }
    1..5 | ForEach-Object {
        Invoke-RestMethod -Method Get -Uri "$script:apiBase/api/v1/inventory/stock" -Headers $headers -UseBasicParsing -TimeoutSec 15 | Out-Null
    }

    $apiMetric = Wait-Until { Find-Metric "wms_api_requests" } "API request metric" 90
    $workerMetric = Wait-Until { Find-Metric "wms_worker_outbox_polls" } "worker outbox poll metric" 90
    $apiTraces = Wait-Until { Find-Traces "wms-api" } "API traces in Tempo" 90
    $workerTraces = Wait-Until { Find-Traces "wms-worker" } "worker traces in Tempo" 90
    $apiLogs = Wait-Until { Find-Logs "wms-api" } "API logs in Loki" 90
    $workerLogs = Wait-Until { Find-Logs "wms-worker" } "worker logs in Loki" 90
    $script:checks.telemetry = [ordered]@{
        apiMetric = $apiMetric; workerMetric = $workerMetric
        apiTraceResults = $apiTraces; workerTraceResults = $workerTraces
        apiLogStreams = $apiLogs; workerLogStreams = $workerLogs
    }

    $result = [ordered]@{
        status = "PASS"
        createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        durationSeconds = [math]::Round(([DateTimeOffset]::UtcNow - $startedAt).TotalSeconds, 2)
        testId = "TEST-OPS-0004"
        scope = "API, worker, PostgreSQL, RabbitMQ, Redis, MinIO and LGTM observability stack"
        checks = $script:checks
        secretsPersisted = $false
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifest -Encoding UTF8
    [pscustomobject]$result
}
catch {
    $failure = [ordered]@{
        status = "FAIL"; createdAtUtc = (Get-Date).ToUniversalTime().ToString("o"); testId = "TEST-OPS-0004"
        failedCheck = $_.Exception.Message; completedChecks = $script:checks; secretsPersisted = $false
    }
    $failure | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifest -Encoding UTF8
    $diagnosticErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & docker @composeArguments logs --no-color --tail 200 api worker otel-collector prometheus loki tempo grafana 2>$null | Write-Warning
    $ErrorActionPreference = $diagnosticErrorAction
    throw
}
finally {
    if (-not $KeepEnvironment) {
        $cleanupErrorAction = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        & docker @composeArguments down --volumes --remove-orphans 2>$null | Out-Null
        $ErrorActionPreference = $cleanupErrorAction
    }
    foreach ($entry in $ports.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $savedEnvironment[$entry.Key], "Process")
    }
    Pop-Location
}
