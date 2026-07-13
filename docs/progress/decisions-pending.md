# Decisiones pendientes

| ID | Decisión | Momento límite | Owner | Default seguro |
|---|---|---|---|---|
| DEC-0001 | Proveedor/stack concreto de observabilidad | antes de H1 deploy | Operaciones | OTLP estándar sin vendor lock-in |
| DEC-0002 | MinIO u otro S3 compatible para piloto | antes de evidencia H2 | Plataforma | MinIO local, interfaz S3 |
| DEC-0003 | Modelo Zebra y profile DataWedge | antes de UAT H2 | Operaciones/Mobile | intents genéricos + teclado/cámara |
| DEC-0004 | Retención de auditoría/evidencias según jurisdicción | antes de datos reales | Legal/Seguridad | no purgar automáticamente |
| DEC-0005 | Política exacta de asignación parcial | diseño Outbound H3 | Producto/Operaciones | todo-o-nada por línea |
| DEC-0006 | Umbral/aprobador de short pick | diseño Outbound H3 | Producto/Seguridad | revisión supervisor siempre |
| DEC-0007 | Topología Linux del piloto y DNS/TLS | antes de H1 deploy | DevOps | red privada + TLS interno/externo |

Estas decisiones no alteran ADR aceptados; si cambian una decisión rectora requieren ADR de reemplazo.
