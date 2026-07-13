# Escenarios E2E de referencia

## Inbound resiliente

```gherkin
Escenario: recepción confirmada mientras el ERP no responde
  Dado un ASN canónico aceptado una sola vez
  Y una tarea de recepción asignada al dispositivo
  Cuando el operario escanea staging, SKU y cantidad y confirma putaway
  Y el webhook ERP falla temporalmente
  Entonces movimiento y saldo quedan confirmados una sola vez
  Y la confirmación permanece reintentable en Outbox
  Y el CorrelationId une ASN, tarea, movimiento y entrega
```

## Conflicto offline

```gherkin
Escenario: el saldo cambia antes de sincronizar un pick
  Dado un pick descargado con versión 4
  Y un cambio autorizado deja la dimensión en versión 5
  Cuando el dispositivo envía el comando de versión 4
  Entonces recibe Conflict
  Y no se ajusta stock automáticamente
  Y la tarea queda pausada para revisión
```

## Outbound concurrente

```gherkin
Escenario: dos pedidos compiten por la misma disponibilidad
  Dado un saldo disponible de 10 unidades
  Cuando dos asignaciones concurrentes solicitan 8 unidades cada una
  Entonces como máximo una asignación de 8 es aceptada
  Y la otra recibe insuficiencia o asignación parcial según política
  Y el saldo reservado nunca supera 10
```

## Automatización ejecutable

`scripts/smoke-e2e.ps1` implementa el recorrido real contra el perfil Compose `app`: ASN duplicado, recepción, replay del comando móvil, putaway, confirmación ERP firmada, pedido, liberación FIFO, pick, packing, despacho, short pick aprobado y aislamiento entre dos tenants.

`tests/performance/mobile-sync.js` ejecuta dos escenarios k6: 100 dispositivos concurrentes con umbral p95 menor a 500 ms y batches de 100 comandos con p95 menor a 10 segundos. Los resultados sólo se consideran evidencia cuando el workflow `Pilot Performance` finaliza correctamente en el ambiente objetivo.
