# Modelo de datos — Mobile Sync

Schema y tablas: `mobile_sync.device, mobile_sync.command_inbox, mobile_sync.sync_checkpoint`.

Campos comunes: `id uuid`, `tenant_id uuid not null`, `created_at timestamptz`, `created_by`, `correlation_id`, `version bigint`. Índices comienzan por `tenant_id`; claves naturales son únicas dentro de su almacén/propietario cuando aplique.

RLS usa el tenant de sesión y deniega por defecto. El rol del módulo sólo puede escribir su schema; migraciones usan un rol separado. FK físicas no cruzan schemas: se guardan IDs y se valida por API/evento.

Retención: entidades operativas según política; auditoría/movimientos son append-only. TEST-SYN-0004 prueba RLS y permisos de schema.
