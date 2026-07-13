# Reglas de negocio — Mobile Sync

| ID | Regla | Verificación |
|---|---|---|
| RULE-SYN-0001 | Bootstrap y tareas se filtran por tenant, almacén, usuario, dispositivo, zona y permisos; cambio de sesión revoca y elimina datos locales anteriores. | TEST-SYN-0004 |
| RULE-SYN-0002 | El cliente persiste el comando antes de mostrar “guardado local”; cada batch contiene 1..100 comandos con envelope 1.0 completo. | TEST-SYN-0001 |
| RULE-SYN-0003 | El servidor agrupa por `taskId` y procesa `localSequence` ascendente; un fallo bloquea posteriores de esa tarea, no otras. | TEST-SYN-0003 |
| RULE-SYN-0004 | `commandId` igual/payload igual devuelve AlreadyProcessed; payload distinto devuelve Rejected/DUPLICATE_PAYLOAD_MISMATCH sin segundo efecto. | TEST-SYN-0002 |
| RULE-SYN-0005 | Resultados válidos: Accepted, Rejected, Conflict, AlreadyProcessed, RequiresReview, Expired y Unauthorized; todos son durables antes de avanzar checkpoint. | TEST-SYN-0005 |
| RULE-SYN-0006 | Recepción, putaway y pick offline requieren tarea descargada/asignada/no expirada; packing y despacho siempre requieren conexión. | TEST-SYN-0001 |
| RULE-SYN-0007 | El servidor es autoridad: conflicto congela la tarea para revisión y nunca aplica last-write-wins ni ajusta stock automáticamente. | TEST-SYN-0003 |

WorkManager reintenta sólo fallos de transporte. Un resultado de negocio no se transforma localmente en éxito.
