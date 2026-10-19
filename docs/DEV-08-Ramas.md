# DEV-08 · Organización de ramas

Decisión del 18 de octubre de 2026, solicitada por el usuario antes de continuar con la lógica.

## Ramas activas

- `main`: versión comprobada e integrable, incluida la última interfaz de `feature/ui-dialogos-productos` (`dc35083`).
- `feature/desarrollo`: única rama de trabajo; parte de la versión integrada en `main` y sirve para continuar la lógica y los ajustes de interfaz necesarios.

No se mantienen ramas permanentes de UI y lógica: los casos de uso y sus pantallas evolucionan juntos. ARQ-01 ya separa responsabilidades entre Domain, Application, Infrastructure y WPF. Los commits deben seguir siendo pequeños y enfocados.

## Integración y comprobaciones

1. Trabajar en `feature/desarrollo`.
2. Ejecutar `scripts/verify.ps1`; añadir `-SqlIntegration` cuando cambien persistencia, permisos o inventario.
3. Abrir PR hacia `main`, revisar el diff y esperar CI correcto.
4. Integrar con merge para conservar los commits. No eliminar `feature/desarrollo`.
5. Actualizar referencias y avanzar `feature/desarrollo` a `origin/main` mediante fast-forward antes del siguiente incremento.

La consolidación de la interfaz se comprobó en Windows con `scripts/verify.ps1 -SqlIntegration`: compilación Release sin errores ni advertencias, 20 casos xUnit, 57 comprobaciones WPF sin errores de enlaces, 49 comprobaciones SQL de seguridad/catálogo y 40 de inventario. Las suites SQL usan bases aisladas `GoDental_Tests_<GUID>`; no modifican la base de desarrollo del usuario. Las capturas y resultados locales están en `TestResults/` (ignorados por Git). Esto no certifica producción ni hardware/DPI real en todos los equipos.

## Archivo y recuperación

Antes de borrar las ramas anteriores se conservan sus puntas como etiquetas `archive/2026-10-18/<nombre-original>`, publicadas en origin. Se guarda además un bundle Git local y un inventario de referencias en `work/branch-cleanup-2026-10-18/`, fuera del repositorio. El stash local preexistente se conserva.

Se archivan las ramas `chore/*`, `feature/arranque-escritorio`, `feature/inventario-operativo`, `feature/ui-dialogos-productos` y las ocho propuestas `dependabot/*`. Las propuestas de dependencias no se integran automáticamente. Los PR anteriores de arranque e inventario quedan sustituidos por la consolidación, cuyo historial incluye sus commits; las propuestas de Dependabot se cierran conservando las etiquetas.

Para consultar el trabajo anterior sin crear otra rama:

```powershell
git fetch origin --tags
git show archive/2026-10-18/feature/ui-dialogos-productos
git log archive/2026-10-18/chore/sql-v0.1 --oneline
```

Si alguna propuesta archivada vuelve a necesitarse, revisar su diff y aplicar los commits adecuados en `feature/desarrollo`; no reabrir ramas permanentes de UI o lógica.

Los PR automáticos de versiones de Dependabot se pausan en ambos ecosistemas para evitar recrear ramas de mantenimiento. Se conservan las alertas de seguridad; las actualizaciones de seguridad habilitadas en GitHub pueden crear ramas excepcionales y deberán revisarse.
