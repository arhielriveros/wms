# Permisos — Task Execution

| ID | Acción | Scopes requeridos |
|---|---|---|
| PERM-TSK-0001 | ejecutar UC-TSK-0001 | tenant + warehouse/owner/zone aplicable + `tsk:write` |
| PERM-TSK-0002 | consultar | tenant + `tsk:read` |
| PERM-TSK-0003 | administrar/reprocesar | rol supervisor/admin + `tsk:admin` |

Autorización deniega por defecto y combina rol con scopes del token. El backend vuelve a validar alcance; la UI nunca es control de seguridad. Toda denegación genera decisión auditada con motivo estable.
