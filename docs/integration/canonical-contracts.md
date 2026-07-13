# Contratos canónicos de integración

## Convenciones

- API: `/api/v1`; JSON UTF-8; timestamps ISO-8601 UTC; UUID para IDs WMS.
- Schemas identificados por `MessageType` + `SchemaVersion`; cambios compatibles agregan campos opcionales, cambios incompatibles incrementan versión mayor.
- Cantidades son decimales positivos con UOM explícita; códigos externos conservan sistema origen.
- `MessageId` es clave idempotente global por origen/tipo; payload distinto con el mismo ID retorna conflicto.

## Envelope

```json
{
  "messageId": "4d45f71b-4fe2-4eaf-899f-d7719a19044c",
  "messageType": "AdvanceShippingNotice",
  "schemaVersion": "1.0",
  "tenantId": "5a1361bc-8432-4eba-ae43-315bad780b91",
  "occurredAt": "2026-07-13T14:00:00Z",
  "sourceSystem": "GENERIC_ERP",
  "correlationId": "b375775d-582e-4d88-a3d7-a1b39efe7447",
  "causationId": null,
  "payload": {}
}
```

## AdvanceShippingNotice 1.0

```json
{
  "externalId": "ASN-1001",
  "warehouseCode": "WH01",
  "ownerCode": "OWN01",
  "supplierExternalId": "SUP-09",
  "expectedAt": "2026-07-14T12:00:00Z",
  "lines": [
    {"externalLineId":"1","sku":"SKU-001","quantity":10,"uom":"EA"}
  ]
}
```

## SalesOrder 1.0

```json
{
  "externalId": "SO-100245",
  "warehouseCode": "WH01",
  "ownerCode": "OWN01",
  "customerExternalId": "C00045",
  "priority": 50,
  "requestedShipAt": "2026-07-15T18:00:00Z",
  "lines": [
    {"externalLineId":"1","sku":"SKU-001","quantity":6,"uom":"EA"}
  ]
}
```

## ReceiptConfirmation 1.0

```json
{
  "externalId": "ASN-1001",
  "receiptId": "686a311e-a2bd-4768-ac3f-a74005e69956",
  "warehouseCode": "WH01",
  "completedAt": "2026-07-14T12:40:00Z",
  "lines": [
    {"externalLineId":"1","sku":"SKU-001","receivedQuantity":10,"uom":"EA","discrepancyCode":null}
  ]
}
```

## ShipmentConfirmation 1.0

```json
{
  "externalId": "SO-100245",
  "shipmentId": "03f6dd29-9751-47d5-a1cd-2ab9cefb93e9",
  "warehouseCode": "WH01",
  "dispatchedAt": "2026-07-15T17:20:00Z",
  "lines": [
    {"externalLineId":"1","sku":"SKU-001","shippedQuantity":6,"uom":"EA","shortPickQuantity":0}
  ]
}
```

## Endpoints

| ID | Método y ruta | Entrada/salida | Permiso |
|---|---|---|---|
| API-INT-0001 | `POST /api/v1/integration/asns` | Envelope ASN → 202/200 idempotente | `wms.integration.ingest` |
| API-INT-0002 | `POST /api/v1/integration/sales-orders` | Envelope SalesOrder → 202/200 | `wms.integration.ingest` |
| API-INT-0003 | `GET /api/v1/integration/messages/{messageId}` | estado, intentos y error redactado | `wms.integration.read` |
| API-INT-0004 | `GET /api/v1/integration/correlations/{correlationId}` | timeline E2E | `wms.integration.read` |
| API-INT-0005 | `POST /api/v1/integration/messages/{messageId}/reprocess` | nuevo intento auditado | `wms.integration.reprocess` |
| API-MOB-0001 | `GET /api/v1/mobile/bootstrap` | configuración y checkpoint | usuario móvil |
| API-MOB-0002 | `GET /api/v1/mobile/tasks?since=` | tareas asignadas/versiones | `wms.task.read_assigned` |
| API-MOB-0003 | `POST /api/v1/mobile/commands:batch` | comandos → resultados | `wms.task.execute` |

## Errores

Problem Details RFC 9457 con `type`, `title`, `status`, `code`, `correlationId` y errores de campos. Códigos base: `VALIDATION_FAILED`, `UNAUTHORIZED_SCOPE`, `DUPLICATE_PAYLOAD_MISMATCH`, `SCHEMA_UNSUPPORTED`, `RESOURCE_CONFLICT`, `RATE_LIMITED` y `DEPENDENCY_UNAVAILABLE`. No se devuelve stack trace ni secreto.

## Webhook saliente

`ReceiptConfirmation` y `ShipmentConfirmation` se envían al endpoint configurado. Headers: `WMS-Message-Id`, `WMS-Timestamp`, `WMS-Signature: v1=<HMAC-SHA256(timestamp + "." + rawBody)>`. Tolerancia de reloj 5 min, secreto rotativo y replay rechazado por ID. HTTP 2xx confirma; 408/429/5xx reintentan; 4xx no transitorio pasa a revisión/DLQ.
