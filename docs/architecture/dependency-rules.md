# Dependencias permitidas

## Capas por módulo

`Domain ← Application ← Infrastructure` y `Application ← Api/Worker`. Domain sólo usa biblioteca estándar y building blocks estables. Los hosts componen módulos; no contienen reglas de negocio.

## Matriz

| Consumidor | Puede depender de | No puede depender de |
|---|---|---|
| Domain | Tipos propios, contratos de dominio | EF, Dapper, MassTransit, ASP.NET, otro módulo |
| Application | Domain, puertos públicos | Tablas/contextos internos ajenos |
| Infrastructure | Application, librerías técnicas | UI, dominio interno ajeno |
| API/Worker | Contratos de Application | Reglas duplicadas, SQL de negocio |
| Web/Mobile | OpenAPI, schemas y tokens | PostgreSQL, broker, tipos ORM |

## Enforcement

Pruebas de arquitectura inspeccionarán referencias y namespaces. Roles PostgreSQL sólo tendrán `USAGE`/DML sobre su schema y lectura explícita sobre proyecciones autorizadas. Toda excepción requiere ADR nuevo, owner, caducidad y test que acote el acceso.
