# Plan de pruebas — Layout

| ID | Nivel | Escenario y aceptación |
|---|---|---|
| TEST-LAY-0001 | Unitario | UC-LAY-0001 cumple RULE-LAY-0001 y transición nominal. |
| TEST-LAY-0002 | Integración | Repetir idempotency key produce un efecto y un evento. |
| TEST-LAY-0003 | Concurrencia | Versiones obsoletas/transición inválida devuelven 409 sin efecto parcial. |
| TEST-LAY-0004 | Seguridad | RLS, IDOR y rol de schema bloquean tenant o módulo ajeno. |
| TEST-LAY-0005 | Contrato/E2E | API y EVENT-LAY-0001 validan schema v1, telemetría y recuperación de dependencia. |

Gate: todos los tests, análisis estático, contratos y trazabilidad verdes; evidencia adjunta al release. Rendimiento objetivo interactivo p95 < 500 ms bajo carga piloto.
