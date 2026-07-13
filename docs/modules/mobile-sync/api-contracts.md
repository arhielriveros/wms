# Contratos API — Mobile Sync

Este dossier adopta sin alias las rutas de `docs/integration/canonical-contracts.md`. JSON UTF-8, OAuth bearer y `X-Correlation-Id` son obligatorios.

| ID | Operación | Contrato y permiso |
|---|---|---|
| API-MOB-0001 | `GET /api/v1/mobile/bootstrap` | Configuración autorizada, dispositivo, scopes, catálogos mínimos y `checkpoint`; usuario móvil autenticado. |
| API-MOB-0002 | `GET /api/v1/mobile/tasks?since={checkpoint}` | Tareas asignadas, versiones, expiración y nuevo checkpoint; `wms.task.read_assigned`. |
| API-MOB-0003 | `POST /api/v1/mobile/commands:batch` | Batch de 1..100 comandos offline y un resultado durable por comando; `wms.task.execute`. |

Cada comando incluye `commandId`, `commandType`, `schemaVersion`, `tenantId`, `warehouseId`, `deviceId`, `userId`, `occurredAt`, `localSequence`, `entityVersion`, `taskId` y `payload`. El servidor agrupa por `taskId`, ordena por `localSequence` y no procesa comandos posteriores de esa tarea después de un fallo.

Los resultados permitidos son `Accepted`, `Rejected`, `Conflict`, `AlreadyProcessed`, `RequiresReview`, `Expired` y `Unauthorized`; incluyen `commandId`, código seguro, versión vigente y acción sugerida. Repetir el mismo `commandId` y payload devuelve `AlreadyProcessed`; un payload distinto produce `DUPLICATE_PAYLOAD_MISMATCH`.

Errores de transporte usan Problem Details RFC 9457. Un conflicto de negocio se expresa dentro del resultado del comando y nunca aplica last-write-wins ni ajusta stock automáticamente.
