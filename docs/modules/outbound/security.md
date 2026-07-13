# Seguridad — Outbound

Amenazas: tenant spoofing, IDOR, replay, escalada de scopes, inyección, carrera y fuga en logs/eventos. Controles: OIDC/PKCE según cliente, MFA para roles privilegiados, tokens cortos, validación issuer/audience, RLS deny-by-default, autorización por operación y versionado optimista.

Entradas se validan por allowlist y tamaño; idempotency keys quedan ligadas a tenant/operación. Secretos provienen del gestor de secretos y rotan. Auditoría es append-only y registra actor, acción, objeto, resultado y correlación.

TEST-OUT-0004 es gate obligatorio; hallazgos críticos o altos bloquean despliegue.
