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

## Abrir en Visual Studio

1. Clonar el repositorio.
2. Abrir `GoDental.sln` en Visual Studio 2022 con el workload **.NET desktop development** y SDK .NET 8 instalado.
3. Copiar `src/GoDental.Wpf/appsettings.local.example.json` a `appsettings.local.json`.
4. Configurar localmente la conexión SQL; `appsettings.local.json` está ignorado por Git.
5. Restaurar paquetes NuGet y compilar la solución.

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

## Base de datos

`database/` conserva la secuencia 001–008 definida por la arquitectura. Los archivos actuales son marcadores de estructura hasta incorporar los scripts SQL v0.1 aprobados; no contienen un esquema inventado.

## Documentación

La documentación fuente vive en `docs/` y es la referencia para decisiones funcionales/técnicas. Si el código necesita contradecir una regla documentada, primero se versiona la decisión; no se introduce una excepción silenciosa.

## Desarrollo

- Ramas: `feature/...`, `fix/...`, `chore/...`.
- `main` debe mantenerse integrable.
- Ejecutar `.\scripts\verify.ps1` antes de integrar cambios relevantes.
- Convenciones adicionales: `CONTRIBUTING.md`.
- Instrucciones para agentes/asistentes de programación: `AGENTS.md`.
