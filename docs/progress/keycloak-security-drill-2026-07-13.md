# Drill de seguridad Keycloak — 2026-07-13

## Resultado

`TEST-SEC-0002` quedó **APROBADO** contra Keycloak 26.1, PostgreSQL 17 y la API .NET 10 en un entorno Docker aislado. La ejecución final terminó el 2026-07-13 a las 21:42:46 UTC y no persistió credenciales, tokens ni secretos.

## Alcance ejecutado

El comando reproducible es:

```powershell
./scripts/keycloak-security-drill.ps1
```

El script crea un cliente confidencial efímero, configura los atributos operativos administrados, crea usuarios sintéticos para dos tenants, asigna roles mínimos y levanta la API con las cabeceras de desarrollo deshabilitadas. Al finalizar elimina contenedores, red y volumen del proyecto `wms-security`.

## Evidencia

| Control | HTTP observado | Resultado |
|---|---:|---|
| Petición sin autenticar | 401 | Aprobado |
| Supervisor con permiso de inventario | 200 | Aprobado |
| Escalada mediante cabeceras con usuario limitado | 403 | Aprobado |
| Tenant A intenta consultar recurso de tenant B | 404 | Aprobado |
| Tenant B consulta su propio recurso | 200 | Aprobado |
| Claim `tenant_id` adulterado sin nueva firma | 401 | Aprobado |
| Token activo antes de logout | 200 | Aprobado |
| Token revocado después de logout | 401 | Aprobado |

La validación del token combina RS256, issuer, audience, vigencia e introspección activa. La evidencia JSON redactada se genera bajo `.backups/wms-keycloak-security-*.json` y se publica como artefacto de CI por 30 días.

## Hallazgos corregidos durante el drill

- Los roles de cliente del realm se normalizaron bajo `roles.client`, formato aceptado por Keycloak 26.
- Los atributos `tenant_id`, `warehouse_ids`, `owner_ids` y `zone_ids` se declaran administrados y sólo editables/visibles por administradores.
- Los permisos se aceptan desde `scope` o desde `resource_access[wms-api].roles`; claims malformados no conceden acceso.
- La API usa introspección OIDC con `client_secret_post` y falla cerrada ante token inactivo o error del proveedor.
- La serialización de arrays para asignación de roles se corrigió para emitir un único array JSON.

## Criterio de aceptación

El gate local de IDOR, escalada, adulteración y revocación está cerrado. La certificación del piloto aún requiere repetirlo con la configuración, red, TLS y política MFA del ambiente objetivo.
