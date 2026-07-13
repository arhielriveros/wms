# Modelo de seguridad

## Principios

Zero trust entre cliente y API, denegación por defecto, mínimo privilegio, separación tenant, doble control para acciones de alto impacto, secretos fuera de código/logs y auditoría no borrable desde la aplicación.

## Identidad

- Keycloak, OIDC/OAuth 2.1 Authorization Code + PKCE; MFA obligatorio para administradores, supervisores, integradores y soporte.
- Access token ≤10 min; refresh token rotativo y ligado a sesión/dispositivo; revocación central.
- Validar firma, algoritmo permitido, issuer, audience, expiración y `not-before`; rechazar tokens ambiguos.
- Dispositivo industrial registrado con identidad, estado y última sincronización. Pérdida/compromiso revoca sesión y limpia datos locales protegidos.

## Autorización

La decisión efectiva es `rol ∩ operación ∩ TenantId ∩ WarehouseId ∩ OwnerId ∩ ZoneId`. El contexto se obtiene del token y de recursos cargados por servidor; nunca de una afirmación no validada del body. Las políticas se evalúan en API y los jobs preservan actor original o identidad de servicio explícita.

## Multi-tenant

- `TenantId` obligatorio en toda entidad y mensaje multi-tenant.
- Query filters más RLS; el contexto de sesión PostgreSQL se establece por transacción y se limpia al devolver la conexión.
- Unique indexes incluyen `TenantId`; cachés, object keys, métricas y DLQ mantienen partición lógica.
- Backups globales identifican tenants; exportaciones son filtradas y auditadas.
- Pruebas con dos tenants cubren lectura, escritura, IDOR, búsquedas, exportación, evidencia y eventos.

## Protección de datos

TLS en tránsito; cifrado en reposo; secretos en secret manager; checksums para evidencias; PII minimizada; payloads sensibles redactados. Logs no incluyen tokens, contraseñas, firmas, secretos ni payload completo por defecto.

## Auditoría

Registrar UTC, actor, rol, dispositivo, IP, tenant, almacén, operación, entidad/ID, antes/después permitido, motivo, resultado, aprobador, CorrelationId y CausationId. Correcciones son nuevos registros. Acceso y exportación de auditoría también se auditan.

## Respuesta

Severidad, contención, preservación de evidencia, revocación, investigación, notificación, recuperación y retrospectiva se ejecutan según runbook. Incidentes de aislamiento tenant o integridad de stock son críticos y detienen despliegues.
