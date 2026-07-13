# Casos de uso — Maestros

## UC-MST-0001 — crear o actualizar un SKU

**Actor:** maestro de datos. **Precondiciones:** tenant activo, identidad válida, scope y permiso PERM-MST-0001. **Disparador:** solicitud en /api/v1/master-data/skus o comando interno autorizado.

Flujo: validar envelope y versión → verificar idempotencia y estado → aplicar RULE-MST-0001 en transacción → auditar → publicar EVENT-MST-0001 por Outbox → devolver versión resultante.

Alternativas: entrada inválida = 400; sin permiso = 403; no encontrado = 404; duplicado = resultado previo; versión obsoleta = 409; dependencia temporal = 503 sin efecto parcial.

**Resultado:** Sku consistente, trazable por `CorrelationId` y observable mediante TEST-MST-0001.
