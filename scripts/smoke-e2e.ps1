[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:8081",
    [string]$MockErpBaseUrl = "http://localhost:9999",
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
$tenantA = "11111111-1111-1111-1111-111111111111"
$tenantB = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
$warehouseA = "22222222-2222-2222-2222-222222222222"
$deviceId = "zebra-01"
$userId = "operator-01"
$headers = @{
    "X-Tenant-Id" = $tenantA
    "X-Warehouse-Id" = $warehouseA
    "X-Device-Id" = $deviceId
    "X-User-Id" = $userId
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "ASSERTION FAILED: $Message" }
}

function Invoke-Wms([string]$Method, [string]$Path, $Body = $null, $RequestHeaders = $headers) {
    $arguments = @{ Method = $Method; Uri = "$ApiBaseUrl$Path"; Headers = $RequestHeaders; UseBasicParsing = $true }
    if ($null -ne $Body) {
        $arguments.ContentType = "application/json"
        $arguments.Body = $Body | ConvertTo-Json -Depth 20 -Compress
    }
    $response = Invoke-WebRequest @arguments
    if ([string]::IsNullOrWhiteSpace($response.Content)) { return $null }
    return $response.Content | ConvertFrom-Json
}

function Wait-Until([scriptblock]$Probe, [string]$Description) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            $value = & $Probe
            if ($null -ne $value) { return $value }
        } catch { }
        Start-Sleep -Milliseconds 750
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Timed out waiting for $Description"
}

Wait-Until { try { Invoke-RestMethod "$ApiBaseUrl/health/ready" } catch { $null } } "API readiness" | Out-Null
Wait-Until { try { Invoke-RestMethod "$MockErpBaseUrl/health" } catch { $null } } "mock ERP readiness" | Out-Null

$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$asnExternalId = "ASN-SMOKE-$suffix"
$asnMessageId = [guid]::NewGuid()
$inboundCorrelationId = [guid]::NewGuid()
$asnEnvelope = @{
    messageId = $asnMessageId
    messageType = "AdvanceShippingNotice"
    schemaVersion = "1.0"
    tenantId = $tenantA
    occurredAt = [DateTimeOffset]::UtcNow.ToString("o")
    sourceSystem = "SMOKE-ERP"
    correlationId = $inboundCorrelationId
    causationId = $null
    payload = @{
        externalId = $asnExternalId
        warehouseCode = "WH01"
        ownerCode = "OWNER01"
        supplierExternalId = "SUP-01"
        expectedAt = [DateTimeOffset]::UtcNow.AddHours(1).ToString("o")
        lines = @(@{ externalLineId = "1"; sku = "SKU-001"; quantity = 20; uom = "EA" })
    }
}
$accepted = Invoke-Wms POST "/api/v1/integration/asns" $asnEnvelope
Assert-True ($accepted.messageId -eq $asnMessageId) "ASN acceptance must preserve MessageId"
$duplicate = Invoke-Wms POST "/api/v1/integration/asns" $asnEnvelope
Assert-True ([bool]$duplicate.alreadyProcessed) "duplicate ASN must be idempotent"

