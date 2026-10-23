# ARQ-01 — Arquitectura C#

**Versión:** v0.2 · **Stack:** .NET 8 · WPF · C# · SQL Server · Dapper · QuestPDF · ASP.NET Core (objetivo)

## Decisión v0.2 · frontera central DTE

[FIS-01 v0.2](../FIS-01/README.md) y [diseño de GoDental.Server](../FIS-01/Arquitectura-y-Servidor.md)
definen un host ASP.NET Core .NET 8 como frontera confiable para operaciones externas.
WPF y Android futuro usan HTTPS; sólo el servidor custodia secretos, firma, autentica/transmite
ante Hacienda y reconcilia. Dependencias internas conservadas; composición por host y contexto
de operador por petición, sin singleton de sesión WPF. No se migra toda la aplicación a API
en un bloque. El acceso SQL comercial actual sigue siendo deuda S1, no identidad fiscal confiable.

Estado: **PREPARADO**, no existe todavía proyecto GoDental.Server. Implementación por bloques
B de FIS-01; contratos externos bloqueados hasta verificar fuentes oficiales. Worker durable
puede vivir en el host central, independiente de que WPF esté abierto. No crear otro proceso
sólo para satisfacer el nombre de una arquitectura.

## Decisiones vigentes

- Solución actual dividida en `GoDental.Domain`, `GoDental.Application`, `GoDental.Infrastructure` y `GoDental.Wpf`; `GoDental.Server` se incorporará según la decisión v0.2. Un proyecto `GoDental.Worker` separado sólo se crea si el host central no cubre una necesidad real de ejecución independiente.
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
