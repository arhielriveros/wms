# Replenishment

## Alcance futuro

crear reposiciones desde reserva o mínimos hacia ubicaciones de picking. La capacidad se rastreará como `FEATURE-REP-0001` cuando se active; no forma parte del MVP.

## Dependencias

Inventario, Layout, Maestros y Task Execution. Consumirá contratos públicos y eventos versionados; no leerá schemas de otros módulos.

## Roadmap

- Fase 2: reglas min/max, demanda y tareas; posterior: optimización predictiva.
- Antes de activarlo se completará el dossier modular, ADR afectados, threat model, UX, contratos y plan de pruebas.
- Gate: evidencia funcional, seguridad, rendimiento, observabilidad, migración y trazabilidad aprobadas.

## Límites actuales

No se crean APIs, tablas, eventos ni permisos ejecutables en Hito 0. Este archivo no autoriza scaffolding ni decisiones anticipadas.
