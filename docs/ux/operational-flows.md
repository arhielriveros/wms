# Flujos UX del MVP

## Inbound móvil

1. Ver tarea asignada y estado de conexión.
2. Escanear ASN/muelle o seleccionar tarea descargada.
3. Escanear ubicación staging, SKU y cantidad.
4. Mostrar diferencia antes de confirmar; exigir motivo cuando aplique.
5. Guardar comando local y distinguir `Pendiente de sync` de `Aceptado`.
6. Recibir ubicación recomendada, escanear origen/destino y confirmar putaway.

Objetivo: happy path sin teclado, máximo un toque de confirmación por paso. Scan inválido conserva contexto; duplicado no incrementa cantidad silenciosamente.

## Outbound móvil

1. Abrir tarea de picking asignada y ver secuencia.
2. Escanear ubicación origen → SKU → cantidad.
3. Confirmar pick o solicitar short pick con motivo/evidencia.
4. Sincronizar; conflicto detiene esa tarea y ofrece escalamiento.
5. Packing y despacho cambian a modo online y verifican pedido/paquete antes de acción irreversible.

## Consola web

- Home operacional: recepciones, putaway, pedidos, tareas, stock bloqueado, integraciones y dispositivos offline.
- Detalle ASN/pedido: timeline, líneas, cantidades, tareas, excepciones y CorrelationId.
- Inventario: saldo por dimensión y movimientos append-only enlazados.
- Integración: estado, intentos, payload redactado, latencia y reproceso autorizado.

## Mensajería

Los mensajes indican acción, causa y recuperación. Ejemplo: “No se confirmó el pick: el saldo cambió desde tu descarga. La tarea quedó pausada; sincroniza y solicita revisión.” Evitar “Error desconocido”; mostrar código seguro/CorrelationId para soporte.

## Confirmación por riesgo

Escaneo ordinario usa feedback inmediato; short pick/override exige motivo; despacho, cancelación, reproceso y cambio de permisos requieren resumen explícito y confirmación. Nunca usar confirmaciones repetitivas para acciones reversibles.
