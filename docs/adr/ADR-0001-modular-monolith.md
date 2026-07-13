# ADR-0001 — Monolito modular distribuible

- **Fecha:** 2026-07-13
- **Estado:** Aceptado
- **Responsable:** Arquitectura WMS

## Contexto

El producto necesita fronteras extraíbles sin asumir desde el piloto el coste operativo y transaccional de microservicios.

## Decisión

Construir un monolito modular .NET 10 desplegado como API y Worker coordinados. Cada bounded context tendrá dominio, aplicación, infraestructura, schema y contrato público propios. No habrá acceso a tablas internas entre módulos.

## Alternativas

Microservicios desde el inicio; monolito por capas sin módulos; funciones serverless. Se descartan por complejidad distribuida prematura o fronteras insuficientes.

## Consecuencias y riesgos

Despliegue y depuración simples, transacciones locales claras y evolución controlada. Riesgo: acoplamiento accidental; se mitiga con pruebas arquitectónicas, permisos de base y contratos. Una extracción futura exige evidencia medible y ADR específico.
