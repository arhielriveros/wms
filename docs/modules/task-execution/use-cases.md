# Casos de uso — Task Execution

## UC-TSK-0001 — asignar o ejecutar una tarea

**Actor:** supervisor y operario. **Precondiciones:** tenant activo, identidad válida, scope y permiso PERM-TSK-0001. **Disparador:** solicitud en /api/v1/tasks o comando interno autorizado.

Flujo: validar envelope y versión → verificar idempotencia y estado → aplicar RULE-TSK-0001 en transacción → auditar → publicar EVENT-TSK-0001 por Outbox → devolver versión resultante.

Alternativas: entrada inválida = 400; sin permiso = 403; no encontrado = 404; duplicado = resultado previo; versión obsoleta = 409; dependencia temporal = 503 sin efecto parcial.

**Resultado:** WarehouseTask consistente, trazable por `CorrelationId` y observable mediante TEST-TSK-0001.
