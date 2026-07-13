# Máquina de estados — Layout

Ruta nominal: **Draft → Active → Blocked → Retired**.

- Cada transición requiere estado origen, comando, permiso PERM-LAY-0001 y versión esperada.
- Repetir el mismo `CommandId` conserva la primera respuesta.
- Estado terminal no admite mutación salvo comando de compensación documentado.
- Fallos externos dejan pendiente la entrega; no revierten una operación física ya confirmada.
- Transición inválida: HTTP 409, código `LAY_INVALID_TRANSITION`, evento no emitido.

TEST-LAY-0001 cubre ruta nominal; TEST-LAY-0003 cubre transición inválida y carrera concurrente.
