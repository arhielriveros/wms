# Máquina de estados — Mobile Sync

Ruta nominal: **Queued → Received → Accepted | Conflict | Rejected | RequiresReview**.

- Cada transición requiere estado origen, comando, permiso PERM-SYN-0001 y versión esperada.
- Repetir el mismo `CommandId` conserva la primera respuesta.
- Estado terminal no admite mutación salvo comando de compensación documentado.
- Fallos externos dejan pendiente la entrega; no revierten una operación física ya confirmada.
- Transición inválida: HTTP 409, código `SYN_INVALID_TRANSITION`, evento no emitido.

TEST-SYN-0001 cubre ruta nominal; TEST-SYN-0003 cubre transición inválida y carrera concurrente.
