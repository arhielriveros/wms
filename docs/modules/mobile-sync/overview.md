# Mobile Sync

## Propósito

sincronizar bootstrap, tareas asignadas y comandos offline en orden seguro. Este dossier implementa FEATURE-SYN-0001 para el MVP.

## Alcance MVP

- Caso principal: UC-SYN-0001, donde dispositivo Android registrado puede descargar tareas o enviar un batch.
- Autoridad: el WMS controla el estado operativo; fuentes externas sólo ingresan por contratos de Integración.
- Dependencias: Tenancy, Seguridad, Tareas y módulos operativos.
- Fuera de alcance: optimización avanzada, extensiones de Fase 2 y escritura directa entre schemas.

## Invariantes

- `TenantId`, `CorrelationId` y actor son obligatorios.
- RULE-SYN-0001 y sus pruebas bloquean el release si fallan.
- Orden por tarea y LocalSequence; jamás resuelve conflictos alterando stock automáticamente.
