# Reglas de negocio — Seguridad y Auditoría

| ID | Regla | Verificación |
|---|---|---|
| RULE-SEC-0001 | Toda mutación exige TenantId, actor, correlación y versión esperada cuando corresponda. | TEST-SEC-0001 |
| RULE-SEC-0002 | Un identificador idempotente repetido devuelve el resultado persistido y no repite efectos/eventos. | TEST-SEC-0002 |
| RULE-SEC-0003 | Sólo transiciones declaradas son válidas; un conflicto no se corrige silenciosamente. | TEST-SEC-0003 |
| RULE-SEC-0004 | Las escrituras ocurren únicamente en `security`; otros módulos se consumen por contrato. | TEST-SEC-0004 |

Incumplir una regla produce Problem Details con código estable `SEC_RULE_VIOLATION` y auditoría sin datos sensibles.
