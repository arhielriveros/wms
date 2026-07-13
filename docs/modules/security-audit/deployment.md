# Despliegue — Seguridad y Auditoría

El módulo se empaqueta dentro de API/worker del monolito modular. Configuración por ambiente, migraciones versionadas y feature flag `sec.enabled` permiten blue/green.

Orden: backup verificado → migración compatible hacia atrás → desplegar worker/API inactivos → smoke test API-SEC-0001 → habilitar tráfico → observar métricas. Rollback de binarios no revierte datos; migraciones destructivas requieren ciclo expand/contract.

Health checks distinguen liveness/readiness. RPO 5 min y RTO 60 min; restauración prueba schema, Outbox y coherencia. Kubernetes queda fuera del MVP.
