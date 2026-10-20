# DEV-08 · Organización de ramas

Decisión del 19 de octubre de 2026. Sustituye la política de una única rama `feature/desarrollo`.

## Ramas vigentes

- `feature/ui`: WPF, estilos, navegación, diálogos, ViewModels y pruebas de presentación.
- `feature/logica`: Domain, Application, Infrastructure, persistencia, SQL y sus pruebas.
- `main`: versión integrada y validada; no es una tercera rama de desarrollo.

Base común: `0fc3504`, que contiene todos los commits de `origin/main` (`ee3a504`) y los incrementos posteriores de conteos, proveedores y clientes. El árbol local estaba limpio al comenzar. No se descartaron cambios ni se usó force push.

## Sincronización y entrega

1. Actualizar referencias antes de cada bloque e incorporar mediante merge los cambios compatibles de la otra rama. Revisar ambos lados de cualquier conflicto.
2. Separar cambios y commits por responsabilidad; no llevar UI pendiente a lógica ni modificar contratos para hacer compilar una pantalla.
3. Registrar necesidades de contratos en `DEV-15-Coordinacion-UI.md`, con entradas, salida, permisos, errores y estado.
4. Compilar Release, ejecutar pruebas pertinentes y `scripts/verify.ps1` en Windows. Para SQL usar sólo bases aisladas de pruebas.
5. Publicar la rama correspondiente y abrir PR hacia `main`; integrar sólo tras completar las comprobaciones y CI.
6. Después de integrar un incremento validado, sincronizar ambas ramas con `main` mediante merge, conservando el historial.

## Archivo

La punta anterior de `feature/desarrollo` se conserva en la etiqueta publicada `archive/2026-10-19/feature/desarrollo` (`0fc3504`). Las etiquetas de archivo del 18 de octubre permanecen vigentes. No crear ramas permanentes adicionales ni borrar referencias sin conservar el trabajo.

La consulta de PR abiertos mediante `gh pr list` fue rechazada por la API GraphQL (Forbidden). Antes de retirar la rama remota anterior se debe comprobar en GitHub que no haya PR abiertos que dependan de ella; mientras tanto se conserva sólo como referencia heredada, sin desarrollo nuevo.
