# Plan de pruebas — Outbound

| ID | Nivel | Escenario y aceptación |
|---|---|---|
| TEST-OUT-0001 | Unitario | UC-OUT-0001 cumple RULE-OUT-0001 y transición nominal. |
| TEST-OUT-0002 | Integración | Repetir idempotency key produce un efecto y un evento. |
| TEST-OUT-0003 | Concurrencia | Versiones obsoletas/transición inválida devuelven 409 sin efecto parcial. |
| TEST-OUT-0004 | Seguridad | RLS, IDOR y rol de schema bloquean tenant o módulo ajeno. |
| TEST-OUT-0005 | Contrato/E2E | API y EVENT-OUT-0001 validan schema v1, telemetría y recuperación de dependencia. |

Gate: todos los tests, análisis estático, contratos y trazabilidad verdes; evidencia adjunta al release. Rendimiento objetivo interactivo p95 < 500 ms bajo carga piloto.
