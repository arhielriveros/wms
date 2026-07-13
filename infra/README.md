# WMS local infrastructure

This directory contains development and pilot infrastructure. It does not contain production credentials.

## Bootstrap

1. Copy .env.example to .env and replace every change-me value.
2. Run: pwsh ./scripts/bootstrap.ps1 -Pull
3. Inspect docker compose ps; every long-running service must be healthy.
4. Stop with docker compose --env-file .env down. Do not add --volumes unless data loss is explicitly approved.

The app profile is optional during foundation work. Start API, worker and web with docker compose --env-file .env --profile app up -d --build.

Local endpoints are Keycloak 8080, API 8081, web 3000, RabbitMQ management 15672, MinIO API/console 9001/9002, Prometheus 9090, Loki 3100, Tempo 3200 and Grafana 3001. OTLP uses 4317/4318.

## Security

- Example passwords are development placeholders and must never be reused.
- Keycloak imports no users. Test users need a realm role, tenant/warehouse/owner/zone attributes and TOTP for privileged access.
- Web and mobile use Authorization Code with PKCE. The API is bearer-only and tokens last five minutes.
- PostgreSQL module roles are NOLOGIN groups. Production login roles and secrets come from the secret manager.
- Redis is cache and coordination only. PostgreSQL remains inventory authority.
- The MinIO evidence bucket is private and its initializer is idempotent.

## Data and messaging

PostgreSQL initializes one schema and group role per active module, plus the Keycloak schema. Cross-schema access is not granted automatically. RabbitMQ provisions the topic exchange wms.events, dead-letter exchange wms.dlx and quorum queue wms.dlq. Consumers still require Inbox idempotency and explicit retry policies.

## Observability

Applications send OTLP to otel-collector:4317. The collector exports metrics to Prometheus, logs to Loki and traces to Tempo. Grafana provisions all datasources and a minimal WMS Overview dashboard. Tokens, unrestricted payloads and personal data are forbidden in telemetry.

## Backup and restore

Run pwsh ./scripts/backup.ps1 to create a PostgreSQL custom-format dump and SHA-256 manifest under ignored .backups. Restore is restricted to development or staging and requires the explicit -ConfirmRestore switch. The restore script verifies the manifest when available, stops Keycloak, restores with exit-on-error, removes temporary files and restarts Keycloak.

Logical dumps support pilot recovery tests but do not meet a five-minute production RPO alone. Production also needs WAL archiving or managed snapshots. Record backup time, checksum, restore duration and smoke-test evidence.

## Blue/green

Set WMS_API_IMAGE and optionally WMS_WORKER_IMAGE in the environment file. Run pwsh ./scripts/blue-green.ps1 -TargetSlot green -ImageTag <immutable-tag>. The script starts the target profile, polls /health/ready and preserves the previous slot if readiness fails. A successful run records the slot under ignored .runtime and reports port 18080 for blue or 18081 for green. The external load balancer switches only after success.

Database changes must use backward-compatible expand/contract migrations. Do not roll back data migrations by restoring a backup during a normal application rollback.

## Troubleshooting

Use docker compose logs --tail 200 <service> and preserve the CorrelationId and timestamps. Validate configuration with docker compose --env-file .env.example config. Do not delete volumes as a troubleshooting shortcut; capture logs and recreate only the affected stateless service.
