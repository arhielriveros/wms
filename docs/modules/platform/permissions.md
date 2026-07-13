# Permisos — Plataforma

| ID | Acción | Scopes requeridos |
|---|---|---|
| PERM-PLT-0001 | ejecutar UC-PLT-0001 | tenant + warehouse/owner/zone aplicable + `plt:write` |
| PERM-PLT-0002 | consultar | tenant + `plt:read` |
| PERM-PLT-0003 | administrar/reprocesar | rol supervisor/admin + `plt:admin` |

Autorización deniega por defecto y combina rol con scopes del token. El backend vuelve a validar alcance; la UI nunca es control de seguridad. Toda denegación genera decisión auditada con motivo estable.
