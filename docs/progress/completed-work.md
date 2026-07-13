# Trabajo completado

## Corte 2026-07-13

- Fuente maestra, visión, alcance, actores, KPI, glosario, roadmap y límites versionados.
- C4, bounded contexts, dependencias permitidas, autoridad ERP/WMS y ADR-0001..0008 aprobados.
- Expediente funcional/técnico de los 11 módulos activos y overview de contextos futuros.
- Backend .NET 10 con 14 proyectos, API/worker coordinados, persistencia modular, RLS, auditoría, Inbox/Outbox y sincronización móvil.
- Contratos `/api/v1` para ASN, pedidos, consulta de mensajes, stock, dashboard y bootstrap/tareas/comandos móviles.
- Reglas ejecutables para stock, versión optimista, reservas, tareas, idempotencia, reintentos y política de conflictos.
- Consola Next.js responsive y cliente Android/Kotlin offline-first con Room, WorkManager, DataWedge y cámara.
- Docker Compose para datos, broker, identidad, objetos, telemetría y aplicaciones; scripts de backup/restore y blue/green.
- GitHub Actions para backend, web, Android, documentación, trazabilidad e infraestructura.
- Validaciones locales: backend sin warnings, 14/14 tests de dominio y arquitectura, typecheck/build web y auditoría npm sin vulnerabilidades.
- Smoke E2E ejecutado en Docker desde base limpia con dos tenants, mock ERP, replay móvil, happy paths Inbound/Outbound, short pick controlado y webhooks; resultado correcto en 22,0 s.
- Escenario k6 aprobado: 100 dispositivos, 27.165 iteraciones, lectura de tareas p95 371,62 ms y batch de 100 comandos p95 2,16 s.
- Cinco millones de movimientos históricos generados con ledger neto e aislamiento tenant verificados; consultas históricas indexadas submilisegundo.
- Carga móvil repetida sobre 5M: 32.348 iteraciones, lectura p95 289,83 ms y batch p95 4,49 s.
- Recovery lógico aislado automatizado con checksum y validación de integridad; RTO lógico identificado como no conforme y convertido en gate ejecutable.
- Recovery físico automatizado con `pg_basebackup`: clúster de 1,986 GB respaldado en 19,922 s y restaurado/validado en 15,302 s, cumpliendo RTO local menor a 60 s.
- Carga de supervisión aprobada con 30 usuarios concurrentes: 6.607 consultas, 0 errores y p95 449,71 ms.
- WAL continuo y PITR automatizados: recuperación aislada al punto elegido en 6,585 s, RPO observado 2,313 s y transacción posterior correctamente excluida.
- Conmutación blue/green automatizada con ingress estable: green en 1,553 s, rollback blue en 1,296 s y 178 solicitudes sin fallos; API y workers de ambos slots verificados.
- Gate de seguridad real automatizado con Keycloak 26.1: ocho controles aprobados para autenticación, RBAC, IDOR, adulteración y revocación; evidencia redactada y workflow dedicado.

## Pendiente para cerrar Hito 4

- Integración con el stack completo de observabilidad, Keycloak, RabbitMQ, Redis y MinIO en un ambiente de piloto.
- Repetición del gate Keycloak local en el ambiente piloto con TLS, MFA y configuración operacional definitiva.
- Repetición de carga web/móvil y volumen histórico en infraestructura equivalente al piloto; los umbrales ya cumplen localmente.
- Repetición del recovery físico/PITR con archivo WAL externo y del blue/green contra el balanceador real del piloto; RPO/RTO y conmutación ya cumplen localmente, mientras el restore lógico no lo hace de forma estable.
- UAT física de escaneo, sonido, vibración, contraste y guantes en Zebra.
- Evidencia de ERP real/sandbox, firma de webhooks y reconciliación operacional.

## Resultado

La baseline ejecutable de Fase 0 + MVP está implementada y validada estática y dinámicamente para los slices principales. Los objetivos locales de carga, volumen, RPO/RTO y despliegue sin corte están demostrados; no corresponde declarar todavía disponibilidad mensual ni compatibilidad física hasta repetir los gates en el piloto y reunir evidencias externas.
