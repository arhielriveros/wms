# Casos de uso — Integración

## UC-INT-0001 — ingerir o entregar un mensaje canónico

**Actor:** ERP o worker de integración. **Precondiciones:** tenant activo, identidad válida, scope y permiso PERM-INT-0001. **Disparador:** solicitud en /api/v1/integration o comando interno autorizado.

Flujo: validar envelope y versión → verificar idempotencia y estado → aplicar RULE-INT-0001 en transacción → auditar → publicar EVENT-INT-0001 por Outbox → devolver versión resultante.

Alternativas: entrada inválida = 400; sin permiso = 403; no encontrado = 404; duplicado = resultado previo; versión obsoleta = 409; dependencia temporal = 503 sin efecto parcial.

**Resultado:** IntegrationMessage consistente, trazable por `CorrelationId` y observable mediante TEST-INT-0001.
