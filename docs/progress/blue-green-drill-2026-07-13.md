# Evidencia de conmutación blue/green — 2026-07-13

## Alcance

`TEST-OPS-0003` ejecutó API y worker en slots blue/green contra PostgreSQL y un ERP simulado. Un ingress Nginx mantuvo el endpoint estable mientras el candidato se preparó y validó. El slot anterior permaneció activo hasta confirmar readiness del nuevo.

## Resultado

| Comprobación | Resultado |
|---|---:|
| Estado | PASS |
| Secuencia | blue → green → blue |
| Conmutación a green | 1,553 s |
| Rollback a blue | 1,296 s |
| Solicitudes durante conmutaciones | 178 |
| Fallos de continuidad | 0 |
| Worker blue activo | Sí |
| Worker green activo | Sí |
| Slot final observado por ingress | blue |

Cada cambio ejecutó validación directa de API, estado del worker, `nginx -t`, recarga graceful y verificación del header `X-WMS-Deployment-Slot` por el endpoint estable. El drill eliminó contenedores, redes, volúmenes, tags temporales y configuración runtime al terminar; sólo preservó el manifiesto JSON en `.backups`.

## Automatización y límites

`scripts/blue-green-drill.ps1` construye imágenes desde el workspace por defecto para CI. La corrida local reutilizó imágenes API/worker ya construidas porque el backend no cambió desde el commit anterior; las reetiquetó con un tag único antes de desplegar. El gate debe repetirse en piloto con imágenes firmadas, balanceador real, migraciones expand/contract y observación de SLI durante 30 minutos y 24 horas.
