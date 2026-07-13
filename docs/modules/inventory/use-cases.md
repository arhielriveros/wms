# Casos de uso — Inventario

## UC-INV-0001 — aplicar un movimiento o reserva

**Actor:** módulo operativo autorizado. **Precondiciones:** tenant activo, identidad válida, scope y permiso PERM-INV-0001. **Disparador:** solicitud en /api/v1/inventory o comando interno autorizado.

Flujo: validar envelope y versión → verificar idempotencia y estado → aplicar RULE-INV-0001 en transacción → auditar → publicar EVENT-INV-0001 por Outbox → devolver versión resultante.

Alternativas: entrada inválida = 400; sin permiso = 403; no encontrado = 404; duplicado = resultado previo; versión obsoleta = 409; dependencia temporal = 503 sin efecto parcial.

**Resultado:** StockDimension consistente, trazable por `CorrelationId` y observable mediante TEST-INV-0001.
