# Fuente de requisitos: prompt maestro

## Procedencia

- **Archivo de origen:** `C:/Users/Ariel Riveros/Downloads/Prompt_Maestro_WMS_Arquitectura_Modular_ERP_Agnostic.md`
- **Nombre:** Prompt Maestro — Plataforma WMS Composable, ERP-Agnostic y Offline-First
- **Incorporado:** 2026-07-13
- **Tratamiento:** síntesis trazable; el archivo externo permanece como fuente primaria y no se copia literalmente para evitar dos fuentes editables divergentes.

## Síntesis normativa

`SRC-PROMPT-001` exige una base operacional propia y contratos explícitos con ERP. `SRC-PROMPT-002` prohíbe modelos ERP dentro del núcleo. `SRC-PROMPT-003` prescribe monolito modular evolutivo. `SRC-PROMPT-004` exige documentación antes de código y trazabilidad completa. `SRC-PROMPT-005` establece .NET 10, PostgreSQL, Next.js y Android/Kotlin como stack. `SRC-PROMPT-006` exige seguridad multi-tenant, OAuth/OIDC, MFA, auditoría y pruebas anti-leak. `SRC-PROMPT-007` prescribe Outbox/Inbox, idempotencia, retry, DLQ, correlación y evolución de schemas. `SRC-PROMPT-008` exige operación móvil offline sin last-write-wins para stock. `SRC-PROMPT-009` exige logs, métricas y trazas con OpenTelemetry. `SRC-PROMPT-010` define una estrategia integral de pruebas. `SRC-PROMPT-011` exige UX industrial accesible. `SRC-PROMPT-012` define roadmap de fundación, core, ejecución avanzada, optimización, 3PL y automatización.

## Derivación

| Fuente | Decisión o entregable derivado |
|---|---|
| SRC-PROMPT-001..003 | ADR-0001, ADR-0002, arquitectura y matriz de autoridad |
| SRC-PROMPT-004 | gate documental, identificadores, matriz de trazabilidad y progreso |
| SRC-PROMPT-005 | ADR-0001 y arquitectura de contenedores |
| SRC-PROMPT-006 | ADR-0006 y modelo de seguridad |
| SRC-PROMPT-007 | ADR-0004, contratos canónicos y operación de integración |
| SRC-PROMPT-008 | ADR-0005 y estrategia offline |
| SRC-PROMPT-009 | observabilidad y SLO |
| SRC-PROMPT-010 | estrategia de pruebas y gates |
| SRC-PROMPT-011 | design system y flujos operativos |
| SRC-PROMPT-012 | roadmap y backlog |

Los cambios posteriores se registrarán como decisiones explícitas; nunca se alterará retrospectivamente esta síntesis para ocultar una desviación.
