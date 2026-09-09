# ARQ-01 — Arquitectura C#

**Versión:** v0.1 · **Stack:** .NET 8 · WPF · C# · SQL Server · Dapper · QuestPDF

## Decisiones vigentes

- Solución dividida en `GoDental.Domain`, `GoDental.Application`, `GoDental.Infrastructure` y `GoDental.Wpf`; `GoDental.Worker` sólo se crea cuando una tarea deba ejecutarse con WPF cerrado.
- Dependencias hacia adentro: Domain sólo BCL; Application depende de Domain; Infrastructure implementa contratos de Application y conoce Domain; WPF compone Application + Infrastructure.
- La UI no ejecuta SQL ni contiene reglas transaccionales. No Service Locator, no `GenericRepository<T>`, no CQRS/event sourcing/microservicios en v0.1.
- Generic Host + `Microsoft.Extensions.DependencyInjection` como composition root.
- Dapper + `Microsoft.Data.SqlClient`; conexiones abiertas tarde/cerradas pronto. Operaciones críticas usan procedimientos/transacciones aprobados.
- Concurrencia multi-PC se resuelve en SQL Server/rowversion/transacciones, nunca con locks locales como mecanismo de integridad compartida.
- IO asíncrona de extremo a extremo; no `.Result`, `.Wait()` ni `Thread.Sleep` en UI.
- Contratos para PDF, archivos, seguridad, fiscal, pagos y Online permiten crecer sin romper el núcleo.
- `tests/GoDental.Tests` cubre reglas puras, validación, integración SQL y escenarios de concurrencia conforme avance el producto.

## Organización esperada

`Domain`: Entities, Enums, ValueObjects, Rules.  
`Application`: Abstractions, DTOs, Services, UseCases, Validators, Common.  
`Infrastructure`: Data, Repositories, Security, Pdf, Files, Backup, Fiscal, Payments, Online.  
`Wpf`: Bootstrap, Navigation, Views, ViewModels, Controls, Behaviors, Converters, Themes, Resources, Common.

Si una necesidad real exige romper estas fronteras, se documenta y versiona ARQ-01 antes de introducir la excepción.
