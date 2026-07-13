# Contratos API — Outbound

Base `/api/v1/outbound`. El pedido canónico se ingiere por API-INT-0002; picking offline usa API-MOB-0003.

| ID | Operación | Contrato |
|---|---|---|
| API-OUT-0001 | `GET /api/v1/outbound/orders/by-external/{externalId}` | Pedido, líneas, cantidades ordenadas/asignadas/pickeadas, short pick, estado y versión. |
| API-OUT-0002 | `POST /api/v1/outbound/orders/{orderId}/release` | Liberación manual: reserva FIFO y crea tareas de pick por dimensión reservada. |
| API-OUT-0003 | `POST /api/v1/outbound/orders/{orderId}/short-picks/{lineId}/decision` | Supervisor aprueba/rechaza cantidad corta con motivo, comando móvil y versión de tarea. |
| API-OUT-0004 | `POST /api/v1/outbound/orders/{orderId}/pack` | Sólo online; exige cierre de cada línea por pick o short pick aprobado. |
| API-OUT-0005 | `POST /api/v1/outbound/orders/{orderId}/dispatch` | Sólo online; registra despacho y publica `ShipmentConfirmation` por Outbox. |

`release` recibe `commandId`, `entityVersion`, `assigneeId` y `deviceId`. `pack`/`dispatch` reciben `commandId` y `entityVersion`. La decisión de short pick recibe `commandId`, `mobileCommandId`, `taskId`, `taskEntityVersion`, `actualQuantity`, `reason` y `approve`.

Permisos: `wms.outbound.read`, `wms.outbound.release`, `wms.outbound.pack` y `wms.outbound.dispatch`. Errores tipados: `INSUFFICIENT_AVAILABLE`, `SHORT_PICK_REASON_REQUIRED`, `SHORT_PICK_QUANTITY_INVALID`, `PICK_NOT_COMPLETE`, `OUT_INVALID_TRANSITION` y `ORDER_STATE_CONFLICT`.
