# Contratos API — Layout

Base: `/api/v1/layout/locations`; JSON UTF-8, OAuth bearer, `X-Correlation-Id` y `Idempotency-Key` en comandos.

| ID | Operación | Resultado |
|---|---|---|
| API-LAY-0001 | `POST /api/v1/layout/locations` | 201/202 con `id`, `status`, `version`, `correlationId` |
| API-LAY-0002 | `GET /api/v1/layout/locations/{id}` | 200 con proyección tenant-scoped; 404 sin filtrar existencia ajena |
| API-LAY-0003 | `GET /api/v1/layout/locations?cursor=&limit=` | página estable, límite máximo 200 |

Errores usan `application/problem+json` con `type,title,status,code,traceId,correlationId,errors`. Cambios incompatibles requieren nueva versión; campos nuevos son opcionales.
