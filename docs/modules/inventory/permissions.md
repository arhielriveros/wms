# Permisos — Inventario

| ID | Acción | Scopes requeridos |
|---|---|---|
| PERM-INV-0001 | ejecutar UC-INV-0001 | tenant + warehouse/owner/zone aplicable + `inv:write` |
| PERM-INV-0002 | consultar | tenant + `inv:read` |
| PERM-INV-0003 | administrar/reprocesar | rol supervisor/admin + `inv:admin` |

Autorización deniega por defecto y combina rol con scopes del token. El backend vuelve a validar alcance; la UI nunca es control de seguridad. Toda denegación genera decisión auditada con motivo estable.
