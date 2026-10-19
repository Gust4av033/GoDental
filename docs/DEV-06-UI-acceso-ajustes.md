# DEV-06 · Acceso separado y ajustes

## Decisión de interfaz

La aplicación abre una ventana de acceso antes de mostrar el espacio de trabajo. Esta ventana contiene el inicio de sesión, el alta del primer administrador y el cambio obligatorio de contraseña que ya existen. Al iniciar sesión se muestra el shell; al cerrar sesión se vuelve a la ventana de acceso. «Mi cuenta» permanece dentro del shell para iniciar el cambio voluntario de contraseña.

La configuración del servidor, la comprobación de conexión y la preferencia de tema se reúnen en una vista «Ajustes». Está disponible como página del shell y desde la ventana de acceso mediante una ventana modal, porque la conexión debe poder repararse antes de iniciar sesión. Ambas superficies comparten el mismo ViewModel y formulario; no se duplican reglas ni almacenamiento de configuración.

Se reutilizan los tokens Light/Dark y las acciones actuales. Las ventanas deben caber en 1366×768 con la barra de tareas visible y desplazar el contenido cuando la escala sea 125/150 %.

## Verificación

La compilación cruzada Release de `GoDental.sln` con .NET 8 y `EnableWindowsTargeting=true` terminó con 0 errores y 0 advertencias. Pasaron los 20 tests ejecutables en Linux. Los smoke WPF ahora renderizan acceso y ajustes en Light/Dark, y comprueban la transición de ventanas, el propietario modal de Ajustes y el estado vacío del catálogo.

Pendiente de ejecutar `scripts/verify.ps1` y revisar visualmente el flujo a 1366×768 y escalas 125/150 % en Windows. La compilación cruzada no confirma la ejecución de ventanas WPF.
