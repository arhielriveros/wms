# ADR-0002 — Tenancy y aislamiento de base de datos

- **Fecha:** 2026-07-13
- **Estado:** Aceptado
- **Responsable:** Arquitectura y Seguridad

## Contexto

El piloto activa un tenant, pero el producto debe impedir fugas cuando se habiliten múltiples tenants y mantener autonomía modular.

## Decisión

Usar PostgreSQL compartido, schema y `DbContext` por módulo, `TenantId` obligatorio y RLS. La API establece el tenant validado en la sesión de base; no acepta que el cuerpo de una petición eleve alcance. Roles de base limitan DML al schema propietario.

## Alternativas

Base por tenant aporta aislamiento fuerte pero eleva operación temprana; schema por tenant complica migraciones; filtros ORM solos son insuficientes como defensa en profundidad.

## Consecuencias y riesgos

Menor coste y consultas operables, con aislamiento redundante. Riesgos: contexto ausente, pool contaminado o política RLS incompleta; se mitigan reseteando sesión, fail-closed, pruebas anti-leak e inspección de migraciones.
