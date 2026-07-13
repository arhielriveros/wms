# Task Execution

## Propósito

asignar y controlar tareas básicas de recepción, putaway y picking. Este dossier implementa FEATURE-TSK-0001 para el MVP.

## Alcance MVP

- Caso principal: UC-TSK-0001, donde supervisor y operario puede asignar o ejecutar una tarea.
- Autoridad: el WMS controla el estado operativo; fuentes externas sólo ingresan por contratos de Integración.
- Dependencias: Tenancy, Seguridad y módulos operativos.
- Fuera de alcance: optimización avanzada, extensiones de Fase 2 y escritura directa entre schemas.

## Invariantes

- `TenantId`, `CorrelationId` y actor son obligatorios.
- RULE-TSK-0001 y sus pruebas bloquean el release si fallan.
- Kernel básico sin optimización, interleaving ni balance avanzado.
