# Modelo de dominio — Seguridad y Auditoría

## Agregado AccessDecision

Protege RULE-SEC-0001; expone comandos explícitos y no setters públicos. AuditEntry pertenece al agregado o se referencia por ID, nunca mediante acceso a tablas ajenas.

## Servicios y puertos

- Servicio de aplicación: orquesta caso UC-SEC-0001, transacción, auditoría y Outbox.
- Puerto de lectura: proyección propia; Dapper sólo para consultas justificadas.
- Puerto externo: contrato versionado, con timeout y política de reintento fuera de la transacción.

Eventos de dominio se convierten en EVENT-SEC-0001; no se comparte el modelo EF entre módulos.
