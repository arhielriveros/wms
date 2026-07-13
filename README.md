# WMS modular, ERP-agnostic y offline-first

Baseline ejecutable del piloto WMS para un almacén mediano. El repositorio contiene un monolito modular .NET 10 (API + worker), una consola de supervisión Next.js, una aplicación Android/Kotlin offline-first y la infraestructura local reproducible.

## Estado

**Fase 0 aprobada y baseline de Hitos 1–3 implementada.** Las reglas, contratos y decisiones transversales están versionadas. El código compila, las pruebas de dominio pasan y la consola web produce un build optimizado. El cierre de Hito 4 requiere ejecutar el piloto con servicios reales, hardware Zebra, ERP de prueba y carga representativa.

## Componentes

- `src/backend`: API y worker .NET 10, módulos aislados, EF Core, RLS, Inbox/Outbox e idempotencia.
- `apps/web`: consola Next.js para flujo, inventario, tareas, excepciones e integración.
- `apps/mobile`: cliente Android nativo con Room, WorkManager, DataWedge, cámara y batch de comandos offline.
- `infra`: PostgreSQL, RabbitMQ, Redis, Keycloak, MinIO y observabilidad mediante Docker Compose.
- `docs`: arquitectura, ADR, expedientes por módulo, seguridad, UX, pruebas, operación y trazabilidad.

## Inicio rápido

Requisitos: .NET SDK 10, Node.js/npm, Docker Compose y JDK 17. Para Android se requiere Android SDK; CI instala Gradle 8.10.2.

```powershell
Copy-Item .env.example .env
docker compose --env-file .env --profile app up --build
```

Smoke E2E del piloto, una vez saludable el stack:

```powershell
docker compose --env-file .env --project-name wms-smoke --file docker-compose.smoke.yml up --detach --build --wait
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\smoke-e2e.ps1
```

Prueba de 100 dispositivos y batches de 100 comandos:

```powershell
docker compose --env-file .env --project-name wms-smoke --file docker-compose.smoke.yml --profile load run --rm load-test
```

Carga de 30 supervisores y drills de recuperación física/PITR:

```powershell
docker compose --env-file .env --project-name wms-smoke --file docker-compose.smoke.yml --profile load run --rm load-test run /scripts/web-dashboard.js
./scripts/physical-recovery-drill.ps1
./scripts/pitr-recovery-drill.ps1
```

Conmutación blue/green y rollback con tráfico continuo:

```powershell
./scripts/blue-green-drill.ps1
```

Validación local sin levantar contenedores:

```powershell
dotnet restore src\backend\Wms.Backend.slnx --configfile NuGet.Config
dotnet build src\backend\Wms.Backend.slnx --no-restore
dotnet test tests\backend\Wms.Backend.Tests\Wms.Backend.Tests.csproj --no-restore
npm ci
npm run web:typecheck
npm run web:build
npm run validate:docs
```

La API pública vive bajo `/api/v1`. En desarrollo local puede usarse el esquema de cabeceras documentado en `src/backend/README.md`; fuera de Development la autenticación es OIDC/Keycloak.

## Slices implementados

- Inbound: ingreso idempotente de ASN, recepción, putaway, movimiento/saldo y confirmación ERP vía Outbox.
- Outbound: ingreso de pedido, liberación manual, reserva FIFO, picking, short pick supervisado, packing, despacho y confirmación ERP.
- Móvil: bootstrap, tareas asignadas, secuencia local y envío batch con resultados tipados; los conflictos no alteran stock automáticamente.
- Supervisión: tablero operacional responsive con estados explícitos de carga, error, offline, conflicto, permisos y alertas.

## Documentación de referencia

- [Fase actual](docs/progress/current-phase.md)
- [Arquitectura](docs/architecture/overview.md)
- [Decisiones arquitectónicas](docs/adr/README.md)
- [Contratos canónicos](docs/integration/canonical-contracts.md)
- [Modelo de seguridad](docs/security/security-model.md)
- [Estrategia de pruebas](docs/testing/test-strategy.md)
- [Trazabilidad](docs/traceability/requirements-traceability-matrix.md)
- [Reporte de implementación](docs/progress/implementation-report-2026-07-13.md)

## Límites de esta entrega

Los gates locales con 100 dispositivos, 30 usuarios web, cinco millones de movimientos y recuperación física/PITR están aprobados. No se declara el piloto productivo hasta repetirlos en infraestructura equivalente, completar failover blue/green, UAT física Zebra y reconciliación contra un ERP real. Lotes, series, HU/GS1 avanzado, olas, reposición, devoluciones y optimización permanecen fuera del MVP.
