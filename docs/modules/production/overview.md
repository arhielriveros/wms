# Production

## Alcance futuro

conectar consumo y producción ligera sin reemplazar un MES. La capacidad se rastreará como `FEATURE-PRD-0001` cuando se active; no forma parte del MVP.

## Dependencias

Inventario, Maestros, Task Execution e Integración. Consumirá contratos públicos y eventos versionados; no leerá schemas de otros módulos.

## Roadmap

- Fase 4: órdenes simples y backflush; posterior: integración MES.
- Antes de activarlo se completará el dossier modular, ADR afectados, threat model, UX, contratos y plan de pruebas.
- Gate: evidencia funcional, seguridad, rendimiento, observabilidad, migración y trazabilidad aprobadas.

## Límites actuales

No se crean APIs, tablas, eventos ni permisos ejecutables en Hito 0. Este archivo no autoriza scaffolding ni decisiones anticipadas.
