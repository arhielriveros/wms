# Plan de pruebas — Mobile Sync

| ID | Nivel | Escenario y aceptación |
|---|---|---|
| TEST-SYN-0001 | Unitario | UC-SYN-0001 cumple RULE-SYN-0001 y transición nominal. |
| TEST-SYN-0002 | Integración | Repetir idempotency key produce un efecto y un evento. |
| TEST-SYN-0003 | Concurrencia | Versiones obsoletas/transición inválida devuelven 409 sin efecto parcial. |
| TEST-SYN-0004 | Seguridad | RLS, IDOR y rol de schema bloquean tenant o módulo ajeno. |
| TEST-SYN-0005 | Contrato/E2E | API y EVENT-SYN-0001 validan schema v1, telemetría y recuperación de dependencia. |

Gate: todos los tests, análisis estático, contratos y trazabilidad verdes; evidencia adjunta al release. Rendimiento objetivo interactivo p95 < 500 ms bajo carga piloto.
