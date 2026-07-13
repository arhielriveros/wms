# Observabilidad — Inbound

Propagar `traceparent` y `CorrelationId` por API, transacción, Outbox y worker. Logs estructurados incluyen tenant seudonimizado, módulo, operación, resultado y latencia; excluyen tokens, payloads sensibles y barcodes completos cuando no sean necesarios.

Métricas: dock-to-stock, exactitud y líneas con excepción; tasa de errores por código, reintentos, DLQ y duración de transacción. Trazas marcan espera de DB/broker y llamadas externas.

Alertas: error sostenido > 2%, DLQ > 0, backlog fuera de SLO o p95 > 500 ms durante 10 min. Dashboard enlaza runbook y permite filtrar por tenant/correlation sin mezclar datos.
