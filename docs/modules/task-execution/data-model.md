# Modelo de datos — Task Execution

Schema y tablas: `task_execution.task, task_execution.task_step`.

Campos comunes: `id uuid`, `tenant_id uuid not null`, `created_at timestamptz`, `created_by`, `correlation_id`, `version bigint`. Índices comienzan por `tenant_id`; claves naturales son únicas dentro de su almacén/propietario cuando aplique.

RLS usa el tenant de sesión y deniega por defecto. El rol del módulo sólo puede escribir su schema; migraciones usan un rol separado. FK físicas no cruzan schemas: se guardan IDs y se valida por API/evento.

Retención: entidades operativas según política; auditoría/movimientos son append-only. TEST-TSK-0004 prueba RLS y permisos de schema.
