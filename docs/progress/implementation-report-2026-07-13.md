# Reporte de implementación — 2026-07-13

## Estado

Fase 0 aprobada. Fundación técnica y baseline funcional de Inbound/Outbound implementadas. Los gates locales de volumen, carga web/móvil, RTO/RPO, blue/green, seguridad Keycloak y observabilidad E2E están aprobados; el cierre del piloto depende del ambiente, hardware y ERP reales.

## Documentación

- 231 documentos no vacíos.
- 11 módulos activos con dossier funcional, técnico, UX, seguridad, observabilidad y pruebas.
- ADR-0001..0008 aceptados y matriz de trazabilidad validada por CI.

## Trabajo realizado

- Backend modular .NET 10: 14 proyectos, API, worker y pruebas.
- Web Next.js: dashboard operacional responsive y estados degradados explícitos.
- Android/Kotlin: caché Room, cola de comandos, sincronización WorkManager y fuentes de escaneo.
- Infraestructura local: 16 servicios/componentes de datos, mensajería, identidad, objetos, mock ERP, telemetría, aplicaciones y carga.
- Automatización: once workflows, smoke E2E, carga k6, validadores documentales, backup/restore, blue/green, gate Keycloak y gate de observabilidad.
- Ejecución física conectada: tareas Inbound/Outbound, transacciones multi-schema, auditoría, short pick controlado y confirmaciones ERP.

## Evidencias locales

| Comprobación | Resultado |
|---|---|
| `dotnet build src/backend/Wms.Backend.slnx --no-restore` | Correcto, 0 warnings, 0 errores |
| Pruebas backend | 19/19 correctas (dominio + arquitectura + permisos de token) |
| `npm run web:typecheck` | Correcto |
| `npm run web:build` | Correcto |
| `npm audit --audit-level=moderate` | 0 vulnerabilidades |
| Validación documental/trazabilidad | Correcta |
| Docker Compose base/app/load | Configuración válida |
| Smoke E2E | Correcto en stack limpio: Inbound, Outbound, short pick, replay, webhooks y aislamiento tenant en 22,0 s |
| Carga k6 | Correcta: 100 VUs, 27.165 iteraciones, lecturas p95 371,62 ms y batch de 100 comandos p95 2,16 s |
| Volumen histórico | 5.000.000 movimientos; últimos 100 en 0,321 ms y lookup idempotente en 0,165 ms |
| Carga sobre volumen histórico | 32.348 iteraciones; lectura p95 289,83 ms y batch p95 4,49 s |
| Carga web concurrente | 30 usuarios, 6.607 consultas, 0 errores y dashboard p95 449,71 ms |
| Restore lógico aislado | Integridad correcta; RTO no aprobado: 60,615–71,805 s para objetivo < 60 s |
| Recovery físico aislado | `pg_basebackup` 19,922 s; recuperación y validación en 15,302 s, RTO < 60 s aprobado localmente |
| WAL/PITR aislado | backup base 5,632 s; recuperación en 6,585 s; RPO observado 2,313 s y exclusión post-target correcta |
| Blue/green aislado | switch green 1,553 s; rollback blue 1,296 s; 178 solicitudes y 0 fallos; ambos workers activos |
| Seguridad Keycloak aislada | 8/8 controles correctos: 401 sin token, 200 autorizado, 403 escalada, 404 IDOR, 200 recurso propio, 401 adulteración, 200 activo y 401 revocado |
| Observabilidad E2E aislada | PASS en 180,77 s: dependencias y Grafana correctos; 2 métricas, 32 resultados de trazas y 98 streams de logs de API/worker; cero secretos persistidos |

## Riesgos abiertos

- La aplicación Android no pudo compilarse en esta estación porque el Android SDK/Gradle wrapper no están disponibles dentro del workspace; CI instala Gradle y el proyecto declara sus versiones.
- La carga de 100 dispositivos, batch móvil, 30 usuarios web y cinco millones de movimientos históricos quedó demostrada localmente; debe repetirse en infraestructura equivalente al piloto.
- El restore lógico no cumple de forma estable el RTO, pero recovery físico, PITR y blue/green cumplieron localmente. Disponibilidad mensual y repetición contra balanceador/infraestructura piloto siguen pendientes.
- La compatibilidad Zebra/DataWedge y la ergonomía con guantes requieren prueba física.
- El mock ERP valida entrega y cabeceras firmadas; el cierre del piloto requiere repetir contra el ERP real o sandbox.

## Próximo paso único

Automatizar el gate web de accesibilidad WCAG 2.2 AA y documentar la preparación ergonómica móvil previa a la UAT física Zebra.
