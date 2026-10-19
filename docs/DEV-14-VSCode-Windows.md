# DEV-14 · Compilar desde VS Code en Windows

Abrir la carpeta del repositorio GoDental en VS Code para cargar `.vscode/tasks.json` y `launch.json`. Esta configuración queda en Git; no configura por sí sola una computadora local que no esté conectada a este chat.

## Preparación del equipo

En una terminal PowerShell local:

```powershell
winget install --id Microsoft.DotNet.SDK.8 --exact
code --install-extension ms-dotnettools.csharp
```

Cerrar y volver a abrir VS Code después de instalar el SDK; comprobar `dotnet --list-sdks` (debe aparecer 8.0.x). Si el SDK ya está instalado, no hace falta reinstalarlo. El SDK de Windows permite compilar y ejecutar WPF; no es obligatorio instalar Visual Studio 2022. La extensión aporta edición y depuración C#.

Si falta el repositorio, clonarlo con Git y abrir la carpeta:

```powershell
git clone https://github.com/Gust4av033/GoDental.git
cd GoDental
git switch feature/desarrollo
code .
```

Si ya existe la copia local, revisar `git status` y conservar sus cambios antes de actualizarla. Los commits de este chat deben estar publicados en `feature/desarrollo` para recuperarlos desde otro equipo.

## Uso

- **Ctrl+Shift+B**: compilar Debug.
- **F5**: compilar y depurar la aplicación WPF.
- **Terminal → Ejecutar tarea**: compilar Release, ejecutar pruebas, abrir GoDental o verificar WPF/SQL.
- Comandos equivalentes: `dotnet build GoDental.sln -c Release`, `dotnet test GoDental.sln -c Release`, `dotnet run --project src/GoDental.Wpf`.

La aplicación puede abrir la interfaz sin SQL; las operaciones necesitan una base configurada en Ajustes. `verify.ps1 -SqlIntegration` requiere SQL Server local con autenticación Windows para crear bases aisladas de pruebas. No apunta a la base del negocio ni configura una conexión con datos reales. LocalDB es una opción para las suites mediante `--localdb`, según CI; no es el servidor central del producto. No hace falta instalar SQL sólo para compilar.

La validación completa y capturas WPF siguen pendientes hasta ejecutarlas en Windows. WPF no se ejecuta en WSL/Linux: usar la terminal local de Windows para F5 y pruebas visuales.
