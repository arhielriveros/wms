# ADR-0008 — Despliegue y recuperación del MVP

- **Fecha:** 2026-07-13
- **Estado:** Aceptado
- **Responsable:** DevOps y Operaciones

## Contexto

El piloto necesita despliegue repetible y recuperación probada, sin la carga operativa de una plataforma de orquestación prematura.

## Decisión

Docker Compose para desarrollo y piloto Linux; GitHub Actions para CI/CD; API y Worker en blue/green con migraciones compatibles hacia atrás y feature flags. PostgreSQL con backups y PITR. Objetivos: RPO 5 min, RTO 60 min.

## Alternativas

Kubernetes no aporta valor inicial proporcional; despliegue in-place eleva interrupción/rollback; canary se reserva para cuando haya tráfico suficiente.

## Consecuencias y riesgos

Capacidad horizontal y autohealing limitados frente a Kubernetes. Se mitiga con health checks, dos slots, rollback de aplicación, runbooks y simulacro de restauración. Una migración destructiva requiere estrategia expand/contract; rollback nunca depende de revertir datos destructivamente.
