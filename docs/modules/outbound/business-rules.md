# Reglas de negocio — Outbound

| ID | Regla | Verificación |
|---|---|---|
| RULE-OUT-0001 | El pedido entra exclusivamente como `SalesOrder 1.0` por API-INT-0002; `MessageId` repetido e idéntico no duplica pedido, reserva ni tarea. | TEST-OUT-0002 |
| RULE-OUT-0002 | La asignación usa FIFO por fecha de recepción y `movementId`, bloquea dimensiones en orden y reserva como máximo la disponibilidad elegible; nunca sobreasigna. | TEST-OUT-0003 |
| RULE-OUT-0003 | Liberar exige reserva vigente y crea tareas de pick por líneas/cantidades asignadas; no se pickea stock no reservado ni desde ubicación distinta sin override aprobado. | TEST-OUT-0001 |
| RULE-OUT-0004 | Confirmar pick consume la reserva y mueve el stock a staging/packing una sola vez; picking offline sólo es válido para tarea asignada, no expirada y versión vigente. | TEST-OUT-0005 |
| RULE-OUT-0005 | Short pick registra cantidad, motivo y evidencia, pausa el remanente y requiere política/aprobación; nunca inventa disponibilidad ni corrige stock silenciosamente. | TEST-OUT-0003 |
| RULE-OUT-0006 | Packing sólo admite cantidades pickeadas y requiere conexión; despacho sólo admite shipment packed, es irreversible y registra la salida física una vez. | TEST-OUT-0001 |
| RULE-OUT-0007 | `ShipmentDispatched.v1` genera una única `ShipmentConfirmation 1.0`; fallo ERP deja webhook pendiente sin revertir despacho. | TEST-OUT-0005 |

Toda mutación conserva `TenantId`, actor, `CorrelationId`, identificador idempotente y versión esperada. La cancelación ERP es un comando sujeto al estado WMS, no una escritura directa.
