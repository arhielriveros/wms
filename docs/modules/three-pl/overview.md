# 3PL

## Alcance futuro

soportar contratos multi-owner, tarifas y segregación operativa 3PL. La capacidad se rastreará como `FEATURE-TPL-0001` cuando se active; no forma parte del MVP.

## Dependencias

Tenancy, Inventario, Integración y billing futuro. Consumirá contratos públicos y eventos versionados; no leerá schemas de otros módulos.

## Roadmap

- Fase 3: reglas por propietario; posterior: facturación configurable.
- Antes de activarlo se completará el dossier modular, ADR afectados, threat model, UX, contratos y plan de pruebas.
- Gate: evidencia funcional, seguridad, rendimiento, observabilidad, migración y trazabilidad aprobadas.

## Límites actuales

No se crean APIs, tablas, eventos ni permisos ejecutables en Hito 0. Este archivo no autoriza scaffolding ni decisiones anticipadas.
