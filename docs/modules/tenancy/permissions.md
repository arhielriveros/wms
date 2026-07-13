# Permisos — Tenancy

| ID | Acción | Scopes requeridos |
|---|---|---|
| PERM-TEN-0001 | ejecutar UC-TEN-0001 | tenant + warehouse/owner/zone aplicable + `ten:write` |
| PERM-TEN-0002 | consultar | tenant + `ten:read` |
| PERM-TEN-0003 | administrar/reprocesar | rol supervisor/admin + `ten:admin` |

Autorización deniega por defecto y combina rol con scopes del token. El backend vuelve a validar alcance; la UI nunca es control de seguridad. Toda denegación genera decisión auditada con motivo estable.
