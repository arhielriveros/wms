# Máquina de estados — Inventario

Ruta nominal: **Available ↔ Reserved → Picked → Shipped; Blocked es ortogonal**.

- Cada transición requiere estado origen, comando, permiso PERM-INV-0001 y versión esperada.
- Repetir el mismo `CommandId` conserva la primera respuesta.
- Estado terminal no admite mutación salvo comando de compensación documentado.
- Fallos externos dejan pendiente la entrega; no revierten una operación física ya confirmada.
- Transición inválida: HTTP 409, código `INV_INVALID_TRANSITION`, evento no emitido.

TEST-INV-0001 cubre ruta nominal; TEST-INV-0003 cubre transición inválida y carrera concurrente.
