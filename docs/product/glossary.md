# Glosario WMS

| Término | Definición normativa |
|---|---|
| ASN | Aviso anticipado de envío recibido desde un sistema externo |
| Allocation / asignación | Selección concreta de stock para satisfacer una línea de pedido |
| Reserva | Compromiso de cantidad que reduce disponibilidad sin movimiento físico |
| Disponible | Cantidad física elegible menos reservas y bloqueos aplicables |
| Movimiento | Hecho inmutable que representa un cambio físico o de estado del stock |
| Saldo | Proyección transaccional mutable derivada de movimientos válidos |
| Dimensión de stock | Tenant + almacén + propietario + SKU + ubicación + estado; extensible a lote/serie/HU |
| Putaway | Traslado dirigido desde recepción/staging a ubicación de almacenamiento |
| Picking | Extracción de stock asignado para un pedido |
| Short pick | Confirmación controlada de una cantidad menor a la asignada |
| Packing | Verificación y conformación de paquetes antes del despacho |
| Despacho | Confirmación irreversible de salida física autorizada |
| Tarea | Unidad asignable y secuenciada de trabajo físico |
| ERP | Sistema empresarial fuente de documentos comerciales y contables |
| Modelo canónico | Contrato neutral del WMS, independiente del ERP origen/destino |
| Outbox / Inbox | Registros transaccionales para publicación y consumo idempotente |
| DLQ | Cola de mensajes no procesables luego de la política de reintentos |
| CorrelationId | Identificador que une una operación de extremo a extremo |
| CausationId | Identificador del mensaje o comando que causó otro |
| RLS | Row-Level Security de PostgreSQL para aislamiento por tenant |
| Evidencia | Archivo o dato verificable asociado a una acción y almacenado de forma durable |
| Override | Desvío autorizado de una recomendación que exige motivo y auditoría |

Los estados `Disponible`, `Reservado`, `Bloqueado`, `En tránsito` y `Dañado` son los únicos activos en el MVP. Cuarentena, inspección, vencido y scrap se reservan para fases posteriores.
