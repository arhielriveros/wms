# ADR-0007 — Evidencias y observabilidad

- **Fecha:** 2026-07-13
- **Estado:** Aceptado
- **Responsable:** Plataforma y Operaciones

## Contexto

Diagnóstico y auditoría requieren unir API, dominio, base, broker, ERP y dispositivo sin guardar binarios pesados en la base.

## Decisión

Usar almacenamiento S3 compatible para evidencias con checksum, tenant, retención y referencia inmutable en PostgreSQL. Instrumentar desde el primer endpoint con Serilog estructurado y OpenTelemetry para logs, métricas y trazas propagando correlación/causalidad.

## Alternativas

Binarios en PostgreSQL afectan operación; logs de texto sin contexto no permiten trazabilidad; instrumentación posterior deja puntos ciegos.

## Consecuencias y riesgos

Coste de almacenamiento y riesgo de PII/secreto. Se mitiga con mínimo dato, cifrado, URLs temporales, acceso auditado, redacción y políticas de retención. La evidencia no puede borrarse desde la aplicación operativa.
