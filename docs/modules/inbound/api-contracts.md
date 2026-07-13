# Contratos API — Inbound

Base `/api/v1/inbound`. El ASN canónico se ingiere por API-INT-0001; estas rutas operan el snapshot WMS. Los comandos offline usan API-MOB-0003.

| ID | Operación | Contrato |
|---|---|---|
| API-INB-0001 | `GET /api/v1/inbound/asns/{asnId}` | Cabecera, líneas, recibido, ubicado, discrepancias, tareas y versión. |
| API-INB-0002 | `POST /api/v1/inbound/asns/{asnId}/release-receiving` | Crea/asigna tareas de recepción; supervisor, idempotente. |
| API-INB-0003 | `POST /api/v1/inbound/asns/{asnId}/receive` | Fallback online: tarea, staging, SKU, cantidad, UOM, versión → movimiento a staging. |
| API-INB-0004 | `POST /api/v1/inbound/receipts/{receiptId}/release-putaway` | Genera tareas dirigidas a destinos activos. |
| API-INB-0005 | `POST /api/v1/inbound/receipts/{receiptId}/complete` | Cierra sólo sin tareas abiertas; publica ReceiptCompleted.v1. |

Mutaciones requieren `wms.receipt.execute` o `wms.putaway.execute`, `Idempotency-Key`, correlación y versión. Conflictos usan `ASN_STATE_CONFLICT`, `OVER_RECEIPT_REQUIRES_REVIEW` o `STOCK_VERSION_CONFLICT`.
