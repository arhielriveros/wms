# Matriz de autorización

## Roles MVP

| Operación | Operario | Supervisor | Jefe | Admin | Integrador | Auditor | Soporte |
|---|---:|---:|---:|---:|---:|---:|---:|
| Ver/ejecutar tarea asignada | Sí, scope propio | Sí | Sí | No | No | Lectura | Lectura |
| Recibir/putaway/pick | Sí, asignado | Sí | Sí | No | No | Lectura | No |
| Short pick / excepción | Solicita | Aprueba | Aprueba | No | No | Lectura | No |
| Packing/despacho online | Según asignación | Sí | Sí | No | No | Lectura | No |
| Reasignar/cancelar tarea | No | Sí, zona | Sí | No | No | Lectura | No |
| Configurar layout/maestros | No | Lectura | Lectura | Sí | No | Lectura | No |
| Ver payload/reprocesar integración | No | Estado | Estado | No | Sí | Lectura redactada | Sí, con ticket |
| Administrar usuarios/roles/flags | No | No | No | Sí | No | Lectura | No |
| Ver/exportar auditoría | Propia | Scope | Scope | Scope | Integración | Sí | Con aprobación |

“Sí” siempre está limitado por tenant y scopes. Un rol no confiere acceso fuera de su almacén/owner/zona. Despacho, reproceso, override, exportación y cambios de permisos exigen motivo; cambios de rol privilegiado requieren segregación solicitante/aprobador.

## Permisos canónicos

`wms.task.read_assigned`, `wms.task.execute`, `wms.task.reassign`, `wms.receipt.execute`, `wms.putaway.execute`, `wms.pick.execute`, `wms.short_pick.approve`, `wms.pack.execute`, `wms.shipment.dispatch`, `wms.inventory.read`, `wms.layout.manage`, `wms.master.manage`, `wms.integration.read`, `wms.integration.reprocess`, `wms.audit.read`, `wms.security.manage`.
