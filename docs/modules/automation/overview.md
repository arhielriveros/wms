# Automation

## Alcance futuro

integrar conveyors, sorters, AS/RS y controladores mediante adaptadores seguros. La capacidad se rastreará como `FEATURE-AUT-0001` cuando se active; no forma parte del MVP.

## Dependencias

Task Execution, Integración, Inventario y observabilidad. Consumirá contratos públicos y eventos versionados; no leerá schemas de otros módulos.

## Roadmap

- Fase 4: abstracción de equipos; posterior: orquestación resiliente.
- Antes de activarlo se completará el dossier modular, ADR afectados, threat model, UX, contratos y plan de pruebas.
- Gate: evidencia funcional, seguridad, rendimiento, observabilidad, migración y trazabilidad aprobadas.

## Límites actuales

No se crean APIs, tablas, eventos ni permisos ejecutables en Hito 0. Este archivo no autoriza scaffolding ni decisiones anticipadas.
