# Máquina de estados — Integración

Ruta nominal: **Received → Accepted | Rejected; Pending → Delivering → Delivered | DeadLetter**.

- Cada transición requiere estado origen, comando, permiso PERM-INT-0001 y versión esperada.
- Repetir el mismo `CommandId` conserva la primera respuesta.
- Estado terminal no admite mutación salvo comando de compensación documentado.
- Fallos externos dejan pendiente la entrega; no revierten una operación física ya confirmada.
- Transición inválida: HTTP 409, código `INT_INVALID_TRANSITION`, evento no emitido.

TEST-INT-0001 cubre ruta nominal; TEST-INT-0003 cubre transición inválida y carrera concurrente.
