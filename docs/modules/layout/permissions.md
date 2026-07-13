# Permisos — Layout

| ID | Acción | Scopes requeridos |
|---|---|---|
| PERM-LAY-0001 | ejecutar UC-LAY-0001 | tenant + warehouse/owner/zone aplicable + `lay:write` |
| PERM-LAY-0002 | consultar | tenant + `lay:read` |
| PERM-LAY-0003 | administrar/reprocesar | rol supervisor/admin + `lay:admin` |

Autorización deniega por defecto y combina rol con scopes del token. El backend vuelve a validar alcance; la UI nunca es control de seguridad. Toda denegación genera decisión auditada con motivo estable.
