# ADR-0006 — Identidad y autorización

- **Fecha:** 2026-07-13
- **Estado:** Aceptado
- **Responsable:** Seguridad

## Contexto

Usuarios web, operarios y dispositivos requieren SSO/MFA y permisos que combinan rol con alcance operativo.

## Decisión

Keycloak mediante OAuth 2.1/OIDC, Authorization Code + PKCE, MFA, access tokens cortos y refresh tokens rotativos. La aplicación aplica RBAC más scopes/atributos de tenant, almacén, propietario, zona y operación. Las acciones críticas exigen autorización en servidor y auditoría.

## Alternativas

IAM propio aumenta riesgo; RBAC puro genera explosión de roles; API keys de usuario carecen de sesión/revocación adecuada.

## Consecuencias y riesgos

Dependencia operativa de Keycloak y sincronización de roles. Se mitiga con caché corta fail-closed, health checks, revocación, sesiones por dispositivo y pruebas de IDOR/escalada. Ningún claim del cliente se confía sin validación criptográfica y de issuer/audience.
