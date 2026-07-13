# Plan de pruebas — Seguridad y Auditoría

| ID | Nivel | Escenario y aceptación |
|---|---|---|
| TEST-SEC-0001 | Unitario | UC-SEC-0001 cumple RULE-SEC-0001 y transición nominal. |
| TEST-SEC-0002 | Integración | Repetir idempotency key produce un efecto y un evento. |
| TEST-SEC-0003 | Concurrencia | Versiones obsoletas/transición inválida devuelven 409 sin efecto parcial. |
| TEST-SEC-0004 | Seguridad | RLS, IDOR y rol de schema bloquean tenant o módulo ajeno. |
| TEST-SEC-0005 | Contrato/E2E | API y EVENT-SEC-0001 validan schema v1, telemetría y recuperación de dependencia. |

Gate: todos los tests, análisis estático, contratos y trazabilidad verdes; evidencia adjunta al release. Rendimiento objetivo interactivo p95 < 500 ms bajo carga piloto.
