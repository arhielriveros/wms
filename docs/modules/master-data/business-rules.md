# Reglas de negocio — Maestros

| ID | Regla | Verificación |
|---|---|---|
| RULE-MST-0001 | Toda mutación exige TenantId, actor, correlación y versión esperada cuando corresponda. | TEST-MST-0001 |
| RULE-MST-0002 | Un identificador idempotente repetido devuelve el resultado persistido y no repite efectos/eventos. | TEST-MST-0002 |
| RULE-MST-0003 | Sólo transiciones declaradas son válidas; un conflicto no se corrige silenciosamente. | TEST-MST-0003 |
| RULE-MST-0004 | Las escrituras ocurren únicamente en `master_data`; otros módulos se consumen por contrato. | TEST-MST-0004 |

Incumplir una regla produce Problem Details con código estable `MST_RULE_VIOLATION` y auditoría sin datos sensibles.
