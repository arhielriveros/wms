# Observabilidad y SLO

## Propagación

OpenTelemetry instrumenta web/mobile → API → dominio → PostgreSQL → Outbox → RabbitMQ → Worker → ERP. W3C Trace Context es preferido; `CorrelationId` funcional y `CausationId` se preservan en mensajes y auditoría. Serilog emite JSON estructurado.

## Campos mínimos de log

Timestamp UTC, nivel, servicio/versión/ambiente, trace/span, CorrelationId, CausationId, tenant, almacén, actor/dispositivo (seudonimizado), operación, entidad/ID, resultado, latencia, error tipado. Nunca token, secreto, firma o payload sensible completo.

## Métricas y SLI

| Área | Métrica/SLI |
|---|---|
| API | requests, p50/p95/p99, tasa 5xx/429, disponibilidad |
| Inventario | movimientos/s, conflicto, lock wait, invariant violation |
| Tareas/mobile | asignadas/completadas, edad, sync lag, batch size, offline devices |
| Integración | throughput, retry, latencia, Outbox age, DLQ, acuse ERP |
| Infra | pool DB, CPU/memoria/disco, conexiones, queue depth, cache hit |

SLO piloto: 99,5 % mensual para operaciones interactivas; p95 <500 ms; batch 100 comandos <10 s; RPO 5 min; RTO 60 min. Ventanas planificadas se reportan por separado, no se ocultan.

## Alertas

- **Crítica:** invariante de stock, aislamiento tenant, backup fallido sin reemplazo, despacho bloqueado global.
- **Alta:** ERP inaccesible >15 min, Outbox oldest >15 min, DLQ >0, error 5xx >5 %/5 min, sync lag >30 min.
- **Media:** p95 >500 ms/15 min, pool/CPU/disco >80 %, certificado <30 días.

Cada alerta incluye impacto, dashboard, CorrelationId de muestra, runbook, owner y condición de cierre. Se controla cardinalidad de tenant/dispositivo y no se usan IDs libres en métricas.

## Dashboards

Operación E2E, integraciones, inventario/concurrencia, dispositivos, PostgreSQL/broker/cache y SLO/error budget. Un deploy se anota con versión, commit, migrations y flags.
