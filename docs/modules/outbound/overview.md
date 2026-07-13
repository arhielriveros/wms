# Outbound

## Propósito

convertir un pedido en reserva, picking, packing, despacho y confirmación ERP. Este dossier implementa FEATURE-OUT-0001 para el MVP.

## Alcance MVP

- Caso principal: UC-OUT-0001, donde picker, packer y despachante puede preparar y despachar un pedido.
- Autoridad: el WMS controla el estado operativo; fuentes externas sólo ingresan por contratos de Integración.
- Dependencias: Maestros, Inventario, Tareas, Integración y Mobile Sync.
- Fuera de alcance: optimización avanzada, extensiones de Fase 2 y escritura directa entre schemas.

## Invariantes

- `TenantId`, `CorrelationId` y actor son obligatorios.
- RULE-OUT-0001 y sus pruebas bloquean el release si fallan.
- FIFO simple; picking offline asignado, packing y despacho sólo online.
