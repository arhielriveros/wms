# Máquina de estados — Task Execution

Ruta nominal: **Created → Assigned → InProgress → Completed | Cancelled | Exception**.

- Cada transición requiere estado origen, comando, permiso PERM-TSK-0001 y versión esperada.
- Repetir el mismo `CommandId` conserva la primera respuesta.
- Estado terminal no admite mutación salvo comando de compensación documentado.
- Fallos externos dejan pendiente la entrega; no revierten una operación física ya confirmada.
- Transición inválida: HTTP 409, código `TSK_INVALID_TRANSITION`, evento no emitido.

TEST-TSK-0001 cubre ruta nominal; TEST-TSK-0003 cubre transición inválida y carrera concurrente.
