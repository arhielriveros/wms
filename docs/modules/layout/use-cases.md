# Casos de uso — Layout

## UC-LAY-0001 — configurar una ubicación

**Actor:** administrador de almacén. **Precondiciones:** tenant activo, identidad válida, scope y permiso PERM-LAY-0001. **Disparador:** solicitud en /api/v1/layout/locations o comando interno autorizado.

Flujo: validar envelope y versión → verificar idempotencia y estado → aplicar RULE-LAY-0001 en transacción → auditar → publicar EVENT-LAY-0001 por Outbox → devolver versión resultante.

Alternativas: entrada inválida = 400; sin permiso = 403; no encontrado = 404; duplicado = resultado previo; versión obsoleta = 409; dependencia temporal = 503 sin efecto parcial.

**Resultado:** Warehouse consistente, trazable por `CorrelationId` y observable mediante TEST-LAY-0001.
