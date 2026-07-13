# Casos de uso — Outbound

## UC-OUT-0001 — preparar y despachar un pedido

**Actor:** picker, packer y despachante. **Precondiciones:** tenant activo, identidad válida, scope y permiso PERM-OUT-0001. **Disparador:** solicitud en /api/v1/outbound/orders o comando interno autorizado.

Flujo: validar envelope y versión → verificar idempotencia y estado → aplicar RULE-OUT-0001 en transacción → auditar → publicar EVENT-OUT-0001 por Outbox → devolver versión resultante.

Alternativas: entrada inválida = 400; sin permiso = 403; no encontrado = 404; duplicado = resultado previo; versión obsoleta = 409; dependencia temporal = 503 sin efecto parcial.

**Resultado:** SalesOrder consistente, trazable por `CorrelationId` y observable mediante TEST-OUT-0001.
