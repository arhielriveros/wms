# Inventario

## Propósito

registrar movimientos append-only y mantener saldos consistentes y reservables. Este dossier implementa FEATURE-INV-0001 para el MVP.

## Alcance MVP

- Caso principal: UC-INV-0001, donde módulo operativo autorizado puede aplicar un movimiento o reserva.
- Autoridad: el WMS controla el estado operativo; fuentes externas sólo ingresan por contratos de Integración.
- Dependencias: Tenancy, Layout, Maestros y Seguridad.
- Fuera de alcance: optimización avanzada, extensiones de Fase 2 y escritura directa entre schemas.

## Invariantes

- `TenantId`, `CorrelationId` y actor son obligatorios.
- RULE-INV-0001 y sus pruebas bloquean el release si fallan.
- Bloqueo por dimensión Tenant/Warehouse/Owner/SKU/Location/Status y control de versión.
