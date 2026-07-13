# Contratos API — Inventario

Base `/api/v1/inventory`. No existe endpoint público para “setear stock”; las mutaciones proceden de comandos de Inbound/Outbound autorizados.

| ID | Operación | Contrato |
|---|---|---|
| API-INV-0001 | `GET /api/v1/inventory/stock?warehouseId=&ownerId=&sku=&locationId=&status=` | Saldos `onHand,reserved,blocked,available,version`; `wms.inventory.read`. |
| API-INV-0002 | `GET /api/v1/inventory/movements?stockDimensionId=&cursor=&limit=` | Ledger append-only ordenado por `occurredAt,movementId`, máximo 200. |
| API-INV-0003 | `POST /api/v1/inventory/reservations` | `orderId,lineId,sku,quantity,uom,expectedVersion` → 201 o 409. |
| API-INV-0004 | `POST /api/v1/inventory/reservations/{reservationId}/release` | Motivo, cantidad y versión → saldo actualizado, idempotente. |
| API-INV-0005 | `GET /api/v1/inventory/reservations/{reservationId}` | Reserva, consumo/liberación y versión; tenant-scoped. |

Comandos requieren `Idempotency-Key` y `X-Correlation-Id`. Errores: `INSUFFICIENT_AVAILABLE`, `STALE_STOCK_VERSION`, `RESERVATION_NOT_ACTIVE`; nunca devuelven efecto parcial.