Wait-Until {
    $message = Invoke-Wms GET "/api/v1/integration/messages/$asnMessageId"
    if ($message.status -eq "Delivered") { $message } else { $null }
} "ASN import" | Out-Null
$asn = Invoke-Wms GET "/api/v1/inbound/asns/by-external/$asnExternalId"
$tasksPage = Wait-Until {
    $page = Invoke-Wms GET "/api/v1/mobile/tasks?since=0"
    if (@($page.tasks | Where-Object { $_.type -eq "Receive" -and $_.title -like "*$($asn.id)*" }).Count -gt 0) { $page } else { $null }
} "receive task"
$receiveTask = @($tasksPage.tasks | Where-Object { $_.type -eq "Receive" -and $_.title -like "*$($asn.id)*" })[0]
$receiptCommandId = [guid]::NewGuid()
$receiptBatch = @{ commands = @(@{
    commandId = $receiptCommandId; commandType = "ConfirmReceipt"; schemaVersion = "1.0";
    tenantId = $tenantA; warehouseId = $warehouseA; deviceId = $deviceId; userId = $userId;
    occurredAt = [DateTimeOffset]::UtcNow.ToString("o"); localSequence = 1; entityVersion = [long]$receiveTask.entityVersion;
    taskId = $receiveTask.taskId; payload = @{ barcode = "784000000001"; quantity = 20; destinationLocation = "STG-01" }
}) }
$receiptResult = (Invoke-Wms POST "/api/v1/mobile/commands:batch" $receiptBatch).results[0]
Assert-True ($receiptResult.status -eq "Accepted") "receipt command must be accepted"
$receiptDuplicate = (Invoke-Wms POST "/api/v1/mobile/commands:batch" $receiptBatch).results[0]
Assert-True ($receiptDuplicate.status -eq "AlreadyProcessed") "receipt command replay must be idempotent"

$putawayPage = Wait-Until {
    $page = Invoke-Wms GET "/api/v1/mobile/tasks?since=0"
    if (@($page.tasks | Where-Object { $_.type -eq "Putaway" -and $_.title -like "*$($asn.id)*" }).Count -gt 0) { $page } else { $null }
} "putaway task"
$putawayTask = @($putawayPage.tasks | Where-Object { $_.type -eq "Putaway" -and $_.title -like "*$($asn.id)*" })[0]
$putawayBatch = @{ commands = @(@{
    commandId = [guid]::NewGuid(); commandType = "ConfirmPutaway"; schemaVersion = "1.0";
    tenantId = $tenantA; warehouseId = $warehouseA; deviceId = $deviceId; userId = $userId;
    occurredAt = [DateTimeOffset]::UtcNow.ToString("o"); localSequence = 1; entityVersion = [long]$putawayTask.entityVersion;
    taskId = $putawayTask.taskId; payload = @{ barcode = "784000000001"; quantity = 20; sourceLocation = "STG-01"; destinationLocation = "A-01-01" }
}) }
$putawayResult = (Invoke-Wms POST "/api/v1/mobile/commands:batch" $putawayBatch).results[0]
Assert-True ($putawayResult.status -eq "Accepted") "putaway command must be accepted"
$asnClosed = Invoke-Wms GET "/api/v1/inbound/asns/by-external/$asnExternalId"
Assert-True ($asnClosed.status -eq "Completed") "ASN must close after putaway"
$stock = @(Invoke-Wms GET "/api/v1/inventory/stock?warehouseId=$warehouseA&sku=SKU-001")
Assert-True (@($stock | Where-Object { $_.locationId -eq "55555555-5555-5555-5555-555555555555" -and $_.onHand -eq 20 }).Count -eq 1) "putaway stock must exist at storage"

$orderExternalId = "SO-SMOKE-$suffix"
$orderMessageId = [guid]::NewGuid()
$outboundCorrelationId = [guid]::NewGuid()
$orderEnvelope = @{
    messageId = $orderMessageId; messageType = "SalesOrder"; schemaVersion = "1.0"; tenantId = $tenantA;
    occurredAt = [DateTimeOffset]::UtcNow.ToString("o"); sourceSystem = "SMOKE-ERP"; correlationId = $outboundCorrelationId; causationId = $null;
    payload = @{ externalId = $orderExternalId; warehouseCode = "WH01"; ownerCode = "OWNER01"; customerExternalId = "CUS-01";
        priority = 10; requestedShipAt = [DateTimeOffset]::UtcNow.AddHours(2).ToString("o");
        lines = @(@{ externalLineId = "1"; sku = "SKU-001"; quantity = 10; uom = "EA" }) }
}
Invoke-Wms POST "/api/v1/integration/sales-orders" $orderEnvelope | Out-Null
Wait-Until {
    $message = Invoke-Wms GET "/api/v1/integration/messages/$orderMessageId"
    if ($message.status -eq "Delivered") { $message } else { $null }
} "sales order import" | Out-Null
$order = Invoke-Wms GET "/api/v1/outbound/orders/by-external/$orderExternalId"
$released = Invoke-Wms POST "/api/v1/outbound/orders/$($order.id)/release" @{
    commandId = [guid]::NewGuid(); entityVersion = [long]$order.entityVersion; assigneeId = $userId; deviceId = $deviceId
}
Assert-True ($released.status -eq "Released") "order must be released"

