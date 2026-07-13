# Reglas de negocio — Plataforma

| ID | Regla | Verificación |
|---|---|---|
| RULE-PLT-0001 | Toda mutación exige TenantId, actor, correlación y versión esperada cuando corresponda. | TEST-PLT-0001 |
| RULE-PLT-0002 | Un identificador idempotente repetido devuelve el resultado persistido y no repite efectos/eventos. | TEST-PLT-0002 |
| RULE-PLT-0003 | Sólo transiciones declaradas son válidas; un conflicto no se corrige silenciosamente. | TEST-PLT-0003 |
| RULE-PLT-0004 | Las escrituras ocurren únicamente en `platform`; otros módulos se consumen por contrato. | TEST-PLT-0004 |

Incumplir una regla produce Problem Details con código estable `PLT_RULE_VIOLATION` y auditoría sin datos sensibles.
