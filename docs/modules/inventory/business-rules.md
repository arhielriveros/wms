# Reglas de negocio — Inventario

| ID | Regla | Verificación |
|---|---|---|
| RULE-INV-0001 | Todo cambio físico crea un movimiento append-only y actualiza el saldo en la misma transacción; nunca se edita o elimina un movimiento. | TEST-INV-0001 |
| RULE-INV-0002 | La dimensión de bloqueo es `Tenant/Warehouse/Owner/SKU/Location/Status`; se bloquea en orden determinista y se valida `version` antes de escribir. | TEST-INV-0003 |
| RULE-INV-0003 | `available = onHand - reserved - blocked`; ninguna operación puede dejar cantidades negativas ni `reserved > onHand - blocked`. | TEST-INV-0003 |
| RULE-INV-0004 | Una reserva sólo consume disponibilidad elegible, queda vinculada a pedido/línea y se consume o libera explícitamente; FIFO usa fecha de recepción y `movementId` como desempate. | TEST-INV-0001 |
| RULE-INV-0005 | Recepción y putaway registran movimientos a staging y staging→ubicación; pick consume una reserva y despacho registra la salida física una sola vez. | TEST-INV-0005 |
| RULE-INV-0006 | Correcciones se expresan mediante movimiento compensatorio con referencia al original, motivo y autorización; Redis nunca es autoridad. | TEST-INV-0004 |
| RULE-INV-0007 | `CommandId`/`MessageId` repetido con payload igual devuelve el resultado previo; payload diferente es conflicto sin segundo efecto ni evento. | TEST-INV-0002 |

Toda escritura exige `TenantId`, actor, `CorrelationId` y versión esperada. Incumplimientos devuelven códigos estables como `INSUFFICIENT_AVAILABLE`, `STALE_STOCK_VERSION` o `INVALID_STOCK_TRANSITION`, sin efecto parcial.
