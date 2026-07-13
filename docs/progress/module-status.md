# Estado de módulos

| Módulo | Fase MVP | Documentación | Baseline ejecutable | Validación local | Gate piloto |
|---|---:|---|---|---|---|
| Plataforma/Tenancy | H1 | Aprobada | Implementada | Build | Pendiente E2E/RLS |
| Seguridad/Auditoría | H1 | Aprobada | Implementada | Build | Pendiente Keycloak/penetración |
| Layout | H1/H2 | Aprobada | Implementada | Build | Pendiente datos piloto |
| Maestros | H1/H2 | Aprobada | Implementada | Build | Pendiente datos piloto |
| Inventario | H1/H2/H3 | Aprobada | Implementada | Tests de invariantes | Pendiente concurrencia/carga |
| Inbound | H2 | Aprobada | Implementada | Contratos + build | Pendiente E2E ERP/Zebra |
| Outbound | H3 | Aprobada | Implementada | Contratos + build | Pendiente E2E ERP/Zebra |
| Task Execution básico | H2/H3 | Aprobada | Implementada | Tests de estados | Pendiente operación física |
| Integración | H1/H2/H3 | Aprobada | Implementada | Tests de retry/checksum | Pendiente broker/ERP real |
| Mobile Sync | H1/H2/H3 | Aprobada | Implementada | Revisión estática | Pendiente build SDK/UAT Zebra |
| Contextos futuros | F2+ | Overview | Diferida | No aplica | Fuera del MVP |

El gate F0 autoriza exclusivamente el alcance MVP documentado. “Implementada” identifica la baseline del repositorio, no el cierre del piloto: Hito 4 requiere las evidencias enumeradas en el reporte de implementación.
