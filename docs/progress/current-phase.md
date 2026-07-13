# Fase actual

## Estado

- **Fase:** 4 — Hardening y piloto
- **Hito activo:** Integración/E2E en ambiente de piloto
- **Estado:** Baseline, E2E, volumen, carga, recuperación, blue/green y seguridad Keycloak local aprobados; certificación en piloto pendiente
- **Fecha de corte:** 2026-07-13
- **Gate F0:** Aprobado por revisión multidisciplinaria y autorización explícita del Product Owner.

## Gates alcanzados

| Gate | Evidencia | Estado |
|---|---|---|
| F0 Documentación/decisiones | C4, ADR, expedientes, contratos, UX, QA y trazabilidad | Aprobado |
| H1 Fundación | monorepo, Compose, CI, seguridad, auditoría y telemetría | Baseline implementada |
| H2 Inbound | contratos, importación, tareas, stock, sync y confirmación | Baseline implementada |
| H3 Outbound | pedido, reserva FIFO, picking, packing/despacho y Outbox | Baseline implementada |
| H4 Hardening/piloto | carga, restore, despliegue, seguridad, Zebra, ERP y UAT | Parcial: E2E, 5M, carga, RPO/RTO, blue/green y Keycloak aprobados localmente |

## Observaciones resueltas

- Endpoints Mobile Sync alineados con `/api/v1/mobile/bootstrap`, `/mobile/tasks` y `/mobile/commands:batch`.
- Invariantes de ledger, saldos, reservas FIFO, versión y bloqueo de stock expresadas en código y pruebas.
- Propagación de `TenantId` a PostgreSQL para RLS y roles por módulo incorporados.
- Fronteras entre proyectos y schemas cubiertas por pruebas de arquitectura.
- Flujos ASN/recepción/putaway y pedido/pick/pack/ship enlazados a idempotencia y Outbox.
- Stack reducido con PostgreSQL real, API, worker y mock ERP validado de punta a punta con dos tenants.
- Carga móvil validada con 100 VUs: lectura p95 371,62 ms y batch de 100 comandos p95 2,16 s.
- Cinco millones de movimientos validados; con el volumen presente, lectura móvil p95 289,83 ms y batch p95 4,49 s.
- Recovery lógico íntegro pero variable entre 60,615 y 71,805 s; no certifica RTO menor a 60 s.
- Recovery físico con `pg_basebackup` aprobado: backup 19,922 s y servicio restaurado/validado en 15,302 s.
- Carga web validada con 30 usuarios: 6.607 consultas, 0 errores y dashboard p95 449,71 ms.
- Archivado WAL/PITR aprobado localmente: backup base 5,632 s, recovery 6,585 s, RPO observado 2,313 s y transacción post-target excluida.
- Blue/green aprobado localmente: switch 1,553 s, rollback 1,296 s y 178 solicitudes continuas sin fallos, con ambos workers activos.
- Seguridad Keycloak aprobada localmente: RBAC, IDOR, adulteración de claims y revocación inmediata con ocho controles HTTP correctos y cero secretos persistidos.

## Dependencias para piloto

Permanecen activos `BLK-UAT-0001` (hardware/red Zebra) y `BLK-UAT-0002` (sandbox/SLAs ERP). No bloquean la baseline, pero impiden certificar Hito 4.

## Próximo paso único

Ejecutar el gate de observabilidad de punta a punta con API, worker, RabbitMQ, Redis, MinIO y el stack OpenTelemetry completos.
