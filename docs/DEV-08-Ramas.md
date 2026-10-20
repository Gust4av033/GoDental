# DEV-08 · Organización de ramas

Decisión del 19 de octubre de 2026: lógica y UI avanzan juntas en una sola rama de trabajo. Sustituye la división temporal entre `feature/ui` y `feature/logica`.

## Ramas vigentes

- `feature/desarrollo`: única rama activa para Domain, Application, Infrastructure, SQL, WPF y pruebas. Separar cada cambio por responsabilidad en commits pequeños.
- `main`: versión integrada y validada mediante PR.
- `feature/ui` y `feature/logica`: referencias históricas. Sus commits quedaron incorporados a `feature/desarrollo`; no crear trabajo nuevo en ellas ni borrarlas antes de comprobar PR y dependencias remotas.

La sincronización local se hizo con un merge de `origin/feature/ui` hacia `feature/desarrollo`, conservando todo el historial. `feature/ui` ya contenía los commits de `feature/logica`. El ajuste de parámetros comerciales se guardó en un commit separado antes del merge. No hubo conflictos ni se usó force push.

## Flujo de trabajo

1. Empezar cada incremento con `git fetch origin`, revisar el estado local y actualizar `feature/desarrollo` desde su remoto. Si `main` recibió cambios, incorporarlos mediante merge, revisando conflictos.
2. Implementar el caso de uso completo en las capas correspondientes: reglas en Domain/Application, persistencia en Infrastructure/SQL y presentación en WPF. Mantener los contratos y pruebas en el mismo incremento.
3. Hacer commits pequeños y descriptivos. Evitar desarrollar por separado en `feature/ui` o `feature/logica`.
4. Ejecutar `dotnet build GoDental.sln -c Release`, `dotnet test GoDental.sln -c Release` y las pruebas SQL/WPF pertinentes. Las pruebas SQL usan bases aisladas; no ejecutar migraciones de prueba en la base del usuario.
5. Publicar `feature/desarrollo`, abrir PR hacia `main`, revisar el diff y esperar CI correcto antes de integrar. Después del merge, actualizar `feature/desarrollo` desde `main` sin reescribir historial publicado.

La unión local de ramas y sus pruebas se registran en este repositorio. La publicación remota y el PR se realizan por separado tras revisión.
