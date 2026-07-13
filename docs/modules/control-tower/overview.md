# Control Tower

## Alcance futuro

ofrecer visibilidad multi-almacén, alertas, KPI y gestión de excepciones. La capacidad se rastreará como `FEATURE-CTL-0001` cuando se active; no forma parte del MVP.

## Dependencias

todos los módulos mediante eventos y proyecciones de lectura. Consumirá contratos públicos y eventos versionados; no leerá schemas de otros módulos.

## Roadmap

- Fase 4: dashboards y alertas; posterior: simulación y recomendaciones.
- Antes de activarlo se completará el dossier modular, ADR afectados, threat model, UX, contratos y plan de pruebas.
- Gate: evidencia funcional, seguridad, rendimiento, observabilidad, migración y trazabilidad aprobadas.

## Límites actuales

No se crean APIs, tablas, eventos ni permisos ejecutables en Hito 0. Este archivo no autoriza scaffolding ni decisiones anticipadas.
