# ADR-0005 — Operación móvil offline-first

- **Fecha:** 2026-07-13
- **Estado:** Aceptado
- **Responsable:** Mobile y Operaciones

## Contexto

La conectividad del almacén es intermitente, pero los operarios deben continuar tareas previamente autorizadas sin comprometer inventario.

## Decisión

Aplicación Android nativa con Kotlin, Compose, Room, WorkManager, Coroutines y Flow. Descarga tareas asignadas y maestros mínimos; persiste comandos con `CommandId`, secuencia y versión; sincroniza en orden por tarea. Picking offline se limita a tareas asignadas; packing y despacho requieren conexión.

## Alternativas

Web/PWA reduce integración industrial; online-only detiene la operación; last-write-wins corrompe stock.

## Consecuencias y riesgos

Mayor complejidad local y necesidad de políticas de expiración/conflicto. El servidor es autoridad: conflictos se marcan para revisión, nunca se resuelven mutando stock automáticamente. DataWedge es principal con fallback teclado/cámara.
