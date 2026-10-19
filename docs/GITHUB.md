# GitHub en GoDental

Este documento explica cómo usar el repositorio como parte del flujo de desarrollo, no sólo como almacenamiento de código.

## Qué valida GitHub automáticamente

El workflow `.github/workflows/ci.yml` se ejecuta en cada Pull Request hacia `main`, en cada push a `main` y también puede ejecutarse manualmente desde **Actions → CI → Run workflow**.

Valida:

1. Restauración de paquetes NuGet.
2. Compilación Release de `GoDental.sln`.
3. Ejecución de tests xUnit.
4. Recolección de cobertura de código.
5. Suites SQL aisladas de seguridad/catálogo, inventario, conteos y proveedores sobre LocalDB del runner (no la base central de la aplicación).
6. Smoke tests WPF con capturas y comprobaciones de enlaces.
7. Publicación de `TestResults` como artifact descargable durante 14 días.

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

1. Trabajar en `feature/desarrollo`, la única rama activa además de `main`.
2. Hacer cambios y pruebas locales.
3. Abrir Pull Request hacia `main`.
4. Esperar CI verde.
5. Revisar el diff.
6. Integrar conservando el historial mediante merge; mantener `feature/desarrollo` y sincronizarla con `main` mediante `git merge --ff-only origin/main` después de actualizar referencias.

UI y lógica se separan por capas y commits. Las ramas anteriores están archivadas como etiquetas, según [DEV-08](DEV-08-Ramas.md).

`.github/pull_request_template.md` agrega el checklist automáticamente al crear un PR.

## CODEOWNERS

`.github/CODEOWNERS` marca al propietario del repositorio como responsable de revisión. Esto cobra más valor al proteger `main` y exigir revisión de cambios antes de integrar.

## Issues

Las plantillas de `.github/ISSUE_TEMPLATE/` separan errores de nuevas funcionalidades. Sirven para que bugs, decisiones y trabajo pendiente queden trazables y no se pierdan en chats o notas aisladas.

## Dependabot

Los PR automáticos de actualización de versiones NuGet y GitHub Actions están pausados con `open-pull-requests-limit: 0` para mantener las dos ramas activas acordadas. Sus propuestas pendientes se archivan sin aplicar cambios de dependencias. Las actualizaciones se revisarán en `feature/desarrollo`. Esto no desactiva las alertas de seguridad ni la configuración de actualizaciones de seguridad de GitHub; estas últimas pueden crear ramas adicionales si están habilitadas.

## Protección recomendada para main

Actualmente esta política se configura en GitHub y no mediante un archivo del repositorio.

En **Settings → Rules → Rulesets** crea una regla dirigida a la rama `main` y activa, como mínimo:

- Require a pull request before merging.
- Require status checks to pass before merging.
- Seleccionar el check `Build & Test` cuando ya aparezca disponible.
- Require branches to be up to date before merging.
- Require conversation resolution before merging.
- Bloquear force pushes.
- No permitir deletions de `main`.

Si trabajas solo, no es necesario exigir una aprobación de otra persona todavía; el objetivo inicial es impedir que se integre código sin PR y sin CI verde.

## Seguridad del repositorio

Mantén activadas las funciones que permita el plan del repositorio, especialmente:

- Dependabot alerts.
- Dependency graph.
- Secret scanning / push protection cuando estén disponibles.

No guardes secrets de SQL Server, DTE, Meta/WhatsApp ni otros proveedores en el repositorio. Cuando una integración requiera secretos para CI o despliegue, deben ir en GitHub Secrets/Environments, no en `appsettings.json`.
