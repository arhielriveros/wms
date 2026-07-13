# Returns

## Alcance futuro

recibir devoluciones, inspeccionar disposición y reintegrar o bloquear stock. La capacidad se rastreará como `FEATURE-RET-0001` cuando se active; no forma parte del MVP.

## Dependencias

Inbound, Outbound, Inventario, Calidad futura e Integración. Consumirá contratos públicos y eventos versionados; no leerá schemas de otros módulos.

## Roadmap

- Fase 2: RMA y disposición básica; posterior: grading y reacondicionamiento.
- Antes de activarlo se completará el dossier modular, ADR afectados, threat model, UX, contratos y plan de pruebas.
- Gate: evidencia funcional, seguridad, rendimiento, observabilidad, migración y trazabilidad aprobadas.

## Límites actuales

No se crean APIs, tablas, eventos ni permisos ejecutables en Hito 0. Este archivo no autoriza scaffolding ni decisiones anticipadas.
