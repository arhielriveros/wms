# Preparación UAT física Zebra y ergonomía móvil

## Propósito y límite

Este protocolo prepara `TEST-MOB-0003` para hardware real sin confundir emulador, revisión de código o evidencia automatizada con certificación física. La ejecución requiere resolver `BLK-UAT-0001`; hasta entonces el estado es **Preparado, no ejecutado**.

## Precondiciones

- Registrar modelo, Android, parche de seguridad, versión DataWedge, resolución, batería y accesorios de cada dispositivo.
- Asociar el profile DataWedge a `com.wms.mobile`, acción `com.wms.mobile.SCAN`, entrega Broadcast y extra `com.symbol.datawedge.data_string`.
- Cargar build identificable por versión/commit, tenant y almacén de UAT; no usar credenciales productivas.
- Disponer de Wi-Fi caracterizado en staging, pasillos, ubicaciones altas y muelle; anotar zonas sin cobertura y latencia.
- Preparar barcodes sintéticos de SKU/ubicación, tareas Receive/Putaway/Pick asignadas y un conflicto de versión controlado.
- Confirmar operario, supervisor, QA, Seguridad y responsable de evidencia; establecer canal de detención segura.

## Matriz de ejecución

| ID | Escenario | Método | Criterio de aceptación | Evidencia mínima |
|---|---|---|---|---|
| ZEB-01 | DataWedge por gatillo físico | 30 lecturas alternando SKU/ubicación | 30/30 valores correctos, una acción por lectura y fuente DataWedge visible | video corto, log redactado y conteo |
| ZEB-02 | Cámara fallback | deshabilitar DataWedge y leer 10 códigos | 10/10 correctos; cancelación conserva entrada y permite reintento | capturas y conteo |
| ZEB-03 | Ingreso manual | código inválido y luego válido | error comprensible, foco recuperable y valor preservado | captura antes/después |
| ZEB-04 | Offline y reconexión | ejecutar Receive, Putaway y Pick asignados sin red; reiniciar; reconectar | cola durable, orden por tarea, resultado único y cero ajuste automático ante conflicto | CommandId, LocalSequence, timestamps y resultado |
| ZEB-05 | Sonido y vibración | éxito, rechazo, conflicto y offline en ambiente ruidoso | cada señal se percibe y el mismo estado también es textual/visual | acta del operario y video |
| ZEB-06 | Contraste/sol | interior, muelle y luz directa | texto, foco, estado y barcode legibles sin depender sólo de color | fotos sin datos sensibles |
| ZEB-07 | Guantes | sesión Receive/Putaway/Pick de 30 minutos | targets accionables, sin toques accidentales repetidos y sin retirar guantes | tiempos, errores y feedback |
| ZEB-08 | Jornada sostenida | 2 horas representativas con batería inicial/final | sin bloqueo, degradación perceptible ni calentamiento que impida operar | batería, ANR/crash y encuesta |
| ZEB-09 | Permisos y sesión | revocar token y reautenticar con cola pendiente | no se filtran datos; la cola permanece y sólo sincroniza con sesión válida | respuestas y estado local redactados |
| ZEB-10 | Packing/despacho offline | cortar red antes de esas operaciones | acción bloqueada con explicación; ningún hecho físico se confirma | captura y auditoría |

## Ergonomía y seguridad

Se prueba con mano dominante/no dominante, gatillo lateral/frontal disponible, guantes reales, postura de pie y movimiento entre ubicaciones. Se registran tiempo por tarea, reescaneos, errores, alcance del pulgar, fatiga percibida de 1 a 5 y comentarios textuales. Un promedio aceptable no oculta un fallo de seguridad: lectura ambigua, doble confirmación, stock alterado por conflicto o acción irreversible sin feedback son defectos bloqueantes.

## Evidencia y decisión

La carpeta de evidencia externa debe contener manifiesto JSON con `TestId`, commit, versión APK, dispositivo, DataWedge, red, actor, UTC, escenario, resultado y referencias a archivos; tokens, barcodes reales, nombres de clientes y payloads se redactan. QA firma cada escenario; Operaciones y Producto firman el go/no-go. `TEST-MOB-0003` sólo pasa con ZEB-01..10 aprobados o con excepción no crítica explícita, owner y fecha.

## Salida

- **PASS:** hardware representativo, red y flujo cumplen; evidencia enlazada a release.
- **FAIL:** cualquier corrupción/duplicación, lectura incorrecta silenciosa, pérdida de cola, bloqueo físico o defecto crítico/alto no aceptado.
- **BLOCKED:** falta dispositivo, profile, red, actores o datos; se mantiene `BLK-UAT-0001` con causa concreta.
