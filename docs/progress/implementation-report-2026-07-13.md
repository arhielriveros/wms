# Reporte de implementación — 2026-07-13

## Estado

Fase 0 aprobada. Fundación técnica y baseline funcional de Inbound/Outbound implementadas. Hardening y piloto pendientes de ambiente, hardware y ERP reales.

## Documentación

- 227 documentos no vacíos.
- 11 módulos activos con dossier funcional, técnico, UX, seguridad, observabilidad y pruebas.
- ADR-0001..0008 aceptados y matriz de trazabilidad validada por CI.

## Trabajo realizado

- Backend modular .NET 10: 14 proyectos, API, worker y pruebas.
- Web Next.js: dashboard operacional responsive y estados degradados explícitos.
- Android/Kotlin: caché Room, cola de comandos, sincronización WorkManager y fuentes de escaneo.
- Infraestructura local: 16 servicios/componentes de datos, mensajería, identidad, objetos, mock ERP, telemetría, aplicaciones y carga.
- Automatización: ocho workflows, smoke E2E, carga k6, validadores documentales, backup/restore y despliegue blue/green.
- Ejecución física conectada: tareas Inbound/Outbound, transacciones multi-schema, auditoría, short pick controlado y confirmaciones ERP.

## Evidencias locales

| Comprobación | Resultado |
|---|---|
| `dotnet build src/backend/Wms.Backend.slnx --no-restore` | Correcto, 0 warnings, 0 errores |
| Pruebas backend | 14/14 correctas (dominio + arquitectura) |
| `npm run web:typecheck` | Correcto |
| `npm run web:build` | Correcto |
| `npm audit --audit-level=moderate` | 0 vulnerabilidades |
| Validación documental/trazabilidad | Correcta |
| Docker Compose base/app/load | Configuración válida |
| Smoke E2E | Correcto en stack limpio: Inbound, Outbound, short pick, replay, webhooks y aislamiento tenant en 22,0 s |
| Carga k6 | Correcta: 100 VUs, 27.165 iteraciones, lecturas p95 371,62 ms y batch de 100 comandos p95 2,16 s |
| Volumen histórico | 5.000.000 movimientos; últimos 100 en 0,321 ms y lookup idempotente en 0,165 ms |
| Carga sobre volumen histórico | 32.348 iteraciones; lectura p95 289,83 ms y batch p95 4,49 s |
| Restore lógico aislado | Integridad correcta; RTO no aprobado: 60,615–71,805 s para objetivo < 60 s |
| Recovery físico aislado | `pg_basebackup` 19,922 s; recuperación y validación en 15,302 s, RTO < 60 s aprobado localmente |

## Riesgos abiertos

- La aplicación Android no pudo compilarse en esta estación porque el Android SDK/Gradle wrapper no están disponibles dentro del workspace; CI instala Gradle y el proyecto declara sus versiones.
- La carga de 100 dispositivos, batch móvil y cinco millones de movimientos históricos quedó demostrada; aún faltan 30 usuarios web concurrentes.
- El restore lógico no cumple de forma estable el RTO, pero el recovery físico sí lo hizo en 15,302 s. RPO/PITR, disponibilidad mensual y conmutación blue/green siguen pendientes en ambiente de piloto.
- La compatibilidad Zebra/DataWedge y la ergonomía con guantes requieren prueba física.
- El mock ERP valida entrega y cabeceras firmadas; el cierre del piloto requiere repetir contra el ERP real o sandbox.

## Próximo paso único

Configurar archivado WAL/PITR y demostrar RPO menor o igual a 5 minutos sobre la estrategia física ya validada antes de la UAT.
