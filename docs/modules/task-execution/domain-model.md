# Modelo de dominio — Task Execution

## Agregado WarehouseTask

Protege RULE-TSK-0001; expone comandos explícitos y no setters públicos. TaskStep pertenece al agregado o se referencia por ID, nunca mediante acceso a tablas ajenas.

## Servicios y puertos

- Servicio de aplicación: orquesta caso UC-TSK-0001, transacción, auditoría y Outbox.
- Puerto de lectura: proyección propia; Dapper sólo para consultas justificadas.
- Puerto externo: contrato versionado, con timeout y política de reintento fuera de la transacción.

Eventos de dominio se convierten en EVENT-TSK-0001; no se comparte el modelo EF entre módulos.
