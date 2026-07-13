# Fase actual

## Estado

- **Fase:** 4 — Hardening y piloto
- **Hito activo:** Integración/E2E en ambiente de piloto
- **Estado:** Baseline y E2E local aprobados; hardening histórico, recuperación y evidencias físicas pendientes
- **Fecha de corte:** 2026-07-13
- **Gate F0:** Aprobado por revisión multidisciplinaria y autorización explícita del Product Owner.

## Gates alcanzados

| Gate | Evidencia | Estado |
|---|---|---|
| F0 Documentación/decisiones | C4, ADR, expedientes, contratos, UX, QA y trazabilidad | Aprobado |
| H1 Fundación | monorepo, Compose, CI, seguridad, auditoría y telemetría | Baseline implementada |
| H2 Inbound | contratos, importación, tareas, stock, sync y confirmación | Baseline implementada |
| H3 Outbound | pedido, reserva FIFO, picking, packing/despacho y Outbox | Baseline implementada |
| H4 Hardening/piloto | carga, restore, Zebra, ERP y UAT | Parcial: E2E y carga móvil aprobados |

## Observaciones resueltas

- Endpoints Mobile Sync alineados con `/api/v1/mobile/bootstrap`, `/mobile/tasks` y `/mobile/commands:batch`.
- Invariantes de ledger, saldos, reservas FIFO, versión y bloqueo de stock expresadas en código y pruebas.
- Propagación de `TenantId` a PostgreSQL para RLS y roles por módulo incorporados.
- Fronteras entre proyectos y schemas cubiertas por pruebas de arquitectura.
- Flujos ASN/recepción/putaway y pedido/pick/pack/ship enlazados a idempotencia y Outbox.
- Stack reducido con PostgreSQL real, API, worker y mock ERP validado de punta a punta con dos tenants.
- Carga móvil validada con 100 VUs: lectura p95 371,62 ms y batch de 100 comandos p95 2,16 s.

## Dependencias para piloto

Permanecen activos `BLK-UAT-0001` (hardware/red Zebra) y `BLK-UAT-0002` (sandbox/SLAs ERP). No bloquean la baseline, pero impiden certificar Hito 4.

## Próximo paso único

Ejecutar cinco millones de movimientos históricos y un restore cronometrado para verificar los objetivos RPO/RTO antes de UAT física.
