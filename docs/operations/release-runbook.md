# Runbook de release piloto

## Pre-release

- Gate H correspondiente aprobado y matriz/release notes completas.
- Artefactos firmados, SBOM y scans sin crítico/alto no aceptado.
- Compatibilidad API/evento/migración verificada; backup y rollback ensayados.
- Dashboard/alertas activos, on-call informado, flags en default seguro.

## Ejecución

Registrar versión, commit, operador, UTC y ticket. Ejecutar blue/green, smoke de autenticación, tenant, inventario, Inbox/Outbox, mobile bootstrap y webhook simulado. Habilitar shadow/zona/full según acta.

## Abort conditions

Fuga tenant, invariante de stock, error rate >5 %, p95 >2× objetivo sostenido, pérdida de trazas, migración irreversible inesperada o falla de autenticación. Conmutar slot, pausar consumidores si aplica y preservar evidencia.

## Cierre

Revisar SLI 30 min/24 h, confirmar colas/DLQ, cerrar change, publicar changelog/release notes, actualizar matriz con commit/release y registrar deuda/incidentes.
