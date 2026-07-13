# Modelo de dominio — Layout

## Agregado Warehouse

Protege RULE-LAY-0001; expone comandos explícitos y no setters públicos. Location pertenece al agregado o se referencia por ID, nunca mediante acceso a tablas ajenas.

## Servicios y puertos

- Servicio de aplicación: orquesta caso UC-LAY-0001, transacción, auditoría y Outbox.
- Puerto de lectura: proyección propia; Dapper sólo para consultas justificadas.
- Puerto externo: contrato versionado, con timeout y política de reintento fuera de la transacción.

Eventos de dominio se convierten en EVENT-LAY-0001; no se comparte el modelo EF entre módulos.
