# Contribución a GoDental

## Flujo de trabajo

- `main`: versión validada e integrable.
- `feature/desarrollo`: única rama de trabajo para funcionalidades, UI, correcciones y mantenimiento.
- Separar UI y lógica en sus capas y en commits enfocados; no mantener ramas permanentes por capa.
- Abrir PR de `feature/desarrollo` hacia `main`, validar y revisar antes de integrar. Conservar la rama de trabajo y sincronizarla con `main` después del merge.
- Las ramas anteriores se conservan mediante etiquetas `archive/2026-10-18/<rama>`. Ver [decisión y recuperación](docs/DEV-08-Ramas.md).

## Antes de programar

Ubicar cada cambio en su capa correcta:

- `Domain`: entidades, enums y reglas puras.
- `Application`: DTOs, contratos, validadores, servicios/casos de uso.
- `Infrastructure`: SQL/Dapper, PDF, archivos, seguridad técnica e integraciones.
- `Wpf`: Views, ViewModels, navegación, diálogos, temas y composition root.

No crear carpetas genéricas como `Helpers` para esconder responsabilidades sin definir.

## Reglas de código

- PascalCase para tipos/métodos/propiedades; camelCase para variables/parámetros.
- Interfaces con prefijo `I`.
- Métodos async con sufijo `Async` y `CancellationToken` en IO cancelable.
- `decimal` para dinero.
- Nullable Reference Types habilitado.
- No usar `.Result`, `.Wait()` ni `Thread.Sleep` en UI.
- No SQL desde Views/ViewModels.
- No `GenericRepository<T>`.
- No `Service Locator`.
- No estado global mutable como mecanismo de concurrencia multi-PC.

## Pull Requests

Antes de abrir/integrar un PR:

- Compilar en Release.
- Ejecutar pruebas.
- Revisar que no existan secretos ni configuración local.
- Documentar decisiones que alteren arquitectura o reglas de negocio.
- Incluir pruebas cuando el cambio afecte reglas, concurrencia, seguridad o transacciones.
