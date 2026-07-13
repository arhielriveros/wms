# Contratos API — Task Execution

Base `/api/v1/tasks`. Las lecturas móviles de tareas asignadas se exponen únicamente por API-MOB-0002.

| ID | Operación | Contrato |
|---|---|---|
| API-TSK-0001 | `GET /api/v1/tasks/{taskId}` | Tipo, referencia, pasos, asignación, expiración, estado y versión. |
| API-TSK-0002 | `GET /api/v1/tasks?warehouseId=&zoneId=&status=&assigneeId=&cursor=` | Cola supervisora tenant-scoped. |
| API-TSK-0003 | `POST /api/v1/tasks/{taskId}/assign` | Usuario/dispositivo/zona, expiración y versión; `wms.task.reassign`. |
| API-TSK-0004 | `POST /api/v1/tasks/{taskId}/start` | Sólo asignado vigente; online o API-MOB-0003. |
| API-TSK-0005 | `POST /api/v1/tasks/{taskId}/complete` | Pasos/evidencia completos; delega la mutación física al módulo owner. |
| API-TSK-0006 | `POST /api/v1/tasks/{taskId}/exception` | Código, evidencia y acción solicitada; pausa para supervisor. |
| API-TSK-0007 | `POST /api/v1/tasks/{taskId}/cancel` | Supervisor, motivo obligatorio; no deshace movimientos confirmados. |

Toda mutación exige versión, correlación e idempotencia. `TASK_NOT_ASSIGNED`, `TASK_EXPIRED` y `TASK_VERSION_CONFLICT` retornan 409 sin efecto.
