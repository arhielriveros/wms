# Plataforma

## Propósito

proveer capacidades transversales sin convertirse en un dominio de negocio. Este dossier implementa FEATURE-PLT-0001 para el MVP.

## Alcance MVP

- Caso principal: UC-PLT-0001, donde servicio u operador autorizado puede ejecutar una operación transversal.
- Autoridad: el WMS controla el estado operativo; fuentes externas sólo ingresan por contratos de Integración.
- Dependencias: ninguna; es base del monolito modular.
- Fuera de alcance: optimización avanzada, extensiones de Fase 2 y escritura directa entre schemas.

## Invariantes

- `TenantId`, `CorrelationId` y actor son obligatorios.
- RULE-PLT-0001 y sus pruebas bloquean el release si fallan.
- Health checks, errores RFC 7807, correlación, reloj UTC y feature flags.
