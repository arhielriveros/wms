# Máquina de estados — Outbound

Ruta nominal: **Imported → Allocated → Released → Picking → Packed → Shipped | Exception**.

- Cada transición requiere estado origen, comando, permiso PERM-OUT-0001 y versión esperada.
- Repetir el mismo `CommandId` conserva la primera respuesta.
- Estado terminal no admite mutación salvo comando de compensación documentado.
- Fallos externos dejan pendiente la entrega; no revierten una operación física ya confirmada.
- Transición inválida: HTTP 409, código `OUT_INVALID_TRANSITION`, evento no emitido.

TEST-OUT-0001 cubre ruta nominal; TEST-OUT-0003 cubre transición inválida y carrera concurrente.
