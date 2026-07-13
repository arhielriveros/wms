# Despliegue, backup y recuperación

## Ambientes

Local, CI efímero, integración, UAT y piloto. Configuración externa y secretos por ambiente; datos productivos no se copian a ambientes inferiores sin anonimización.

## Blue/green

1. Build reproducible, SBOM, scan, firma y pruebas.
2. Backup/checkpoint y migraciones expand compatibles con versión anterior.
3. Desplegar slot green API/Worker con consumidores pausados.
4. Readiness, smoke y contrato; activar workers compatibles.
5. Conmutar tráfico y vigilar SLI/error budget.
6. Rollback de tráfico si falla; migraciones destructivas sólo en fase contract posterior.

Feature flags habilitan shadow mode → zona controlada → full. Cada flag tiene owner, fecha de retiro, default seguro y auditoría.

## Backup

PostgreSQL: full diario, WAL/PITR continuo para RPO 5 min, cifrado y copia fuera del host. S3: versionado/retención. Keycloak y configuración de broker/observabilidad se exportan versionados sin secretos. Se verifican checksums y fallos alertan.

## Restore

Mensualmente en ambiente aislado: restaurar a punto elegido, validar migraciones, tenants, conteos/ledger-saldo, Inbox/Outbox, evidencia, autenticación y smoke inbound/outbound. Medir RTO desde declaración hasta servicio verificado. Guardar acta `TEST-OPS-0001`.

## Runbook de degradación ERP

Mantener operación física autorizada, elevar alerta, observar Outbox, no reprocesar en paralelo, reconciliar al volver, reprocesar idempotentemente y verificar acuse. Nunca revertir recepción/despacho mediante SQL.

## Runbook de inconsistencia de stock

Detener sólo la dimensión/operación afectada, preservar evidencia, abrir incidente crítico, comparar movimientos/saldo/reservas, no editar filas, aplicar comando compensatorio aprobado, reconciliar y documentar causa/prevención.
