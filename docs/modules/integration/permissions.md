# Permisos — Integración

| ID | Acción | Scopes requeridos |
|---|---|---|
| PERM-INT-0001 | ejecutar UC-INT-0001 | tenant + warehouse/owner/zone aplicable + `int:write` |
| PERM-INT-0002 | consultar | tenant + `int:read` |
| PERM-INT-0003 | administrar/reprocesar | rol supervisor/admin + `int:admin` |

Autorización deniega por defecto y combina rol con scopes del token. El backend vuelve a validar alcance; la UI nunca es control de seguridad. Toda denegación genera decisión auditada con motivo estable.
