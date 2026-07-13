# Contratos API — Tenancy

Base: `/api/v1/tenants`; JSON UTF-8, OAuth bearer, `X-Correlation-Id` y `Idempotency-Key` en comandos.

| ID | Operación | Resultado |
|---|---|---|
| API-TEN-0001 | `POST /api/v1/tenants` | 201/202 con `id`, `status`, `version`, `correlationId` |
| API-TEN-0002 | `GET /api/v1/tenants/{id}` | 200 con proyección tenant-scoped; 404 sin filtrar existencia ajena |
| API-TEN-0003 | `GET /api/v1/tenants?cursor=&limit=` | página estable, límite máximo 200 |

Errores usan `application/problem+json` con `type,title,status,code,traceId,correlationId,errors`. Cambios incompatibles requieren nueva versión; campos nuevos son opcionales.
