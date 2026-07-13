# Plan de pruebas — Inventario

| ID | Nivel | Escenario y aceptación |
|---|---|---|
| TEST-INV-0001 | Unitario | UC-INV-0001 cumple RULE-INV-0001 y transición nominal. |
| TEST-INV-0002 | Integración | Repetir idempotency key produce un efecto y un evento. |
| TEST-INV-0003 | Concurrencia | Versiones obsoletas/transición inválida devuelven 409 sin efecto parcial. |
| TEST-INV-0004 | Seguridad | RLS, IDOR y rol de schema bloquean tenant o módulo ajeno. |
| TEST-INV-0005 | Contrato/E2E | API y EVENT-INV-0001 validan schema v1, telemetría y recuperación de dependencia. |

Gate: todos los tests, análisis estático, contratos y trazabilidad verdes; evidencia adjunta al release. Rendimiento objetivo interactivo p95 < 500 ms bajo carga piloto.
