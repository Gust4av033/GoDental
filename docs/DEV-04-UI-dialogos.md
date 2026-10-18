# DEV-04 · Diálogos de edición en WPF

## Decisión de UX

UX-01 v0.1 limitaba los diálogos a acciones breves o críticas. El formulario de producto requiere varios campos, pero abrirlo al final de la página obliga a desplazar el catálogo y pierde el contexto de la selección. Se amplía la regla: crear o editar un registro acotado puede usar un diálogo modal, con cuerpo desplazable y acciones siempre visibles. Las confirmaciones críticas también usan diálogo. Consulta, listados y detalles de solo lectura continúan en la página o en paneles.

## Alcance de esta fase

Se añade un servicio de diálogos inyectado y una ventana reutilizable con los recursos Light/Dark existentes. El editor de producto se mueve a un ViewModel nuevo por apertura; la página conserva catálogo, filtros, paginación, el estado de la operación y el detalle de existencias. Guardar correctamente cierra la ventana y refresca el catálogo; un error deja el formulario y su mensaje visibles. Salir con cambios solicita confirmación.

La administración de categorías, la edición de inventario y la gestión de cuentas conservan por ahora sus flujos actuales. Son fases posteriores y requieren una decisión de alcance propia.

## Verificación

En el entorno Linux se completó la compilación cruzada Release de la solución con .NET 8 y `EnableWindowsTargeting=true` (0 errores, 0 advertencias), y `dotnet test` aprobó los 20 casos ejecutables allí. Los smoke WPF incluyen render Light/Dark y comprobaciones de enlaces, filtros, guardado, rechazo, estado ocupado, cancelación y confirmación de cambios sin guardar. Al intentar ejecutarlos en Linux, el host informó que falta `Microsoft.WindowsDesktop.App` 8.0; requieren Windows. Su ejecución, así como `scripts/verify.ps1`, queda pendiente de Windows; la compilación cruzada no demuestra el comportamiento visual ni que `ValidateOnBuild` funcione al iniciar la aplicación.
