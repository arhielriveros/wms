# Bloqueos

## Activos para UAT/piloto

| ID | Bloqueo | Impacto | Owner | Salida |
|---|---|---|---|---|
| BLK-UAT-0001 | Hardware Zebra y red del almacén no caracterizados | Impide certificar escaneo, ergonomía y rendimiento físico | Operaciones | inventario de modelos, DataWedge y site survey |
| BLK-UAT-0002 | Endpoint/SLAs reales del ERP piloto no confirmados | Impide certificar mapeo, firma y retry contra el ERP real | Integración/ERP | contrato técnico y sandbox |

No hay bloqueos activos para desarrollar y probar localmente el MVP con adaptadores genéricos y emuladores. Todo bloqueo nuevo debe incluir owner, impacto, fecha y criterio de salida.

## Resueltos localmente

- `BLK-OPS-0001`: archivado WAL/PITR configurado y validado el 2026-07-13. Recovery 6,585 s y RPO observado 2,313 s; queda como gate de repetición en infraestructura equivalente al piloto, no como bloqueo de desarrollo.
