# Arquitectura transversal

## Estilo

Monolito modular distribuible, ejecutado como dos procesos coordinados: API y Worker. Comparten artefactos versionados, pero cada módulo posee schema, `DbContext`, contratos y permisos de base propios. La extracción futura se habilita por eventos y contratos, no se anticipa.

## Contenedores

- **API .NET 10:** REST `/api/v1`, autenticación, autorización, orquestación de comandos y consultas.
- **Worker .NET 10:** Outbox, Inbox, integración, webhooks, reconciliación y trabajos programados.
- **Web Next.js:** consola de supervisión; no accede directamente a datos.
- **Android/Kotlin:** Jetpack Compose, Room, WorkManager, cola offline y adaptadores de escáner.
- **PostgreSQL:** autoridad transaccional, schemas por módulo, RLS por tenant.
- **RabbitMQ:** transporte at-least-once; no es autoridad de negocio.
- **Redis:** caché/coordinación efímera; jamás saldos o reservas autoritativos.
- **Keycloak:** OIDC/OAuth 2.1, MFA, sesiones y tokens cortos.
- **S3 compatible:** evidencia y adjuntos con metadatos auditables.
- **OpenTelemetry:** logs, métricas y trazas exportadas a la plataforma de observabilidad.

## Módulos del MVP

Plataforma/Tenancy, Seguridad/Auditoría, Layout, Maestros, Inventario, Inbound, Outbound, Task Execution, Integración y Sincronización móvil. Putaway y picking son capacidades de Inbound/Outbound que usan Task Execution e Inventario.

## Reglas de dependencia

1. Dominio no depende de infraestructura, UI ni tipos ERP.
2. Comunicación sincrónica entre módulos sólo por interfaces públicas de aplicación; comunicación asíncrona por eventos versionados.
3. No hay `SELECT`, FK ni navegación ORM hacia schemas ajenos.
4. EF Core se usa para comandos/transacciones; Dapper sólo para proyecciones de lectura documentadas.
5. No se implementan repositorios genéricos ni una capa de eventos sin propietario.
6. Toda operación física crítica genera movimiento, auditoría y telemetría dentro de una frontera consistente.

## Persistencia e inventario

La dimensión mínima de stock es `(TenantId, WarehouseId, OwnerId, SkuId, LocationId, Status)`. Cada cambio bloquea la fila de saldo afectada, valida `Version`, escribe movimiento append-only y actualiza saldo/reserva en una transacción. Un conflicto retorna resultado tipado; nunca se aplica last-write-wins.

## Calidad arquitectónica

El CI rechazará dependencias circulares, referencias a infraestructura desde dominio, acceso cruzado de schemas, contratos no versionados y entidades multi-tenant sin `TenantId`. Las decisiones rectoras están en los ADR.
