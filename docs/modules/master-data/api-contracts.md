# Contratos API — Maestros

Base: `/api/v1/master-data/skus`; JSON UTF-8, OAuth bearer, `X-Correlation-Id` y `Idempotency-Key` en comandos.

| ID | Operación | Resultado |
|---|---|---|
| API-MST-0001 | `POST /api/v1/master-data/skus` | 201/202 con `id`, `status`, `version`, `correlationId` |
| API-MST-0002 | `GET /api/v1/master-data/skus/{id}` | 200 con proyección tenant-scoped; 404 sin filtrar existencia ajena |
| API-MST-0003 | `GET /api/v1/master-data/skus?cursor=&limit=` | página estable, límite máximo 200 |

Errores usan `application/problem+json` con `type,title,status,code,traceId,correlationId,errors`. Cambios incompatibles requieren nueva versión; campos nuevos son opcionales.