$pickPage = Wait-Until {
    $page = Invoke-Wms GET "/api/v1/mobile/tasks?since=0"
    if (@($page.tasks | Where-Object { $_.type -eq "Pick" -and $_.title -like "*$($order.id)*" }).Count -gt 0) { $page } else { $null }
} "pick task"
$pickTask = @($pickPage.tasks | Where-Object { $_.type -eq "Pick" -and $_.title -like "*$($order.id)*" })[0]
$pickBatch = @{ commands = @(@{
    commandId = [guid]::NewGuid(); commandType = "ConfirmPick"; schemaVersion = "1.0";
    tenantId = $tenantA; warehouseId = $warehouseA; deviceId = $deviceId; userId = $userId;
    occurredAt = [DateTimeOffset]::UtcNow.ToString("o"); localSequence = 1; entityVersion = [long]$pickTask.entityVersion;
    taskId = $pickTask.taskId; payload = @{ barcode = "784000000001"; quantity = 10; sourceLocation = "A-01-01" }
}) }
$pickResult = (Invoke-Wms POST "/api/v1/mobile/commands:batch" $pickBatch).results[0]
Assert-True ($pickResult.status -eq "Accepted") "pick command must be accepted"
$order = Invoke-Wms GET "/api/v1/outbound/orders/by-external/$orderExternalId"
$packed = Invoke-Wms POST "/api/v1/outbound/orders/$($order.id)/pack" @{ commandId = [guid]::NewGuid(); entityVersion = [long]$order.entityVersion }
Assert-True ($packed.status -eq "Packed") "order must be packed online"
$dispatched = Invoke-Wms POST "/api/v1/outbound/orders/$($order.id)/dispatch" @{ commandId = [guid]::NewGuid(); entityVersion = [long]$packed.entityVersion }
Assert-True ($dispatched.status -eq "Shipped") "order must be shipped"

