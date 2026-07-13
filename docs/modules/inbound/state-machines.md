# Máquina de estados — Inbound

Ruta nominal: **Imported → Receiving → Received → PutawayInProgress → Completed | Exception**.

- Cada transición requiere estado origen, comando, permiso PERM-INB-0001 y versión esperada.
- Repetir el mismo `CommandId` conserva la primera respuesta.
- Estado terminal no admite mutación salvo comando de compensación documentado.
- Fallos externos dejan pendiente la entrega; no revierten una operación física ya confirmada.
- Transición inválida: HTTP 409, código `INB_INVALID_TRANSITION`, evento no emitido.

TEST-INB-0001 cubre ruta nominal; TEST-INB-0003 cubre transición inválida y carrera concurrente.
