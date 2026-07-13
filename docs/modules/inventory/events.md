# Eventos — Inventario

## EVENT-INV-0001

Envelope obligatorio: `MessageId`, `MessageType=EVENT-INV-0001`, `SchemaVersion=1`, `TenantId`, `OccurredAt`, `SourceSystem=WMS`, `CorrelationId`, `CausationId`, `Payload`.

El payload contiene ID del agregado, estado, versión y datos mínimos; nunca secretos ni PII innecesaria. Se persiste en Outbox en la misma transacción y se publica al exchange del módulo. Consumidores usan Inbox por `MessageId`.

EVENT-INV-0002 representa rechazo/excepción sin exponer datos sensibles. Reintentos exponenciales terminan en DLQ y alerta; la evolución mantiene compatibilidad hacia atrás.
