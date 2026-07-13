# Estrategia offline-first

## Datos locales

Room almacena identidad/alcance, configuración, tareas asignadas, ubicaciones y SKU mínimos, checkpoints, cola de comandos, resultados, conflictos y referencias de evidencia. Datos cifrados, segregados por sesión y eliminados al revocar/cambiar tenant.

## Comando 1.0

```json
{
  "commandId": "83bab098-0870-4f34-b886-f739ee1dc105",
  "commandType": "ConfirmPick",
  "schemaVersion": "1.0",
  "tenantId": "5a1361bc-8432-4eba-ae43-315bad780b91",
  "warehouseId": "f9dd692e-c00a-460c-896f-3e26a3dfc1c1",
  "deviceId": "ZEBRA-TC58-001",
  "userId": "d05c52bf-6297-4578-8b9a-1ce206f45949",
  "occurredAt": "2026-07-13T15:03:12Z",
  "localSequence": 18,
  "entityVersion": 4,
  "taskId": "4bc00512-f21f-4dd1-90f4-617993064744",
  "payload": {"locationBarcode":"A-01-01","skuBarcode":"SKU-001","quantity":2}
}
```

## Procesamiento

- El cliente persiste antes de mostrar éxito y envía batches de ≤100.
- El servidor agrupa por tarea y procesa `LocalSequence` ascendente; un fallo bloquea comandos posteriores de esa tarea, no otras.
- `CommandId` garantiza idempotencia. Repetición igual devuelve `AlreadyProcessed`; payload distinto con el mismo ID se rechaza.
- Checkpoint confirma el máximo resultado durable; WorkManager reintenta sólo fallos de transporte.

## Resultados

`Accepted`, `Rejected`, `Conflict`, `AlreadyProcessed`, `RequiresReview`, `Expired`, `Unauthorized`. Incluyen `commandId`, estado, código, mensaje seguro, versión actual y acción sugerida. Un conflicto nunca aplica last-write-wins ni ajusta stock automáticamente.

## Matriz de conflicto

| Condición | Resultado | Acción |
|---|---|---|
| Versión vigente y tarea asignada | Accepted | actualizar checkpoint |
| Comando ya aplicado | AlreadyProcessed | marcar enviado |
| Versión cambió/stock no elegible | Conflict | congelar tarea y mostrar supervisor |
| Tarea expirada/cancelada | Expired | impedir ejecución y refrescar |
| Usuario/dispositivo/scope inválido | Unauthorized | bloquear sync y reautenticar |
| Short pick/override requiere política | RequiresReview | conservar evidencia y esperar decisión |
| Barcode/cantidad inválido | Rejected | permitir corrección local |

## Operación permitida

Recepción, putaway y picking pueden continuar sólo sobre tareas descargadas, no expiradas y asignadas. Packing y despacho requieren conexión. La UI siempre muestra estado online/offline, pendientes y último sync; nunca confunde “guardado local” con “confirmado por servidor”.

## Dispositivos

Zebra DataWedge por intent es la vía primaria; fallback de teclado y cámara. Se normalizan symbology, terminador y deduplicación de lecturas. Feedback: tono/vibración distintos para válido, error y conflicto; controles ≥48dp y contraste AA.
