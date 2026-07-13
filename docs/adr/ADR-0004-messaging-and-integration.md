# ADR-0004 — Mensajería e integración confiable

- **Fecha:** 2026-07-13
- **Estado:** Aceptado
- **Responsable:** Integración

## Contexto

PostgreSQL y el broker no comparten transacción, y la entrega externa puede duplicarse o fallar durante horas.

## Decisión

Usar RabbitMQ con entrega at-least-once, Outbox transaccional, Inbox idempotente, reintentos exponenciales con jitter, DLQ y reconciliación. Todos los mensajes usan envelope canónico, `MessageId`, correlación, causalidad y versión. Webhooks salientes son firmados.

## Alternativas

Publicación directa puede perder mensajes; exactamente-una-vez extremo a extremo no es realista; Kafka no se justifica para el volumen inicial.

## Consecuencias y riesgos

Los consumidores deben ser idempotentes y tolerar duplicados/orden parcial. El ERP caído no revierte recepción o despacho; la confirmación queda pendiente. Saga no es universal: sólo se adopta con compensación distribuida explícita.
