# Tenancy

## Propósito

aislar datos y alcance operativo por tenant, almacén y propietario. Este dossier implementa FEATURE-TEN-0001 para el MVP.

## Alcance MVP

- Caso principal: UC-TEN-0001, donde administrador de plataforma puede provisionar y resolver un tenant.
- Autoridad: el WMS controla el estado operativo; fuentes externas sólo ingresan por contratos de Integración.
- Dependencias: Plataforma.
- Fuera de alcance: optimización avanzada, extensiones de Fase 2 y escritura directa entre schemas.

## Invariantes

- `TenantId`, `CorrelationId` y actor son obligatorios.
- RULE-TEN-0001 y sus pruebas bloquean el release si fallan.
- TenantId obligatorio en token, contexto, tablas, eventos y comandos.
