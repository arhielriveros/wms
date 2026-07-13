# Permisos — Inbound

| ID | Acción | Scopes requeridos |
|---|---|---|
| PERM-INB-0001 | ejecutar UC-INB-0001 | tenant + warehouse/owner/zone aplicable + `inb:write` |
| PERM-INB-0002 | consultar | tenant + `inb:read` |
| PERM-INB-0003 | administrar/reprocesar | rol supervisor/admin + `inb:admin` |

Autorización deniega por defecto y combina rol con scopes del token. El backend vuelve a validar alcance; la UI nunca es control de seguridad. Toda denegación genera decisión auditada con motivo estable.
