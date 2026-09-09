# GitHub en GoDental

Este documento explica cómo usar el repositorio como parte del flujo de desarrollo, no sólo como almacenamiento de código.

## Qué valida GitHub automáticamente

El workflow `.github/workflows/ci.yml` se ejecuta en cada Pull Request hacia `main`, en cada push a `main` y también puede ejecutarse manualmente desde **Actions → CI → Run workflow**.

Valida:

1. Restauración de paquetes NuGet.
2. Compilación Release de `GoDental.sln`.
3. Ejecución de tests xUnit.
4. Recolección de cobertura de código.
5. Publicación de `TestResults` como artifact descargable durante 14 días.

## Ejecutar lo mismo localmente

Desde PowerShell en la raíz del repositorio:

```powershell
.\scripts\verify.ps1
```

O manualmente:

```powershell
dotnet restore GoDental.sln
dotnet build GoDental.sln -c Release --no-restore
dotnet test GoDental.sln -c Release --no-build --no-restore --collect:"XPlat Code Coverage"
```

Los tests están en `tests/GoDental.Tests/`.

## AGENTS.md

`AGENTS.md` no es un GitHub Action. Es un contrato de trabajo para asistentes/agentes de programación que operen sobre el repositorio. Resume límites arquitectónicos y documentales de GoDental para evitar que un agente genere código que contradiga ARQ-01, DB-01, UX-01 u otras decisiones vigentes.

## Pull Requests

El flujo recomendado es:

1. Crear rama `feature/...`, `fix/...`, `docs/...` o `chore/...`.
2. Hacer cambios y pruebas locales.
3. Abrir Pull Request hacia `main`.
4. Esperar CI verde.
5. Revisar el diff.
6. Integrar mediante squash merge cuando corresponda.

`.github/pull_request_template.md` agrega el checklist automáticamente al crear un PR.

## CODEOWNERS

`.github/CODEOWNERS` marca al propietario del repositorio como responsable de revisión. Esto cobra más valor al proteger `main` y exigir revisión de cambios antes de integrar.

## Issues

Las plantillas de `.github/ISSUE_TEMPLATE/` separan errores de nuevas funcionalidades. Sirven para que bugs, decisiones y trabajo pendiente queden trazables y no se pierdan en chats o notas aisladas.

## Dependabot

`.github/dependabot.yml` revisa semanalmente paquetes NuGet y GitHub Actions. Cuando encuentre actualizaciones, propondrá Pull Requests en vez de modificar dependencias silenciosamente.

## Configuración administrativa recomendada

`main` debe protegerse desde GitHub Settings/Rules para requerir Pull Request y CI exitoso antes del merge. Esta configuración vive en GitHub, no dentro del repositorio.

También es recomendable mantener activadas las funciones de seguridad que el plan del repositorio permita, especialmente alertas de dependencias y detección de secretos.
