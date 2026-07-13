# Reglas de negocio — Task Execution

| ID | Regla | Verificación |
|---|---|---|
| RULE-TSK-0001 | Sólo existen tareas MVP de Receipt, Putaway y Pick, creadas por el módulo dueño con referencia, pasos, ubicación/SKU/cantidad y versión. | TEST-TSK-0001 |
| RULE-TSK-0002 | Una tarea tiene como máximo una asignación activa; usuario, dispositivo, tenant, almacén y zona deben coincidir con sus scopes. | TEST-TSK-0004 |
| RULE-TSK-0003 | Un operario sólo descarga/ejecuta tareas asignadas, no expiradas y no canceladas; supervisor puede reasignar con motivo y nueva versión. | TEST-TSK-0003 |
| RULE-TSK-0004 | Start, paso, excepción y complete respetan versión e idempotencia; completar exige todos los pasos y evidencia requeridos. | TEST-TSK-0002 |
| RULE-TSK-0005 | Task Execution no escribe stock ni documentos: delega el comando al módulo dueño y cambia estado sólo si éste confirma el efecto. | TEST-TSK-0005 |
| RULE-TSK-0006 | Un conflicto pausa la tarea en Exception; cancelación/reasignación nunca deshace movimientos físicos ya confirmados. | TEST-TSK-0003 |

El orden offline se gobierna por `taskId/localSequence`; el fallo de una tarea no bloquea otras tareas del mismo batch.
