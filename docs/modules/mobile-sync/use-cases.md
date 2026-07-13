# Casos de uso — Mobile Sync

## UC-SYN-0001 — descargar tareas o enviar un batch

**Actor:** dispositivo Android registrado. **Precondiciones:** tenant activo, identidad válida, scope y permiso PERM-SYN-0001. **Disparador:** solicitud en /api/v1/mobile-sync o comando interno autorizado.

Flujo: validar envelope y versión → verificar idempotencia y estado → aplicar RULE-SYN-0001 en transacción → auditar → publicar EVENT-SYN-0001 por Outbox → devolver versión resultante.

Alternativas: entrada inválida = 400; sin permiso = 403; no encontrado = 404; duplicado = resultado previo; versión obsoleta = 409; dependencia temporal = 503 sin efecto parcial.

**Resultado:** SyncSession consistente, trazable por `CorrelationId` y observable mediante TEST-SYN-0001.
