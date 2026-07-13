# Matriz de trazabilidad de requisitos

`TBD` indica enlace que sólo existe tras implementación; no se considera evidencia ni autoriza marcar “Terminado”.

| Epic | Feature | Story | Use case | Regla | API | Evento | Entidad | Permiso/control | Test | Commit | Release | Estado |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| EPIC-WMS-0001 | FEATURE-PLT-0001 | STORY-PLT-0001 | UC-PLT-0001 | RULE-PLT-0001 | API-MOB-0001 | EVENT-PLT-0001 | Tenant | tenant scope/RLS | TEST-SEC-0001 | TBD | TBD | Documentado |
| EPIC-WMS-0001 | FEATURE-INB-0001 | STORY-INB-0001 | UC-INB-0001 | RULE-INB-0001 | API-INT-0001 | EVENT-INB-0001 | ASN/Receipt | wms.receipt.execute | TEST-INB-0001 | TBD | TBD | Documentado |
| EPIC-WMS-0001 | FEATURE-INB-0002 | STORY-INB-0002 | UC-INB-0002 | RULE-INV-0001 | API-MOB-0003 | EVENT-INB-0002 | Task/Movement | wms.putaway.execute | TEST-MOB-0001 | TBD | TBD | Documentado |
| EPIC-WMS-0001 | FEATURE-INV-0001 | STORY-INV-0001 | UC-INV-0001 | RULE-INV-0001 | API-MOB-0003 | EVENT-INV-0001 | Movement/Balance | lock+version | TEST-INV-0001 | TBD | TBD | Documentado |
| EPIC-WMS-0001 | FEATURE-OUT-0001 | STORY-OUT-0001 | UC-OUT-0001 | RULE-OUT-0001 | API-INT-0002 | EVENT-OUT-0001 | SalesOrder/Reservation | wms.pick.execute | TEST-OUT-0001 | TBD | TBD | Documentado |
| EPIC-WMS-0001 | FEATURE-OUT-0002 | STORY-OUT-0002 | UC-OUT-0002 | RULE-OUT-0002 | API-MOB-0003 | EVENT-OUT-0002 | PickTask | wms.short_pick.approve | TEST-OUT-0002 | TBD | TBD | Documentado |
| EPIC-WMS-0001 | FEATURE-INT-0001 | STORY-INT-0001 | UC-INT-0001 | RULE-INT-0001 | API-INT-0003 | EVENT-INB-0001 | Inbox/Outbox | wms.integration.read | TEST-INT-0001 | TBD | TBD | Documentado |
| EPIC-WMS-0001 | FEATURE-MOB-0001 | STORY-MOB-0001 | UC-MOB-0001 | RULE-MOB-0001 | API-MOB-0003 | EVENT-MOB-0001 | MobileCommand | assigned task scope | TEST-MOB-0002 | TBD | TBD | Documentado |
| EPIC-WMS-0001 | FEATURE-SEC-0001 | STORY-SEC-0001 | UC-SEC-0001 | RULE-SEC-0001 | todas | EVENT-SEC-0001 | AuditRecord | RBAC+scopes | TEST-SEC-0001 | TBD | TBD | Documentado |
| EPIC-WMS-0001 | FEATURE-OPS-0001 | STORY-OPS-0001 | UC-OPS-0001 | RULE-OPS-0001 | health/readiness | eventos técnicos | Backup | ops privileged | TEST-OPS-0001 | TBD | TBD | Documentado |

## Reglas normativas iniciales

- `RULE-PLT-0001`: toda operación multi-tenant requiere contexto validado y RLS.
- `RULE-INV-0001`: todo cambio de cantidad/estado genera movimiento append-only y saldo consistente.
- `RULE-INB-0001`: una recepción idempotente produce como máximo un efecto por MessageId.
- `RULE-OUT-0001`: reservas concurrentes nunca superan disponibilidad.
- `RULE-OUT-0002`: short pick requiere motivo y política/aprobación.
- `RULE-INT-0001`: un fallo externo no revierte un hecho físico confirmado.
- `RULE-MOB-0001`: conflicto offline no se resuelve por last-write-wins.
- `RULE-SEC-0001`: autorización efectiva combina rol y scopes contextuales.
- `RULE-OPS-0001`: recuperación sólo se declara cuando integridad y smoke están verificados.

La matriz se actualiza en cada PR y release. CI validará formato/referencias; el owner funcional valida semántica.
