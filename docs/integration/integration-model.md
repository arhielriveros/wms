# Modelo operativo de integración

## Flujo de entrada

1. Autenticar sistema y resolver tenant.
2. Validar envelope/schema, tamaño, scopes y códigos.
3. Inbox reserva `MessageId`; duplicado igual retorna `AlreadyProcessed`, diferente retorna conflicto.
4. Mapear modelo canónico al comando público del módulo.
5. Confirmar Inbox, efecto y Outbox localmente.
6. Publicar evento; telemetría conserva correlación y causalidad.

## Flujo de salida

Evento interno → Outbox → Worker → adaptador REST → webhook firmado → acuse. El estado progresa `Pending → Delivering → Delivered`; errores transitorios `RetryScheduled`; agotamiento `DeadLettered`; intervención `RequiresReview → Reprocessed/Cancelled`. “Cancelled” cancela la entrega, no el hecho físico.

## Política de retry y DLQ

Backoff exponencial con jitter: 1 min, 5 min, 15 min, 1 h, 6 h; máximo 8 intentos/24 h, configurable. `Retry-After` válido se respeta. DLQ conserva mensaje cifrado, error tipado, intentos y checksum. Reproceso genera auditoría y nuevo attempt, preservando `MessageId`.

## Reconciliación

Job diario y bajo demanda compara documentos aceptados, confirmaciones y acuses por tenant/sistema/ventana. Produce diferencias `MissingInbound`, `MissingConfirmation`, `PayloadMismatch`, `StalePending` o `AuthorityConflict`. La resolución crea comando explícito; nunca escribe stock directamente.

## Consola

Permite filtrar por sistema, tipo, estado, fecha, tenant, entidad y CorrelationId; muestra timeline, latencia, mapeo redactado, intentos y dependencia. Reprocesar/cancelar requiere permiso, motivo y confirmación proporcional al riesgo.

## Catálogo de eventos MVP

| ID | Evento | Owner | Consumidor principal |
|---|---|---|---|
| EVENT-INB-0001 | `ReceiptCompleted.v1` | Inbound | Integración, Inventario |
| EVENT-INB-0002 | `PutawayCompleted.v1` | Inbound | Supervisión |
| EVENT-INV-0001 | `StockChanged.v1` | Inventario | Proyecciones/observabilidad |
| EVENT-OUT-0001 | `ShipmentDispatched.v1` | Outbound | Integración |
| EVENT-OUT-0002 | `ShortPickRecorded.v1` | Outbound | Supervisión |
| EVENT-TSK-0001 | `TaskAssigned.v1` | Task Execution | Mobile Sync |
| EVENT-MOB-0001 | `MobileCommandProcessed.v1` | Mobile Sync | Auditoría/observabilidad |
