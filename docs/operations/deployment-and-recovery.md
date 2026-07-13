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

La conmutación ejecutable usa un ingress estable y mantiene el slot anterior hasta validar API, worker y ruta nueva. Si readiness, recarga o verificación post-switch falla, restaura la configuración anterior y detiene el candidato. El gate local completo es:

```powershell
./scripts/blue-green-drill.ps1
```

Para un despliegue controlado se invoca `scripts/blue-green.ps1` con `TargetSlot`, tag inmutable, repositorios API/worker y archivo de ambiente. `KeepPrevious` conserva el slot anterior durante la ventana de observación; sin ese switch se detiene sólo después de verificar el nuevo ingress.

## Backup

PostgreSQL: full diario, WAL/PITR continuo para RPO 5 min, cifrado y copia fuera del host. S3: versionado/retención. Keycloak y configuración de broker/observabilidad se exportan versionados sin secretos. Se verifican checksums y fallos alertan.

La baseline Compose activa `wal_level=replica`, `archive_mode=on` y `archive_timeout=60s`. Los WAL se escriben en un volumen distinto de `PGDATA`; en piloto ese destino debe sustituirse o replicarse hacia almacenamiento duradero fuera del host, con retención, cifrado, capacidad y alertas supervisadas.

## Restore

Mensualmente en ambiente aislado: restaurar a punto elegido, validar migraciones, tenants, conteos/ledger-saldo, Inbox/Outbox, evidencia, autenticación y smoke inbound/outbound. Medir RTO desde declaración hasta servicio verificado. Guardar acta `TEST-OPS-0001`.

Los gates locales reproducibles son:

```powershell
./scripts/physical-recovery-drill.ps1
./scripts/pitr-recovery-drill.ps1
```

El segundo crea un acceso de replicación SCRAM temporal limitado a la red Compose, ejecuta `pg_basebackup`, restaura hasta un timestamp elegido y comprueba que la transacción anterior exista y la posterior no. Los contenedores, volúmenes y regla temporal se eliminan incluso ante error; el manifiesto JSON queda en `.backups`.

## Runbook de degradación ERP

Mantener operación física autorizada, elevar alerta, observar Outbox, no reprocesar en paralelo, reconciliar al volver, reprocesar idempotentemente y verificar acuse. Nunca revertir recepción/despacho mediante SQL.

## Runbook de inconsistencia de stock

Detener sólo la dimensión/operación afectada, preservar evidencia, abrir incidente crítico, comparar movimientos/saldo/reservas, no editar filas, aplicar comando compensatorio aprobado, reconciliar y documentar causa/prevención.
