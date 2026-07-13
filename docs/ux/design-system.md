# Design system industrial

## Principios

Claridad bajo presión, escaneo primero, baja carga cognitiva, acciones explícitas, consistencia web/móvil y confirmación proporcional al impacto. El estado operacional nunca depende sólo del color.

## Tokens iniciales

| Token | Valor | Uso |
|---|---|---|
| `color.surface` | `#E9EEF2` | concreto frío de baja fatiga |
| `color.surface.strong` | `#D5DEE5` | carriles, filtros y agrupación |
| `color.text` | `#142027` | grafito estructural |
| `color.primary` | `#2358A6` | acción y flujo operacional |
| `color.info` | `#167A8B` | información/sincronización |
| `color.success` | `#1F7A52` | confirmación |
| `color.warning` | `#B86E00` | ámbar de seguridad |
| `color.danger` | `#B43A32` | error/acción destructiva |
| `space.1..6` | 4, 8, 12, 16, 24, 32 px | espaciado |
| `radius` | 6 px | controles industriales |
| `touch.min` | 48×48 dp | uso con guantes |

`Bahnschrift`, `Arial Narrow` o sans condensada del sistema para datos, códigos y etiquetas; sans del sistema para lectura. Mínimo 16 px/dp móvil y 14 px web salvo metadatos. Números de cantidad usan tabular figures. Focus visible de 2 px; contraste WCAG 2.2 AA.

## Firma visual

La consola usa un **riel operacional** horizontal inspirado en el recorrido físico del almacén. Sus estaciones — ASN, staging, stock, reserva, picking y despacho — muestran volumen, riesgo y estado sin convertir el dashboard en una colección genérica de tarjetas. El riel es información navegable, no decoración, y se reduce a una secuencia vertical en pantallas angostas.

## Componentes

Web: app shell, tabla operativa virtualizable, KPI, filtros persistentes, timeline, status badge con icono/texto, formulario tipado, dialog/drawer, banner, toast y panel de excepción. Mobile: task card, scan target, quantity stepper, confirm bar, offline banner, sync queue, conflict sheet y feedback sonoro/háptico.

## Estados obligatorios

| Estado | Tratamiento |
|---|---|
| Loading | skeleton/progreso y acción bloqueada justificada |
| Empty | motivo, alcance y siguiente acción |
| Error | qué ocurrió, dato preservado, retry seguro y CorrelationId |
| Offline | banner persistente, pendientes y última sincronización |
| Sin permisos | operación negada, alcance y canal de solicitud |
| Datos parciales | fuente/ventana faltante y nivel de confianza |
| Conflict | bloquear avance, explicar conflicto y escalar |
| Success | confirmación breve con entidad/cantidad |
| Warning | riesgo y consecuencia antes de confirmar |
| Maintenance | ventana, impacto y alternativa |

## Accesibilidad y validación

Teclado completo en web, labels programáticos, errores asociados al campo, live regions para sync/scan, reduced motion, targets táctiles, contraste AA y no depender de audio/vibración. Se prueba con lector, teclado, sol, modo oscuro, guantes y Zebra físico.
