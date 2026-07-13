# Convención de identificadores

Formato: `<TIPO>-<DOMINIO>-NNNN`, cuatro dígitos, estable e inmutable. Dominios MVP: `WMS`, `ARC`, `PLT`, `SEC`, `LAY`, `MST`, `INV`, `INB`, `OUT`, `TSK`, `INT`, `MOB`, `UX`, `OPS`, `QA`.

| Tipo | Ejemplo | Uso |
|---|---|---|
| EPIC | EPIC-WMS-0001 | objetivo de negocio amplio |
| FEATURE | FEATURE-INB-0001 | capacidad entregable |
| STORY | STORY-INB-0001 | incremento verificable |
| UC | UC-INB-0001 | caso de uso |
| RULE | RULE-INV-0001 | regla de negocio |
| API | API-INT-0001 | operación pública |
| EVENT | EVENT-INB-0001 | hecho versionado |
| TEST | TEST-INB-0001 | prueba/evidencia |
| SEC | SEC-WMS-0001 | control/amenaza |
| ADR | ADR-0001 | decisión arquitectónica global |

No se renumeran IDs borrados. Código, mensajes de error, tests y documentación referencian el ID normativo. Un commit usa `type(scope): resumen [ID]`; una release enlaza commit y evidencia CI.

## Estados

Pendiente, En análisis, Documentado, En desarrollo, En pruebas, En revisión, Terminado, Desplegado o Deprecado. “Documentado” no equivale a aprobado/terminado.
