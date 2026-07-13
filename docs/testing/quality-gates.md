# Gates de calidad y aceptación

## Gate F0 — Documentación

- Visión, límites, glosario, C4, contexts, autoridad y ADR coherentes.
- Seguridad, integración, offline, UX, observabilidad, recuperación y pruebas completas.
- Matriz sin requisito MVP huérfano y decisiones pendientes con owner.
- Aprobación explícita de Producto, Arquitectura, Seguridad, Integración, UX, QA y Operaciones.

## Gate H1 — Fundación técnica

- Build/análisis/pruebas verdes; migraciones repetibles y rollback de aplicación.
- Dos tenants sin leak en API/DB/caché/evidencia/mensajes.
- Health/readiness y traza API→DB→broker visible.
- Backup/restore verificado y secretos ausentes de código/logs.

## Gate H2 — Inbound

ASN idempotente, recepción/putaway online y offline, duplicados/conflictos, concurrencia y ERP caído sin corrupción; consola y evidencia completas.

## Gate H3 — Outbound

Pedido idempotente, FIFO, reserva sin sobreasignación, pick/short pick, packing/despacho online y confirmación ERP reintentable.

## Gate H4 — Piloto

Rendimiento/SLO, seguridad, restore, Zebra, WCAG, UAT, runbooks, dashboards, release notes y shadow mode aprobados. El subgate local de seguridad exige `TEST-SEC-0002` verde contra Keycloak real, sin secretos persistidos y con rechazo de IDOR, escalada, adulteración y revocación. El subgate de observabilidad exige `TEST-OPS-0004` verde con métricas, trazas y logs consultables para API/worker, dependencias operativas verificadas y evidencia redactada. Cero defectos críticos; altos requieren aceptación documentada y fecha.

## Criterio Definition of Done

Toda feature posee caso de uso, regla, permiso, API/evento, validación, auditoría, error tipado, UX/estados, seguridad, telemetría, test, changelog, trazabilidad y evidencia de revisión. Una casilla faltante significa “no terminada”.