$shortOrderExternalId = "SO-SHORT-$suffix"
$shortOrderMessageId = [guid]::NewGuid()
$shortEnvelope = @{
    messageId = $shortOrderMessageId; messageType = "SalesOrder"; schemaVersion = "1.0"; tenantId = $tenantA;
    occurredAt = [DateTimeOffset]::UtcNow.ToString("o"); sourceSystem = "SMOKE-ERP"; correlationId = [guid]::NewGuid(); causationId = $null;
    payload = @{ externalId = $shortOrderExternalId; warehouseCode = "WH01"; ownerCode = "OWNER01"; customerExternalId = "CUS-02";
        priority = 20; requestedShipAt = [DateTimeOffset]::UtcNow.AddHours(2).ToString("o");
        lines = @(@{ externalLineId = "1"; sku = "SKU-001"; quantity = 10; uom = "EA" }) }
}
Invoke-Wms POST "/api/v1/integration/sales-orders" $shortEnvelope | Out-Null
Wait-Until {
    $message = Invoke-Wms GET "/api/v1/integration/messages/$shortOrderMessageId"
    if ($message.status -eq "Delivered") { $message } else { $null }
} "short-pick order import" | Out-Null
$shortOrder = Invoke-Wms GET "/api/v1/outbound/orders/by-external/$shortOrderExternalId"
Invoke-Wms POST "/api/v1/outbound/orders/$($shortOrder.id)/release" @{
    commandId = [guid]::NewGuid(); entityVersion = [long]$shortOrder.entityVersion; assigneeId = $userId; deviceId = $deviceId
} | Out-Null
$shortPickPage = Wait-Until {
    $page = Invoke-Wms GET "/api/v1/mobile/tasks?since=0"
    if (@($page.tasks | Where-Object { $_.type -eq "Pick" -and $_.title -like "*$($shortOrder.id)*" }).Count -gt 0) { $page } else { $null }
} "short-pick task"
$shortPickTask = @($shortPickPage.tasks | Where-Object { $_.type -eq "Pick" -and $_.title -like "*$($shortOrder.id)*" })[0]
$shortPickCommandId = [guid]::NewGuid()
$shortPickBatch = @{ commands = @(@{
    commandId = $shortPickCommandId; commandType = "ConfirmPick"; schemaVersion = "1.0";
    tenantId = $tenantA; warehouseId = $warehouseA; deviceId = $deviceId; userId = $userId;
    occurredAt = [DateTimeOffset]::UtcNow.ToString("o"); localSequence = 1; entityVersion = [long]$shortPickTask.entityVersion;
    taskId = $shortPickTask.taskId; payload = @{ barcode = "784000000001"; quantity = 6; sourceLocation = "A-01-01" }
}) }
$shortPickResult = (Invoke-Wms POST "/api/v1/mobile/commands:batch" $shortPickBatch).results[0]
Assert-True ($shortPickResult.status -eq "RequiresReview") "short pick must require supervisor review"
$shortLine = @($shortOrder.lines)[0]
Invoke-Wms POST "/api/v1/outbound/orders/$($shortOrder.id)/short-picks/$($shortLine.id)/decision" @{
    commandId = [guid]::NewGuid(); mobileCommandId = $shortPickCommandId; taskId = $shortPickTask.taskId; taskEntityVersion = [long]$shortPickTask.entityVersion;
    actualQuantity = 6; reason = "DAMAGED_STOCK"; approve = $true
} | Out-Null
$shortOrder = Invoke-Wms GET "/api/v1/outbound/orders/by-external/$shortOrderExternalId"
Assert-True ($shortOrder.lines[0].shortPickedQuantity -eq 4) "approved short pick must persist the shortage"
$shortPacked = Invoke-Wms POST "/api/v1/outbound/orders/$($shortOrder.id)/pack" @{ commandId = [guid]::NewGuid(); entityVersion = [long]$shortOrder.entityVersion }
$shortDispatched = Invoke-Wms POST "/api/v1/outbound/orders/$($shortOrder.id)/dispatch" @{ commandId = [guid]::NewGuid(); entityVersion = [long]$shortPacked.entityVersion }
Assert-True ($shortDispatched.status -eq "Shipped") "approved short-pick order must ship"

Wait-Until {
    $messages = (Invoke-RestMethod "$MockErpBaseUrl/messages").messages
    if (@($messages | Where-Object path -eq "/wms/receipts").Count -gt 0 -and @($messages | Where-Object path -eq "/wms/shipments").Count -ge 2) { $messages } else { $null }
} "ERP confirmations" | Out-Null

$tenantBHeaders = $headers.Clone()
$tenantBHeaders["X-Tenant-Id"] = $tenantB
$tenantBHeaders["X-Warehouse-Id"] = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
$isolated = $false
try { Invoke-Wms GET "/api/v1/inbound/asns/by-external/$asnExternalId" $null $tenantBHeaders | Out-Null }
catch { $isolated = $_.Exception.Response.StatusCode.value__ -eq 404 }
Assert-True $isolated "tenant B must not read tenant A ASN"

[PSCustomObject]@{
    Status = "PASS"
    Asn = $asnExternalId
    Order = $orderExternalId
    ShortPickOrder = $shortOrderExternalId
    TenantIsolation = "PASS"
    ReceiptConfirmation = "PASS"
    ShipmentConfirmation = "PASS"
} | Format-List
