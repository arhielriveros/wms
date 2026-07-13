# Flujo UX — Layout

1. La pantalla carga contexto y permisos; muestra skeleton, no datos de otro scope.
2. El usuario inicia UC-LAY-0001; escaneo o formulario valida en el dispositivo y nuevamente en API.
3. Se confirma la acción irreversible con resumen y se muestra estado/correlación.
4. Error recuperable conserva entrada; conflicto obliga a refrescar o enviar a revisión.

Estados obligatorios: loading, empty, error, offline, conflict, permission-denied, success y warning. Web cumple WCAG 2.2 AA; móvil usa controles grandes, alto contraste, sonido/vibración y operación con guantes. Ningún mensaje revela existencia cross-tenant.
