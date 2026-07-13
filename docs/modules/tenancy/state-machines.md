# Máquina de estados — Tenancy

Ruta nominal: **Provisioning → Active → Suspended → Archived**.

- Cada transición requiere estado origen, comando, permiso PERM-TEN-0001 y versión esperada.
- Repetir el mismo `CommandId` conserva la primera respuesta.
- Estado terminal no admite mutación salvo comando de compensación documentado.
- Fallos externos dejan pendiente la entrega; no revierten una operación física ya confirmada.
- Transición inválida: HTTP 409, código `TEN_INVALID_TRANSITION`, evento no emitido.

TEST-TEN-0001 cubre ruta nominal; TEST-TEN-0003 cubre transición inválida y carrera concurrente.
