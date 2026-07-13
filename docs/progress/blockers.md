# Bloqueos

## Activos para UAT/piloto

| ID | Bloqueo | Impacto | Owner | Salida |
|---|---|---|---|---|
| BLK-UAT-0001 | Hardware Zebra y red del almacén no caracterizados | Impide certificar escaneo, ergonomía y rendimiento físico | Operaciones | inventario de modelos, DataWedge y site survey |
| BLK-UAT-0002 | Endpoint/SLAs reales del ERP piloto no confirmados | Impide certificar mapeo, firma y retry contra el ERP real | Integración/ERP | contrato técnico y sandbox |
| BLK-OPS-0001 | Archivado WAL/PITR aún no configurado | Impide certificar RPO ≤ 5 min; RTO físico local ya pasó en 15,302 s | Plataforma/DBA | archivar WAL, restaurar a un punto elegido y medir pérdida máxima en infraestructura equivalente al piloto |

No hay bloqueos activos para desarrollar y probar localmente el MVP con adaptadores genéricos y emuladores. Todo bloqueo nuevo debe incluir owner, impacto, fecha y criterio de salida.
