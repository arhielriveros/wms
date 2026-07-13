# Slotting

## Alcance futuro

recomendar ubicación de SKU según rotación, ergonomía y capacidad. La capacidad se rastreará como `FEATURE-SLO-0001` cuando se active; no forma parte del MVP.

## Dependencias

Layout, Inventario, Maestros y analítica histórica. Consumirá contratos públicos y eventos versionados; no leerá schemas de otros módulos.

## Roadmap

- Fase 3: scoring explicable; posterior: optimización continua.
- Antes de activarlo se completará el dossier modular, ADR afectados, threat model, UX, contratos y plan de pruebas.
- Gate: evidencia funcional, seguridad, rendimiento, observabilidad, migración y trazabilidad aprobadas.

## Límites actuales

No se crean APIs, tablas, eventos ni permisos ejecutables en Hito 0. Este archivo no autoriza scaffolding ni decisiones anticipadas.
