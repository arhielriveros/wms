# Matriz de autoridad ERP/WMS

| Entidad/campo | ERP | WMS | Dirección | Regla de conflicto |
|---|---|---|---|---|
| Cliente/proveedor fiscal | Autoridad | Copia mínima | ERP → WMS | ERP prevalece; no bloquea tarea ya asignada |
| Documento comercial y líneas | Autoridad hasta aceptación | Snapshot operacional | ERP → WMS | cambios posteriores son nueva versión/comando |
| Precio, impuesto, pago, contabilidad | Autoridad | No almacena salvo referencia | ERP → WMS | fuera del dominio WMS |
| SKU/UOM/código externo | Autoridad de alta | Copia canónica operativa | ERP → WMS | cambio incompatible requiere revisión |
| Barcode operativo adicional | Referencia opcional | Autoridad | WMS; confirmación opcional | WMS prevalece en operación |
| Layout/ubicación/zona/staging | Sin autoridad | Autoridad | WMS | ERP no modifica |
| Estado físico detallado | Resumen eventual | Autoridad | WMS → ERP | WMS prevalece; reconciliar diferencia |
| Movimiento físico | Confirmación resumida | Autoridad append-only | WMS → ERP | nunca se borra/edita; se compensa |
| Reserva/asignación/picking/tarea | Sin autoridad operacional | Autoridad | WMS | ERP cancela mediante comando sujeto a estado |
| Recepción/despacho confirmado | Documento contable posterior | Autoridad del hecho físico | WMS → ERP | fallo ERP no revierte el hecho físico |
| Evidencia/auditoría/excepción | Referencia | Autoridad | WMS | inmutable según retención |

## Propiedad de campos

Cada adaptador mapea desde/hacia modelos canónicos. Un `externalId` identifica la referencia del sistema origen, pero no sustituye el UUID WMS. La reconciliación compara hechos y estados; jamás “corrige” stock mediante escritura directa.
