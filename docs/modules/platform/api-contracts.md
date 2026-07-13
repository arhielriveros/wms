# Contratos API — Plataforma

Base: `/api/v1/platform`; JSON UTF-8, OAuth bearer, `X-Correlation-Id` y `Idempotency-Key` en comandos.

| ID | Operación | Resultado |
|---|---|---|
| API-PLT-0001 | `POST /api/v1/platform` | 201/202 con `id`, `status`, `version`, `correlationId` |
| API-PLT-0002 | `GET /api/v1/platform/{id}` | 200 con proyección tenant-scoped; 404 sin filtrar existencia ajena |
| API-PLT-0003 | `GET /api/v1/platform?cursor=&limit=` | página estable, límite máximo 200 |

Errores usan `application/problem+json` con `type,title,status,code,traceId,correlationId,errors`. Cambios incompatibles requieren nueva versión; campos nuevos son opcionales.
