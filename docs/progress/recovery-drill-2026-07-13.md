# Evidencia de volumen histórico y recuperación — 2026-07-13

## Alcance

Drill local reproducible sobre PostgreSQL 17 en Docker Desktop. Se preservaron constraints, índice histórico, unicidad idempotente y ledger append-only. El restore se realizó siempre en la base aislada `wms_restore_drill`; nunca se reemplazó la base activa.

## Volumen y consultas

| Comprobación | Resultado |
|---|---:|
| Movimientos históricos | 5.000.000 |
| Tiempo de generación | 251,638 s |
| Ledger neto sintético | 0 |
| Filas históricas tenant B | 0 |
| Últimos 100 movimientos, tiempo PostgreSQL | 0,321 ms |
| Lookup por `TenantId + CommandId`, tiempo PostgreSQL | 0,165 ms |

La carga k6 ejecutada sobre esta misma base produjo 32.348 iteraciones con 100 dispositivos: lectura de tareas p95 289,83 ms y batch de 100 comandos p95 4,49 s. Ambos umbrales del MVP se cumplieron.

## Recovery drill lógico

| Corrida | Backup | Restore | Validación | Recovery verificado | Resultado RTO < 60 s |
|---|---:|---:|---:|---:|---|
| Baseline, compresión 6 | 27,284 s | 69,857 s | 1,948 s | 71,805 s | No |
| Tuning aislado, compresión 1 | 22,252 s | 58,351 s | 2,264 s | 60,615 s | No |
| Gate automatizado | 19,250 s | 69,972 s | 1,506 s | 71,478 s | No |

En todas las corridas se restauraron 5.000.000 movimientos, ledger neto 0 y cero filas históricas para tenant B. El dump final midió 67.664.699 bytes y tuvo SHA-256 `943F32BE33D57D8F35B7E113C5C0E5994B8C0A2B060FE232C0520000615A7031`.

## Decisión

El dump lógico queda aprobado como mecanismo de respaldo verificable y recuperación funcional, pero **no certifica el RTO de 60 segundos** en este volumen. La variabilidad observada entre 60,615 y 71,805 segundos exige recuperación física para el piloto.

## Recovery físico

Se generó un `pg_basebackup` consistente del clúster de 1,986 GB en 19,922 s. A partir de ese backup se copió un volumen aislado, se inició una segunda instancia PostgreSQL y se validaron servicio, 5.000.000 movimientos, ledger neto 0 y tenant B en 0. El recovery completo tardó **15,302 s**, por lo que el RTO local menor a 60 s quedó aprobado para este método.

La regla de replicación SCRAM utilizada fue temporal, restringida al usuario del drill y eliminada automáticamente. Esta evidencia no certifica todavía RPO 5 min: falta configurar y probar archivado WAL/PITR o snapshots continuos en infraestructura equivalente al piloto.
