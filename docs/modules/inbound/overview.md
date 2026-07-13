# Inbound

## Propósito

convertir un ASN en recepción, putaway y confirmación ERP trazables. Este dossier implementa FEATURE-INB-0001 para el MVP.

## Alcance MVP

- Caso principal: UC-INB-0001, donde receptor y montacarguista puede recibir y ubicar mercadería.
- Autoridad: el WMS controla el estado operativo; fuentes externas sólo ingresan por contratos de Integración.
- Dependencias: Layout, Maestros, Inventario, Tareas, Integración y Mobile Sync.
- Fuera de alcance: optimización avanzada, extensiones de Fase 2 y escritura directa entre schemas.

## Invariantes

- `TenantId`, `CorrelationId` y actor son obligatorios.
- RULE-INB-0001 y sus pruebas bloquean el release si fallan.
- Recepción y putaway soportan comandos offline previamente asignados.
