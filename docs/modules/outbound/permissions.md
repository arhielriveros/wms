# Permisos — Outbound

| ID | Acción | Scopes requeridos |
|---|---|---|
| PERM-OUT-0001 | ejecutar UC-OUT-0001 | tenant + warehouse/owner/zone aplicable + `out:write` |
| PERM-OUT-0002 | consultar | tenant + `out:read` |
| PERM-OUT-0003 | administrar/reprocesar | rol supervisor/admin + `out:admin` |

Autorización deniega por defecto y combina rol con scopes del token. El backend vuelve a validar alcance; la UI nunca es control de seguridad. Toda denegación genera decisión auditada con motivo estable.
