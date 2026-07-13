# Layout

## Propósito

modelar almacenes, zonas y ubicaciones operables. Este dossier implementa FEATURE-LAY-0001 para el MVP.

## Alcance MVP

- Caso principal: UC-LAY-0001, donde administrador de almacén puede configurar una ubicación.
- Autoridad: el WMS controla el estado operativo; fuentes externas sólo ingresan por contratos de Integración.
- Dependencias: Tenancy y Seguridad.
- Fuera de alcance: optimización avanzada, extensiones de Fase 2 y escritura directa entre schemas.

## Invariantes

- `TenantId`, `CorrelationId` y actor son obligatorios.
- RULE-LAY-0001 y sus pruebas bloquean el release si fallan.
- Códigos de ubicación únicos por almacén; staging es una zona explícita.
