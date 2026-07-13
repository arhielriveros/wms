# Drill de observabilidad E2E — 2026-07-13

## Resultado

`TEST-OPS-0004` quedó **APROBADO** en un entorno Docker aislado con API, worker, PostgreSQL, RabbitMQ, Redis, MinIO, OpenTelemetry Collector, Prometheus, Tempo, Loki y Grafana. La ejecución limpia final terminó el 2026-07-13 a las 23:46:07 UTC en 180,77 segundos y no persistió secretos.

## Alcance ejecutado

El comando reproducible es:

```powershell
./scripts/observability-e2e-drill.ps1
```

El script usa puertos altos aislados, aprovisiona RabbitMQ mediante un init one-shot, crea/verifica el bucket privado de MinIO, autentica los probes de RabbitMQ/Redis/Grafana, genera tráfico API y espera señales reales de API/worker. Al finalizar elimina contenedores, redes y volúmenes del proyecto `wms-observability` salvo que se solicite explícitamente `-KeepEnvironment` para diagnóstico.

## Evidencia

| Control | Resultado observado |
|---|---|
| MinIO bucket privado | PASS |
| OpenTelemetry Collector | ready |
| RabbitMQ + `wms.dlq` quorum | PASS |
| Redis autenticado | PONG |
| Grafana + Prometheus/Loki/Tempo + dashboard | provisionados |
| API/worker | healthy |
| Métrica API | `wms_wms_api_requests_total` |
| Métrica worker | `wms_wms_worker_outbox_polls_total` |
| Resultados Tempo | API 12; worker 20 |
| Streams Loki | API 50; worker 48 |
| Secretos persistidos | no |

La evidencia JSON redactada se genera bajo `.backups/wms-observability-*.json` y GitHub Actions la publica como `observability-e2e-evidence` por 30 días.

## Hallazgos corregidos durante el drill

- RabbitMQ 4 no puede importar una topología que referencia `/` antes de crear el vhost; ahora el usuario/vhost se crean desde el entorno y `rabbitmq-init` importa la topología después del health check.
- Las métricas personalizadas usan etiquetas de baja cardinalidad y el collector convierte atributos de recurso en labels de Prometheus.
- El provider OTLP paralelo no recibía eventos después de que Serilog reemplazaba la fábrica; API/worker ahora usan el sink OTLP oficial de Serilog con `service.name` explícito.
- El dashboard dejó de consultar una métrica interna inexistente y muestra tasa API, polls del worker y resultados de entrega.
- El gate espera APIs funcionales de RabbitMQ/Grafana y señales consultables, no sólo procesos o contenedores activos.

## Criterio de aceptación

El subgate local de observabilidad de Hito 4 está cerrado. La certificación del piloto aún requiere repetirlo con TLS, retención, alertas, secretos y capacidades del ambiente objetivo.
