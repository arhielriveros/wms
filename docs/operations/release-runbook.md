# Runbook de release piloto

## Pre-release

- Gate H correspondiente aprobado y matriz/release notes completas.
- Artefactos firmados, SBOM y scans sin crítico/alto no aceptado.
- Compatibilidad API/evento/migración verificada; backup y rollback ensayados.
- Dashboard/alertas activos, on-call informado, flags en default seguro.

## Ejecución

Registrar versión, commit, operador, UTC y ticket. Ejecutar `scripts/blue-green.ps1` con tag inmutable y `KeepPrevious`, luego smoke de autenticación, tenant, inventario, Inbox/Outbox, mobile bootstrap y webhook simulado. Verificar el header de slot por el ingress estable antes de iniciar la observación y habilitar shadow/zona/full según acta.

## Abort conditions

Fuga tenant, invariante de stock, error rate >5 %, p95 >2× objetivo sostenido, pérdida de trazas, migración irreversible inesperada o falla de autenticación. Reejecutar `scripts/blue-green.ps1` hacia el slot anterior, verificar ingress, pausar consumidores si aplica y preservar evidencia.

## Cierre

Revisar SLI 30 min/24 h, confirmar colas/DLQ, cerrar change, publicar changelog/release notes, actualizar matriz con commit/release y registrar deuda/incidentes.
