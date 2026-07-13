# Casos de uso — Inbound

## UC-INB-0001 — recibir y ubicar mercadería

**Actor:** receptor y montacarguista. **Precondiciones:** tenant activo, identidad válida, scope y permiso PERM-INB-0001. **Disparador:** solicitud en /api/v1/inbound/asns o comando interno autorizado.

Flujo: validar envelope y versión → verificar idempotencia y estado → aplicar RULE-INB-0001 en transacción → auditar → publicar EVENT-INB-0001 por Outbox → devolver versión resultante.

Alternativas: entrada inválida = 400; sin permiso = 403; no encontrado = 404; duplicado = resultado previo; versión obsoleta = 409; dependencia temporal = 503 sin efecto parcial.

**Resultado:** AdvanceShippingNotice consistente, trazable por `CorrelationId` y observable mediante TEST-INB-0001.
