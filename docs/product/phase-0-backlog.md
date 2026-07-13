# Backlog de Fase 0

| ID | Entregable | Dependencia | DoD | Estado |
|---|---|---|---|---|
| STORY-WMS-0001 | Visión, alcance, actores, KPI y glosario | Prompt maestro | Revisión funcional | Documentado |
| STORY-ARC-0001 | C4, bounded contexts y dependencias | STORY-WMS-0001 | Revisión arquitectónica | Documentado |
| STORY-ARC-0002 | ADR-0001..0008 | STORY-ARC-0001 | Alternativas y consecuencias registradas | Documentado |
| STORY-INT-0001 | Autoridad ERP/WMS y contratos canónicos | STORY-ARC-0001 | Versionado e idempotencia definidos | Documentado |
| STORY-MOB-0001 | Estrategia offline y conflictos | STORY-INT-0001 | Sin last-write-wins | Documentado |
| STORY-SEC-0001 | Modelo multi-tenant, IAM y threat model | STORY-ARC-0001 | Controles y pruebas definidos | Documentado |
| STORY-UX-0001 | Design system y flujos industriales | STORY-WMS-0001 | Estados y WCAG definidos | Documentado |
| STORY-QA-0001 | Estrategia de pruebas y gates | Anteriores | Criterios medibles | Documentado |
| STORY-OPS-0001 | Observabilidad, SLO y recuperación | Arquitectura | Alertas y runbooks definidos | Documentado |
| STORY-TRC-0001 | Matriz de trazabilidad y progreso | Todos | Sin huérfanos del MVP | Documentado |
| STORY-GATE-0001 | Revisión y aprobación multidisciplinaria | Todos | Aprobadores registrados | Pendiente |

## Gate pendiente

La documentación está lista para revisión, pero `STORY-GATE-0001` requiere aprobación humana explícita de arquitectura, producto, seguridad, integración, UX, QA y operaciones. Hasta entonces el gate permanece **pendiente**.
