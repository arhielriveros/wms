# Visión y alcance

## Visión

Entregar un WMS comercializable, auditable y evolutivo que controle la ejecución física del almacén sin depender del modelo interno de ningún ERP. El piloto debe demostrar continuidad operativa ante pérdida de conectividad y caída temporal del ERP, preservando siempre la integridad del inventario.

## Personas y necesidades

| Actor | Necesidad principal |
|---|---|
| Operario | Ejecutar tareas con escaneo, pocas pulsaciones y feedback inequívoco |
| Supervisor | Ver progreso, excepciones, stock y reasignar trabajo autorizado |
| Jefe de almacén | Controlar KPI, riesgos operativos y cumplimiento |
| Administrador | Configurar tenant, almacén, maestros, roles y flags |
| Integrador | Operar contratos, mapeos, reintentos y reconciliación |
| Auditor | Reconstruir quién hizo qué, cuándo, dónde y con qué resultado |
| Soporte | Diagnosticar usando correlación, métricas y evidencia sin alterar stock |

## Almacén de referencia

- Operación mediana, una sede activa, una zona de recepción/staging, ubicaciones de reserva y picking, packing y despacho.
- 1 tenant, 1 almacén y 1 propietario activados; estructura lógica multi-tenant/multi-warehouse/multi-owner.
- SKU en unidad simple (`EA`), barcode de SKU y ubicación, FIFO y estados básicos.
- Hasta 100 dispositivos Android industriales concurrentes, 30 usuarios web y ≥5 millones de movimientos históricos en pruebas.

## Dentro del MVP

1. Plataforma, tenancy, seguridad/auditoría, layout y maestros mínimos.
2. Ledger de movimientos append-only, saldos, reservas y control de concurrencia.
3. Inbound E2E con ASN, recepción, putaway y confirmación.
4. Outbound E2E con pedido, reserva FIFO, picking discreto, packing, despacho y confirmación.
5. Kernel básico de tareas, sincronización móvil, integración REST genérica y consola de supervisión.

## Fuera del MVP

Lotes, series, HU, GS1 avanzado, calidad avanzada, olas/waveless, reposición, devoluciones, conteos productivos, slotting, labor, yard, 3PL billing, VAS, producción, automatización e IA. Sus fronteras se preservan en el roadmap, sin implementación anticipada.

## KPI y objetivos

| Indicador | Objetivo inicial |
|---|---|
| Integridad de inventario | 0 sobreasignaciones y 0 mutaciones sin movimiento/auditoría |
| API interactiva | p95 < 500 ms bajo carga objetivo |
| Sync móvil | batch de 100 comandos < 10 s |
| Disponibilidad piloto | 99,5 % mensual |
| Recuperación | RPO ≤ 5 min; RTO ≤ 60 min |
| Idempotencia | un único efecto por MessageId/CommandId |
| Trazabilidad | 100 % de reglas del incremento enlazadas a prueba y contrato |

## Criterio de éxito

Los slices inbound y outbound operan en happy path, desconexión/reconexión, duplicados, concurrencia y caída del ERP sin corrupción; la evidencia permite reconstruir cada decisión y cada efecto.
