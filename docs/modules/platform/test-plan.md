# Plan de pruebas — Plataforma

| ID | Nivel | Escenario y aceptación |
|---|---|---|
| TEST-PLT-0001 | Unitario | UC-PLT-0001 cumple RULE-PLT-0001 y transición nominal. |
| TEST-PLT-0002 | Integración | Repetir idempotency key produce un efecto y un evento. |
| TEST-PLT-0003 | Concurrencia | Versiones obsoletas/transición inválida devuelven 409 sin efecto parcial. |
| TEST-PLT-0004 | Seguridad | RLS, IDOR y rol de schema bloquean tenant o módulo ajeno. |
| TEST-PLT-0005 | Contrato/E2E | API y EVENT-PLT-0001 validan schema v1, telemetría y recuperación de dependencia. |

Gate: todos los tests, análisis estático, contratos y trazabilidad verdes; evidencia adjunta al release. Rendimiento objetivo interactivo p95 < 500 ms bajo carga piloto.
