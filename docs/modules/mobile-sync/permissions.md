# Permisos — Mobile Sync

| ID | Acción | Scopes requeridos |
|---|---|---|
| PERM-SYN-0001 | ejecutar UC-SYN-0001 | tenant + warehouse/owner/zone aplicable + `syn:write` |
| PERM-SYN-0002 | consultar | tenant + `syn:read` |
| PERM-SYN-0003 | administrar/reprocesar | rol supervisor/admin + `syn:admin` |

Autorización deniega por defecto y combina rol con scopes del token. El backend vuelve a validar alcance; la UI nunca es control de seguridad. Toda denegación genera decisión auditada con motivo estable.
