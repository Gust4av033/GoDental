# GoDental

GoDental es una aplicación de escritorio para la operación comercial de un depósito dental: inventario, compras/importaciones, ventas/POS, caja, crédito, seguridad, fiscal y comercial/online.

## Stack

- .NET 8 + C# 12
- WPF / MVVM
- SQL Server central
- Dapper + Microsoft.Data.SqlClient
- Microsoft.Extensions.Hosting / Dependency Injection
- QuestPDF
- xUnit

## Arquitectura

```text
GoDental/
├── GoDental.sln
├── src/
│   ├── GoDental.Domain/
│   ├── GoDental.Application/
│   ├── GoDental.Infrastructure/
│   └── GoDental.Wpf/
├── tests/
│   └── GoDental.Tests/
├── database/
├── docs/
├── scripts/
└── .github/
```

Reglas principales:

1. `Domain` no conoce WPF, Dapper, SQL Server ni QuestPDF.
2. `Application` depende únicamente de `Domain` y define contratos/casos de uso.
3. `Infrastructure` implementa acceso a datos, archivos, PDF e integraciones.
4. `Wpf` es UI + composition root; no contiene SQL ni lógica transaccional.
5. Las operaciones críticas se validan en SQL Server y deben ser seguras para múltiples PCs.
6. No se crea `GoDental.Worker` hasta que exista una tarea que deba ejecutarse con WPF cerrado.

## Abrir en Visual Studio 2022

1. Clonar el repositorio.
2. Abrir `GoDental.sln` en **Visual Studio 2022** con el workload **.NET desktop development** y SDK .NET 8 instalado.
3. Establecer `GoDental.Wpf` como proyecto de inicio y ejecutar.
4. La aplicación abre la ventana de acceso. Desde allí, abrir **Ajustes de conexión** para guardar el servidor/base y comprobar el acceso con la cuenta de Windows. No hace falta SQL para abrir la interfaz.
5. Restaurar paquetes NuGet y compilar la solución.

## Base de datos

La versión 0.3 incluye usuarios y catálogo de la [v0.2](docs/DEV-02-Seguridad-Catalogo.md), más existencias, lotes/vencimientos, ajustes, bajas, transferencias y Kardex con auditoría. También incorpora filtros y mantenimiento de categorías. [Alcance y guía de inventario](docs/DEV-03-Inventario-operativo.md).

El perfil sin credenciales en `%LOCALAPPDATA%\GoDental\workstation.json` alimenta ahora tanto el diagnóstico como las operaciones. Para este avance se usa Windows integrada sobre SQL local; la cuenta SQL de producción con mínimo privilegio y su almacén protegido sigue pendiente. No se guarda una contraseña SQL en el perfil.

En este equipo se preparó `GoDental_Development`, vacía de usuarios y productos reales. En la ventana de acceso, elige tu primer administrador. Para otro equipo de desarrollo con SQL local puedes ejecutar:

```powershell
dotnet run --project tests/GoDental.DatabaseTests -- --install-development .
```

La herramienta limita el destino a una base local de desarrollo marcada. Nunca instala sobre `GoDental` ni sobre una base existente sin su marcador.

`database/` contiene la **baseline SQL v0.1** aprobada y validada estáticamente contra DB-01. Para una base nueva los scripts deben ejecutarse, obligatoriamente, en este orden:

```text
001_Core.sql
002_Inventario.sql
003_Compras.sql
004_ClientesVentas.sql
005_Fiscal_Base.sql
006_Comercial_Base.sql
007_Seed.sql
008_ViewsProcedures.sql
009_Seguridad_Parametros.sql
010_Inventario_Operativo.sql
```

La baseline define 72 tablas, datos iniciales controlados, 8 vistas y 12 procedimientos almacenados. La migración 010 añade dos procedimientos de inventario (14 en total). No crea un usuario Administrador con contraseña por defecto y no utiliza triggers.

Para una instalación local puede usarse `scripts/install-database.ps1` si `sqlcmd` está disponible. La primera ejecución debe hacerse contra una instancia de **desarrollo**, nunca directamente contra producción.

## Regla comercial de precios

`docs/VEN-01A.md` cierra la precedencia que VEN-01 había dejado pendiente:

**lista base → condición/tipo de cliente → comparación con promoción → mejor precio válido → descuento manual autorizado**.

No existe acumulación automática de beneficios; una promoción sólo puede acumularse cuando está configurada expresamente como acumulable.

## Probar antes de subir cambios

Desde PowerShell en la raíz del repositorio:

```powershell
.\scripts\verify.ps1
```

Ese script ejecuta Restore + Build Release + Tests + Coverage, equivalente al pipeline principal de GitHub Actions. Los resultados quedan en `TestResults/`.

También puedes ejecutar las pruebas desde **Visual Studio → Test → Test Explorer**.

## GitHub / CI

Cada Pull Request a `main` y cada push a `main` ejecuta CI automáticamente. También puede lanzarse manualmente desde **Actions → CI → Run workflow**. Los resultados y cobertura se publican como artifacts descargables.

Guía completa: [`docs/GITHUB.md`](docs/GITHUB.md).

## Configuración sensible

Nunca subir connection strings reales, contraseñas, tokens, certificados, `.bak`, logs o secretos fiscales/comerciales. Los valores locales viven fuera del control de versiones.

## Documentación

La documentación fuente vive en `docs/` y es la referencia para decisiones funcionales/técnicas. `VEN-01A` complementa `VEN-01` y prevalece sobre sus propuestas provisionales en materia de precedencia de precios. Si el código necesita contradecir una regla documentada, primero se versiona la decisión; no se introduce una excepción silenciosa.

## Desarrollo

- Ramas: `feature/...`, `fix/...`, `chore/...`.
- `main` debe mantenerse integrable.
- Ejecutar `.\scripts\verify.ps1` antes de integrar cambios relevantes.
- Convenciones adicionales: `CONTRIBUTING.md`.
- Instrucciones para agentes/asistentes de programación: `AGENTS.md`.
