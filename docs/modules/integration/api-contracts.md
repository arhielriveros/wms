# Contratos API — Integración

Adopta exactamente `docs/integration/canonical-contracts.md`; envelope canónico y permiso por operación son obligatorios.

| ID | Operación | Entrada/salida |
|---|---|---|
| API-INT-0001 | `POST /api/v1/integration/asns` | Envelope AdvanceShippingNotice 1.0 → 202 nuevo / 200 duplicado idéntico. |
| API-INT-0002 | `POST /api/v1/integration/sales-orders` | Envelope SalesOrder 1.0 → 202/200 idempotente. |
| API-INT-0003 | `GET /api/v1/integration/messages/{messageId}` | Estado de Inbox/Outbox, intentos y error redactado. |
| API-INT-0004 | `GET /api/v1/integration/correlations/{correlationId}` | Timeline ASN/pedido, operación, confirmación y webhook. |
| API-INT-0005 | `POST /api/v1/integration/messages/{messageId}/reprocess` | Nuevo intento auditado; conserva MessageId y requiere motivo. |

Ingesta requiere `wms.integration.ingest`; lectura `wms.integration.read`; reproceso `wms.integration.reprocess`. `DUPLICATE_PAYLOAD_MISMATCH`, `SCHEMA_UNSUPPORTED` y `DEPENDENCY_UNAVAILABLE` siguen Problem Details RFC 9457.
