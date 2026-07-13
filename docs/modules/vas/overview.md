# VAS

## Alcance futuro

orquestar servicios de valor agregado como etiquetado, kitting y reempaque. La capacidad se rastreará como `FEATURE-VAS-0001` cuando se active; no forma parte del MVP.

## Dependencias

Task Execution, Inventario, Maestros y Outbound. Consumirá contratos públicos y eventos versionados; no leerá schemas de otros módulos.

## Roadmap

- Fase 3: tareas VAS básicas; posterior: BOM y costos.
- Antes de activarlo se completará el dossier modular, ADR afectados, threat model, UX, contratos y plan de pruebas.
- Gate: evidencia funcional, seguridad, rendimiento, observabilidad, migración y trazabilidad aprobadas.

## Límites actuales

No se crean APIs, tablas, eventos ni permisos ejecutables en Hito 0. Este archivo no autoriza scaffolding ni decisiones anticipadas.
