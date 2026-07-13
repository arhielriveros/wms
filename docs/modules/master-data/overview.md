# Maestros

## Propósito

mantener SKU, unidad simple y códigos de barra canónicos. Este dossier implementa FEATURE-MST-0001 para el MVP.

## Alcance MVP

- Caso principal: UC-MST-0001, donde maestro de datos puede crear o actualizar un SKU.
- Autoridad: el WMS controla el estado operativo; fuentes externas sólo ingresan por contratos de Integración.
- Dependencias: Tenancy y Seguridad.
- Fuera de alcance: optimización avanzada, extensiones de Fase 2 y escritura directa entre schemas.

## Invariantes

- `TenantId`, `CorrelationId` y actor son obligatorios.
- RULE-MST-0001 y sus pruebas bloquean el release si fallan.
- MVP con una UOM base por SKU; lotes, series y GS1 avanzado fuera de alcance.
