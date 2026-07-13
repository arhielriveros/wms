# Máquina de estados — Seguridad y Auditoría

Ruta nominal: **Requested → Allowed | Denied → Audited**.

- Cada transición requiere estado origen, comando, permiso PERM-SEC-0001 y versión esperada.
- Repetir el mismo `CommandId` conserva la primera respuesta.
- Estado terminal no admite mutación salvo comando de compensación documentado.
- Fallos externos dejan pendiente la entrega; no revierten una operación física ya confirmada.
- Transición inválida: HTTP 409, código `SEC_INVALID_TRANSITION`, evento no emitido.

TEST-SEC-0001 cubre ruta nominal; TEST-SEC-0003 cubre transición inválida y carrera concurrente.
