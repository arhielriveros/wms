# Seguridad y Auditoría

## Propósito

autenticar, autorizar y registrar evidencia inmutable de acciones relevantes. Este dossier implementa FEATURE-SEC-0001 para el MVP.

## Alcance MVP

- Caso principal: UC-SEC-0001, donde usuario, dispositivo o servicio puede acceder a una operación protegida.
- Autoridad: el WMS controla el estado operativo; fuentes externas sólo ingresan por contratos de Integración.
- Dependencias: Plataforma, Tenancy y Keycloak.
- Fuera de alcance: optimización avanzada, extensiones de Fase 2 y escritura directa entre schemas.

## Invariantes

- `TenantId`, `CorrelationId` y actor son obligatorios.
- RULE-SEC-0001 y sus pruebas bloquean el release si fallan.
- OIDC, MFA, RBAC y scopes por tenant/warehouse/owner/zone/operation.
