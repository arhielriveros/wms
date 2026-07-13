# Permisos — Seguridad y Auditoría

| ID | Acción | Scopes requeridos |
|---|---|---|
| PERM-SEC-0001 | ejecutar UC-SEC-0001 | tenant + warehouse/owner/zone aplicable + `sec:write` |
| PERM-SEC-0002 | consultar | tenant + `sec:read` |
| PERM-SEC-0003 | administrar/reprocesar | rol supervisor/admin + `sec:admin` |

Autorización deniega por defecto y combina rol con scopes del token. El backend vuelve a validar alcance; la UI nunca es control de seguridad. Toda denegación genera decisión auditada con motivo estable.
