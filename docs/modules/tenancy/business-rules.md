# Reglas de negocio — Tenancy

| ID | Regla | Verificación |
|---|---|---|
| RULE-TEN-0001 | Toda mutación exige TenantId, actor, correlación y versión esperada cuando corresponda. | TEST-TEN-0001 |
| RULE-TEN-0002 | Un identificador idempotente repetido devuelve el resultado persistido y no repite efectos/eventos. | TEST-TEN-0002 |
| RULE-TEN-0003 | Sólo transiciones declaradas son válidas; un conflicto no se corrige silenciosamente. | TEST-TEN-0003 |
| RULE-TEN-0004 | Las escrituras ocurren únicamente en `tenancy`; otros módulos se consumen por contrato. | TEST-TEN-0004 |

Incumplir una regla produce Problem Details con código estable `TEN_RULE_VIOLATION` y auditoría sin datos sensibles.
