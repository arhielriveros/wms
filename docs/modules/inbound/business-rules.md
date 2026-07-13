# Reglas de negocio — Inbound

| ID | Regla | Verificación |
|---|---|---|
| RULE-INB-0001 | El ASN entra exclusivamente como `AdvanceShippingNotice 1.0` por API-INT-0001; `MessageId` repetido e idéntico no duplica documento, tarea ni stock. | TEST-INB-0002 |
| RULE-INB-0002 | El snapshot aceptado conserva `externalId`, almacén, propietario, SKU, UOM y cantidades; cambios ERP posteriores requieren un nuevo mensaje/versionado y revisión de estado. | TEST-INB-0005 |
| RULE-INB-0003 | La recepción exige tarea asignada vigente y escaneo de staging, SKU y cantidad; SKU/UOM deben coincidir y el MVP no acepta sobre-recepción sin revisión de supervisor. | TEST-INB-0001 |
| RULE-INB-0004 | Confirmar recepción publica en Inventario un movimiento a staging y su saldo en una transacción; un conflicto deja la tarea pausada, sin ajuste automático. | TEST-INB-0003 |
| RULE-INB-0005 | Putaway exige tarea asignada, ubicación destino activa/capaz y stock recibido en staging; mueve staging→destino de forma atómica e idempotente. | TEST-INB-0001 |
| RULE-INB-0006 | Un ASN sólo llega a `Completed` cuando todas las líneas aceptadas están recibidas y ubicadas o cerradas con discrepancia aprobada, sin tareas abiertas. | TEST-INB-0003 |
| RULE-INB-0007 | `ReceiptCompleted.v1` genera una única `ReceiptConfirmation 1.0`; un fallo ERP deja el webhook pendiente y nunca revierte recepción o putaway. | TEST-INB-0005 |

Recepción y putaway offline sólo admiten tareas descargadas, asignadas y no expiradas. Toda mutación conserva `TenantId`, actor, `CorrelationId`, `CommandId` y versión esperada.
