# DEV-04 · Diálogos de edición en WPF

## Decisión de UX

UX-01 v0.1 limitaba los diálogos a acciones breves o críticas. El formulario de producto requiere varios campos, pero abrirlo al final de la página obliga a desplazar el catálogo y pierde el contexto de la selección. Se amplía la regla: crear o editar un registro acotado puede usar un diálogo modal, con cuerpo desplazable y acciones siempre visibles. Las confirmaciones críticas también usan diálogo. Consulta, listados y detalles de solo lectura continúan en la página o en paneles.

## Alcance de esta fase

Se añade un servicio de diálogos inyectado y una ventana reutilizable con los recursos Light/Dark existentes. El editor de producto se mueve a un ViewModel nuevo por apertura; la página conserva catálogo, filtros, paginación, el estado de la operación y el detalle de existencias. Guardar correctamente cierra la ventana y refresca el catálogo; un error deja el formulario y su mensaje visibles. Salir con cambios solicita confirmación.

La administración de categorías, la edición de inventario y la gestión de cuentas conservan por ahora sus flujos actuales. Son fases posteriores y requieren una decisión de alcance propia.

## Verificación prevista

Compilación y pruebas de solución, `scripts/verify.ps1` y smoke WPF en Windows, incluidos temas Light/Dark, enlaces, guardado, cancelación y filtros.
