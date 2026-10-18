# DEV-05 · Flujos de interfaz y apariencia

## Decisiones

- El tema claro/oscuro se controla mediante un interruptor accesible y reutilizable en la barra superior y la configuración. Cambia al instante y guarda la preferencia en el perfil local sin guardar cambios pendientes del servidor o la base de datos.
- La administración de categorías, la creación de cuentas y la administración de cuentas usan diálogos. Las operaciones siguen en los ViewModels y servicios existentes; las contraseñas permanecen en `PasswordBox` y sólo se entregan al servicio al ejecutar la acción.
- El detalle de existencias y Kardex es consulta: sustituye temporalmente el catálogo en el área de contenido y ofrece «Volver al catálogo». Registrar un movimiento o cambiar el estado de un lote abre un diálogo con encabezado y acciones fijas.
- Los diálogos de escritura conservan el mensaje de error visible, bloquean el doble guardado y piden confirmación al cerrar con cambios. Los formularios de categorías impiden cambiar de registro mientras hay cambios sin guardar; se pueden descartar desde el propio formulario.
- Se usan únicamente los tokens actuales Light/Dark. No se añaden colores, marca ni iconos nuevos.

## Alcance técnico

Esta entrega modifica presentación WPF y smoke tests. No cambia reglas de negocio, SQL, contratos de Application ni migraciones. La lógica existente queda conectada a los nuevos controles y diálogos.

## Verificación

La compilación cruzada Release de `GoDental.sln` con .NET 8 y `EnableWindowsTargeting=true` terminó con 0 errores y 0 advertencias. Los 20 tests de `GoDental.Tests` pasaron en Linux. Se añadieron smoke para los nuevos diálogos en Light/Dark, el interruptor de tema y los flujos de guardado y error; al ejecutar el binario en Linux falta el runtime `Microsoft.WindowsDesktop.App` 8.0.

La ejecución de `scripts/verify.ps1`, los smoke WPF y la revisión visual a 1366×768 y escala 125/150 % requieren Windows. Registrar aquí los resultados tras ejecutarlos.
