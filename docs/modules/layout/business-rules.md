# Reglas de negocio — Layout

| ID | Regla | Verificación |
|---|---|---|
| RULE-LAY-0001 | Toda mutación exige TenantId, actor, correlación y versión esperada cuando corresponda. | TEST-LAY-0001 |
| RULE-LAY-0002 | Un identificador idempotente repetido devuelve el resultado persistido y no repite efectos/eventos. | TEST-LAY-0002 |
| RULE-LAY-0003 | Sólo transiciones declaradas son válidas; un conflicto no se corrige silenciosamente. | TEST-LAY-0003 |
| RULE-LAY-0004 | Las escrituras ocurren únicamente en `layout`; otros módulos se consumen por contrato. | TEST-LAY-0004 |

Incumplir una regla produce Problem Details con código estable `LAY_RULE_VIOLATION` y auditoría sin datos sensibles.
