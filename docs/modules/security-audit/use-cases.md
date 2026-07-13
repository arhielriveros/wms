# Casos de uso — Seguridad y Auditoría

## UC-SEC-0001 — acceder a una operación protegida

**Actor:** usuario, dispositivo o servicio. **Precondiciones:** tenant activo, identidad válida, scope y permiso PERM-SEC-0001. **Disparador:** solicitud en /api/v1/audit o comando interno autorizado.

Flujo: validar envelope y versión → verificar idempotencia y estado → aplicar RULE-SEC-0001 en transacción → auditar → publicar EVENT-SEC-0001 por Outbox → devolver versión resultante.

Alternativas: entrada inválida = 400; sin permiso = 403; no encontrado = 404; duplicado = resultado previo; versión obsoleta = 409; dependencia temporal = 503 sin efecto parcial.

**Resultado:** AccessDecision consistente, trazable por `CorrelationId` y observable mediante TEST-SEC-0001.
