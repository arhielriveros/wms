# Estrategia de pruebas

## Pirámide y ambientes

1. Unitarias rápidas para reglas, estados, cantidades, FIFO, permisos y transiciones.
2. Integración con PostgreSQL, RabbitMQ, Redis y Keycloak reales mediante contenedores.
3. Contrato para OpenAPI, envelopes, webhooks, eventos y compatibilidad de schemas.
4. E2E para slices y operación offline; rendimiento, seguridad y recuperación como suites dedicadas.

Datos de prueba son sintéticos y deterministas. Cada test porta su ID de trazabilidad. Flaky tests se aíslan con owner/plazo; no se reintentan silenciosamente para aprobar CI.

## Cobertura obligatoria

| ID | Escenario | Aserción crítica |
|---|---|---|
| TEST-SEC-0001 | Tenant A intenta leer/escribir B | 0 datos/efectos y auditoría de rechazo |
| TEST-SEC-0002 | Token Keycloak real: RBAC, IDOR, adulteración y logout | 401/403/404 correctos, acceso propio permitido y token revocado rechazado |
| TEST-INT-0001 | ASN repetido | un documento y un efecto |
| TEST-INT-0002 | mismo MessageId, payload distinto | conflicto, ningún efecto adicional |
| TEST-INV-0001 | dos reservas sobre el mismo saldo | sin stock negativo/sobreasignación |
| TEST-INV-0002 | movimiento/actualización falla | rollback de ambos |
| TEST-INB-0001 | ASN → recepción → putaway | stock disponible y confirmación única |
| TEST-INB-0002 | ERP caído tras recepción | hecho físico persiste, Outbox reintenta |
| TEST-MOB-0001 | offline → reinicio → reconexión | orden/idempotencia preservados |
| TEST-MOB-0002 | versión cambió offline | Conflict, cero ajuste automático |
| TEST-OUT-0001 | pedido → FIFO → pick → pack → ship | reserva consumida y confirmación única |
| TEST-OUT-0002 | short pick | motivo, aprobación y cantidad trazables |
| TEST-OPS-0001 | restore PITR | RPO/RTO y checks de integridad cumplidos |
| TEST-OPS-0002 | 100 dispositivos + 30 usuarios web sobre ≥5M movimientos | p95/batch/error dentro de gate y cero invariantes rotas |
| TEST-OPS-0003 | blue → green → rollback blue con tráfico continuo | API/worker healthy, slot previo preservado y cero respuestas fallidas |
| TEST-OPS-0004 | stack completo API/worker + dependencias + LGTM | checks operativos y métricas/trazas/logs consultables para ambos servicios, sin secretos persistidos |

## Rendimiento

Perfil con 100 dispositivos, 30 web, ≥5 millones de movimientos; ramp-up, carga sostenida y spike. Gates: API p95 <500 ms, batch 100 comandos <10 s, error <1 % excluyendo validaciones esperadas, cero invariantes rotas. Reportar p50/p95/p99, throughput, locks, pool, colas y recursos.

## Seguridad

RLS/IDOR, escalada, claims manipulados, replay, revocación, rate limit, inyección, XSS/CSRF/SSRF, fuga de secretos, firma webhook y dispositivo comprometido. Hallazgo crítico/alto sin mitigación bloquea release.

## Accesibilidad y hardware

WCAG 2.2 AA automatizada más revisión manual de teclado, lector, foco, contraste y zoom. En Zebra real: DataWedge, cámara fallback, pérdida de red, reinicio, sonido/vibración, sol, guantes y jornada sostenida.

## Evidencia

CI conserva resultados, cobertura, OpenAPI/schema diff, logs redactados, trazas, métricas de carga, SBOM, scan, screenshots y acta UAT con versión/commit/ambiente.
