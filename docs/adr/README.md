# Architecture Decision Records

| ADR | Decisión | Estado |
|---|---|---|
| [ADR-0001](ADR-0001-modular-monolith.md) | Monolito modular distribuible | Aceptado |
| [ADR-0002](ADR-0002-tenancy-and-database-isolation.md) | PostgreSQL compartido, schemas y RLS | Aceptado |
| [ADR-0003](ADR-0003-inventory-consistency.md) | Ledger append-only y saldos transaccionales | Aceptado |
| [ADR-0004](ADR-0004-messaging-and-integration.md) | Outbox/Inbox sobre RabbitMQ | Aceptado |
| [ADR-0005](ADR-0005-mobile-offline-first.md) | Android nativo offline-first | Aceptado |
| [ADR-0006](ADR-0006-identity-and-authorization.md) | Keycloak, RBAC y scopes contextuales | Aceptado |
| [ADR-0007](ADR-0007-evidence-and-observability.md) | S3 y OpenTelemetry desde el inicio | Aceptado |
| [ADR-0008](ADR-0008-deployment-and-recovery.md) | Compose y blue/green sin Kubernetes MVP | Aceptado |

Un ADR aceptado sólo se reemplaza mediante otro ADR que enlace al anterior. Responsable inicial: equipo de arquitectura WMS. Fecha de las decisiones: 2026-07-13.
