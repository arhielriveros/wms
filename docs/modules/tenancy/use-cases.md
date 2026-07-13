# Casos de uso — Tenancy

## UC-TEN-0001 — provisionar y resolver un tenant

**Actor:** administrador de plataforma. **Precondiciones:** tenant activo, identidad válida, scope y permiso PERM-TEN-0001. **Disparador:** solicitud en /api/v1/tenants o comando interno autorizado.

Flujo: validar envelope y versión → verificar idempotencia y estado → aplicar RULE-TEN-0001 en transacción → auditar → publicar EVENT-TEN-0001 por Outbox → devolver versión resultante.

Alternativas: entrada inválida = 400; sin permiso = 403; no encontrado = 404; duplicado = resultado previo; versión obsoleta = 409; dependencia temporal = 503 sin efecto parcial.

**Resultado:** Tenant consistente, trazable por `CorrelationId` y observable mediante TEST-TEN-0001.
