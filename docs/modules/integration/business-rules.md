# Reglas de negocio — Integración

| ID | Regla | Verificación |
|---|---|---|
| RULE-INT-0001 | Inbox reserva `sourceSystem + messageType + messageId`; duplicado con checksum igual devuelve el resultado previo y checksum distinto retorna `DUPLICATE_PAYLOAD_MISMATCH`. | TEST-INT-0002 |
| RULE-INT-0002 | Sólo se aceptan envelopes y schemas canónicos soportados; tenant, scopes, códigos y cantidad/UOM se validan antes de invocar Inbound/Outbound. | TEST-INT-0005 |
| RULE-INT-0003 | Efecto local, Inbox y Outbox se confirman en la misma transacción; RabbitMQ opera at-least-once y todo consumidor deduplica por MessageId. | TEST-INT-0003 |
| RULE-INT-0004 | ReceiptConfirmation y ShipmentConfirmation se envían con HMAC v1 sobre `timestamp.rawBody`, MessageId y tolerancia de reloj de 5 minutos. | TEST-INT-0005 |
| RULE-INT-0005 | 408/429/5xx reintentan con jitter; 4xx no transitorio o agotamiento pasa a RequiresReview/DeadLettered, conservando payload cifrado y error tipado. | TEST-INT-0003 |
| RULE-INT-0006 | Reprocesar crea un nuevo DeliveryAttempt, conserva MessageId, exige motivo/permiso y no repite el hecho físico. | TEST-INT-0004 |
| RULE-INT-0007 | Caída ERP nunca revierte recepción o despacho; reconciliación crea comandos explícitos y jamás escribe stock directamente. | TEST-INT-0001 |

Política por defecto: 1 min, 5 min, 15 min, 1 h y 6 h, máximo 8 intentos/24 h; un `Retry-After` válido prevalece.
