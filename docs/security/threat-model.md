# Threat model

## Activos críticos

Integridad de stock, aislamiento tenant, credenciales/tokens, evidencia/auditoría, documentos comerciales, disponibilidad operativa y capacidad de despacho.

| ID | Amenaza | Impacto | Control preventivo | Detección/prueba |
|---|---|---|---|---|
| SEC-WMS-0001 | IDOR o fuga entre tenants | Crítico | RLS, scopes, IDs opacos | Test anti-leak en API/DB |
| SEC-WMS-0002 | Replay de comando/mensaje | Alto | CommandId/MessageId e Inbox | Test duplicado/concurrencia |
| SEC-WMS-0003 | Sobreasignación concurrente | Crítico | lock, versión, constraint | stress sobre dimensión común |
| SEC-WMS-0004 | Token robado | Alto | token corto, PKCE, rotación, revocación | sesión revocada y anomalías |
| SEC-WMS-0005 | Webhook falsificado | Alto | HMAC, timestamp, nonce | firma/tolerancia/replay |
| SEC-WMS-0006 | Inyección/SSRF/XSS/CSRF | Alto | parametrización, allowlist, CSP, SameSite | OWASP automation/manual |
| SEC-WMS-0007 | Secreto/PII en log | Alto | redacción y schema de logging | escaneo de logs/artefactos |
| SEC-WMS-0008 | Dispositivo perdido offline | Alto | cifrado local, expiración, revocación | prueba remota y TTL |
| SEC-WMS-0009 | Elevación de rol/SoD | Alto | políticas server-side y aprobación | test de claims manipulados |
| SEC-WMS-0010 | Borrado/alteración de auditoría | Crítico | append-only, rol separado, backup | prueba de permiso e integridad |
| SEC-WMS-0011 | DLQ/payload como exfiltración | Alto | cifrado, redacción, RBAC | revisión de consola/DLQ |
| SEC-WMS-0012 | Dependencia/imagen comprometida | Alto | lockfiles, SBOM, firma, scanning | gate CI y verificación deploy |

## Riesgo residual aceptado en piloto

La operación offline acepta una ventana limitada sin revocación inmediata. Se reduce mediante tareas preasignadas, expiración, mínimos datos locales, cifrado y prohibición de packing/despacho offline. La aceptación requiere owner y revisión antes de UAT.

## Checklist mínimo

OWASP ASVS/API Top 10, hardening de Keycloak/PostgreSQL/RabbitMQ/Redis/S3, rate limiting por identidad/tenant, CORS explícito, CSP, headers seguros, backups cifrados, SBOM y análisis de dependencias/imágenes.
