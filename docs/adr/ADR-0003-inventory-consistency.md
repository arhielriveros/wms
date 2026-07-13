# ADR-0003 — Consistencia de inventario

- **Fecha:** 2026-07-13
- **Estado:** Aceptado
- **Responsable:** Dominio de Inventario

## Contexto

Recepción, reserva y picking concurrentes no pueden producir stock negativo, sobreasignación ni historia irrecuperable.

## Decisión

Registrar movimientos append-only y mantener saldos/reservas transaccionales. Bloquear la dimensión afectada, comprobar versión y constraints, escribir movimiento y actualizar proyección en una única transacción EF Core. Correcciones se expresan como movimientos compensatorios.

## Alternativas

Event sourcing completo añade complejidad no justificada; un campo mutable pierde historia; optimistic-only amplifica conflictos bajo alta contención.

## Consecuencias y riesgos

Auditoría y disponibilidad rápidas con integridad fuerte. Riesgo de hot rows y deadlocks; se mitiga con orden global de bloqueo, transacciones cortas, partición/índices y retry sólo de errores transitorios. Redis no participa en la autoridad.
