# Casos de uso — Plataforma

## UC-PLT-0001 — ejecutar una operación transversal

**Actor:** servicio u operador autorizado. **Precondiciones:** tenant activo, identidad válida, scope y permiso PERM-PLT-0001. **Disparador:** solicitud en /api/v1/platform o comando interno autorizado.

Flujo: validar envelope y versión → verificar idempotencia y estado → aplicar RULE-PLT-0001 en transacción → auditar → publicar EVENT-PLT-0001 por Outbox → devolver versión resultante.

Alternativas: entrada inválida = 400; sin permiso = 403; no encontrado = 404; duplicado = resultado previo; versión obsoleta = 409; dependencia temporal = 503 sin efecto parcial.

**Resultado:** RequestContext consistente, trazable por `CorrelationId` y observable mediante TEST-PLT-0001.
