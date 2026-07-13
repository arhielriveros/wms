# WMS Mobile

Cliente Android industrial offline-first para recepción, putaway y picking. Package: `com.wms.mobile`.

## Stack

- Kotlin 2.0.21, Android Gradle Plugin 8.7.3 y Gradle 8.10.2.
- Jetpack Compose/Material 3, Room, WorkManager, Coroutines/Flow.
- OkHttp + kotlinx.serialization detrás de `MobileApi`.
- Zebra DataWedge y Google Code Scanner detrás de interfaces de escaneo.

## Seguridad y sesión

La pantalla inicial solicita endpoint HTTPS y token de acceso obtenido del flujo OIDC autorizado. El repositorio no contiene endpoints, usuarios, contraseñas, client secrets ni tokens de ejemplo. Sólo el endpoint público se persiste; el token vive en memoria y se pierde al cerrar sesión o terminar el proceso. Tras reinicio, los comandos siguen en Room pero requieren nueva autenticación para sincronizar.

HTTP se admite exclusivamente para `localhost` y `10.0.2.2` mediante una configuración de seguridad de red acotada. Todo otro destino requiere HTTPS y `usesCleartextTraffic=false` mantiene el bloqueo base.

## Flujo

1. Configurar endpoint e ingresar token de sesión.
2. `GET /api/v1/mobile/bootstrap` resuelve tenant, almacén y usuario.
3. `GET /api/v1/mobile/tasks` descarga tareas asignadas.
4. Receive, Putaway y Pick crean un `OfflineCommand` durable con UUID, `localSequence`, `entityVersion` y payload.
5. WorkManager envía batches de hasta 100 a `POST /api/v1/mobile/commands:batch` cuando hay red.
6. `Accepted`/`AlreadyProcessed` confirman; `Conflict`/`RequiresReview` pausan; `Rejected`, `Expired` y `Unauthorized` se muestran explícitamente. No existe last-write-wins.

Packing y despacho no están implementados offline por decisión arquitectónica.

## Zebra DataWedge

Crear un profile asociado a `com.wms.mobile`, habilitar Barcode input e Intent output con:

- Intent action: `com.wms.mobile.SCAN`
- Intent category: vacía o default según política del dispositivo
- Delivery: Broadcast intent
- Extra de datos esperado: `com.symbol.datawedge.data_string`

El receiver se registra sólo mientras la Activity está visible. El fallback manual siempre está disponible; el botón de cámara usa Google Code Scanner mediante el contrato `CameraScanner`.

## Build

Requisitos: JDK 17 o 21, Android SDK Platform 35 y Build Tools compatibles. El proyecto fija Gradle en `gradle/wrapper/gradle-wrapper.properties`; por la restricción de edición textual de esta implementación no se versionó el binario `gradle-wrapper.jar`.

En una estación con Gradle 8.10.2:

```text
gradle wrapper --gradle-version 8.10.2
./gradlew test
./gradlew assembleDebug
```

En Windows usar `gradlew.bat` después de generar el wrapper. No versionar `local.properties`.

## Pruebas

Los tests JVM verifican orden por tarea/secuencia, límite de batch, idempotencia por `CommandId`, mapeo de los siete resultados de sync y política HTTPS. Las pruebas instrumentadas pendientes deben cubrir Room real, reinicio del proceso, WorkManager, DataWedge físico, cámara y pérdida/restablecimiento de red.

## Estructura

- `data/local`: entidades y DAO Room.
- `data/network`: contratos DTO y adaptador HTTP.
- `data/repository`: frontera de tareas/cola/sync.
- `domain`: orden e interpretación de resultados, sin Android.
- `scanner`: adaptadores DataWedge/cámara.
- `sync`: Worker y scheduler.
- `ui`: ViewModel, pantallas y tokens industriales.
