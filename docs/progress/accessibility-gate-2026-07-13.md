# Gate de accesibilidad web — 2026-07-13

## Estado

`TEST-UX-0001` queda implementado y aprobado localmente para el alcance automatizable. Esto no reemplaza lector de pantalla, zoom 200/400 % ni la UAT física Zebra.

## Trabajo realizado

- Playwright 1.61.1 y axe-core 4.12.1 ejecutan reglas WCAG 2 A/AA, 2.1 A/AA y 2.2 AA sobre Chromium.
- Fixtures interceptan la API y prueban consola lista, vacía, degradada y responsive a 360 px sin depender del backend.
- El gate valida skip link, foco en contenido principal, indicador de foco y nombre programático de todos los botones.
- La UI incorporó contenido saltable, región de tabla desplazable alcanzable por teclado, caption, estados de integración textuales y decoraciones ocultas al árbol accesible.
- GitHub Actions conserva JSON y cuatro screenshots en `accessibility-gate-evidence` durante 14 días.
- `scripts/accessibility-gate.ps1` construye la web, espera readiness, ejecuta Playwright contra un servidor externo y lo detiene siempre; evita procesos huérfanos en Windows y usa la misma secuencia en CI.

## Evidencia local

| Escenario | Reglas axe aprobadas | Violaciones |
|---|---:|---:|
| desktop ready 1440×1000 | 25 | 0 |
| desktop empty 1440×1000 | 23 | 0 |
| desktop error 1440×1000 | 23 | 0 |
| mobile ready 360×800 | 26 | 0 |

El primer ciclo detectó `scrollable-region-focusable` en la tabla responsive y falló correctamente. Tras hacer la región nombrada y enfocable, el segundo ciclo produjo `PASS`, cuatro controles de teclado correctos y cero violaciones. Los resultados `incomplete` de axe permanecen sujetos a revisión humana y no se declaran aprobados automáticamente.

## Preparación móvil

`TEST-MOB-0003` dispone ahora de un protocolo reproducible para DataWedge, cámara, offline/reinicio, sonido, vibración, sol, guantes y jornada sostenida. Su estado sigue **Preparado, no ejecutado** por `BLK-UAT-0001`.

## Riesgos

- axe no demuestra conformidad WCAG completa; lector, zoom y juicio visual siguen manuales.
- hardware, accesorios, ruido, iluminación y Wi-Fi reales pueden cambiar el resultado móvil.
- las capturas locales son diagnósticas; la evidencia versionada oficial será el artifact asociado al commit en CI.
