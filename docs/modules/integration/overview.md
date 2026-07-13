# Integración

## Propósito

desacoplar contratos canónicos ERP con idempotencia, outbox/inbox y webhooks. Este dossier implementa FEATURE-INT-0001 para el MVP.

## Alcance MVP

- Caso principal: UC-INT-0001, donde ERP o worker de integración puede ingerir o entregar un mensaje canónico.
- Autoridad: el WMS controla el estado operativo; fuentes externas sólo ingresan por contratos de Integración.
- Dependencias: Plataforma, Tenancy, Seguridad y RabbitMQ.
- Fuera de alcance: optimización avanzada, extensiones de Fase 2 y escritura directa entre schemas.

## Invariantes

- `TenantId`, `CorrelationId` y actor son obligatorios.
- RULE-INT-0001 y sus pruebas bloquean el release si fallan.
- Correlación por MessageId/CorrelationId; webhook firmado y reintentable.
