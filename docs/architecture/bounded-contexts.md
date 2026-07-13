# Mapa de bounded contexts

| Contexto | Autoridad | Publica | Consume |
|---|---|---|---|
| Plataforma/Tenancy | Tenant, configuración, feature flags | TenantActivated, ConfigurationChanged | Ninguno del dominio |
| Seguridad/Auditoría | Políticas locales, dispositivos, registro inmutable | DeviceRegistered, AuditRecorded | Identidad externa |
| Layout | Almacén, zona, ubicación y staging | LocationChanged | Configuración tenant |
| Maestros | SKU, UOM, owner y códigos de barras mínimos | SkuChanged | Datos canónicos ERP |
| Inventario | Movimiento, saldo, reserva y disponibilidad | StockChanged, StockReserved, StockReleased | Layout/Maestros por contrato |
| Inbound | ASN, recepción, diferencia y putaway | ReceiptCompleted, PutawayCompleted | ASN canónico, inventario, tareas |
| Outbound | Pedido, asignación, picking, packing y despacho | ShipmentDispatched, ShortPickRecorded | Pedido canónico, inventario, tareas |
| Task Execution | Tarea, asignación, secuencia y resultado | TaskAssigned, TaskCompleted, TaskFailed | Solicitudes de Inbound/Outbound |
| Integración | Envelope, Inbox/Outbox, entrega, retry y reconciliación | Mensajes canónicos externos | Eventos internos |
| Mobile Sync | Bootstrap, checkpoint, comando y conflicto | MobileCommandProcessed | Tareas y APIs públicas del dominio |

## Contextos futuros

Replenishment, Counting, Returns, Quality, HU/GS1, Slotting, Labor, Yard, 3PL, VAS, Production, Automation y Control Tower sólo conservan nombre, dependencia prevista y fase en el roadmap. No deben contaminar contratos MVP.

## Política de evolución

- Un evento pertenece al contexto que confirma el hecho.
- Los consumidores toleran campos nuevos y rechazan versiones mayores no compatibles.
- Una necesidad de lectura cruzada se satisface con proyección o API, no con SQL cruzado.
- Una transacción no abarca broker ni ERP: confirma localmente y publica mediante Outbox.
- Saga se usa sólo si aparece una operación distribuida con compensación explícita.
