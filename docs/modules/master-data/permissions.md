# Permisos — Maestros

| ID | Acción | Scopes requeridos |
|---|---|---|
| PERM-MST-0001 | ejecutar UC-MST-0001 | tenant + warehouse/owner/zone aplicable + `mst:write` |
| PERM-MST-0002 | consultar | tenant + `mst:read` |
| PERM-MST-0003 | administrar/reprocesar | rol supervisor/admin + `mst:admin` |

Autorización deniega por defecto y combina rol con scopes del token. El backend vuelve a validar alcance; la UI nunca es control de seguridad. Toda denegación genera decisión auditada con motivo estable.
