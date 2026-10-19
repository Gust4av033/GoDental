# DEV-12 · Interfaces de Conteos y Proveedores

Incremento del 19/10/2026 en `feature/desarrollo`, sobre los contratos de DEV-09, DEV-10 y DEV-11. Prioriza terminar estos dos flujos antes de empezar Clientes. No cambia SQL, reglas de negocio ni repositorios.

## Conteos físicos

La navegación **Conteos** permite consultar por almacén y estado, paginar, abrir el detalle y crear un conteo parcial. El formulario de apertura busca productos por SKU/nombre, conserva la selección entre páginas y solicita almacén, motivo y confirmación. El servicio valida el límite de 500 productos y SQL exige saldos establecidos e incluye sus lotes.

El detalle presenta almacén, estado, capturadas/total, pendientes, cantidades de sistema y diferencias. Una cantidad vacía permanece pendiente; introducir `0` registra un cero físico. Sólo se envían líneas editadas, en grupos de hasta 1.000, recargando la cabecera y su versión entre grupos. Las cantidades usan punto decimal sin separadores de miles; los validadores existentes deciden rango y precisión.

Cerrar y cancelar requieren motivo y confirmación. El cierre exige además `CanClose`; se bloquea mientras existan cantidades editadas sin guardar. Los conteos finalizados quedan de consulta. `RequiresRecount` muestra la advertencia de movimientos posteriores; el servidor conserva la decisión definitiva al finalizar. Una escritura rechazada por concurrencia recarga el detalle sin reintentar ni rebasar la fotografía.

Si se pierde una respuesta de finalización, se invalida el detalle editable: actualizar el listado y consultar el estado antes de repetir. Si se pierde la respuesta de apertura, el formulario bloquea otro envío y pide consultar el listado. Si la apertura fue confirmada pero falló su recarga, se comunica que el conteo ya se abrió.

## Proveedores

La navegación **Proveedores** ofrece búsqueda por código/nombre/país, inclusión de inactivos, paginación y ficha con pestañas de información general, contactos y referencias comerciales.

Los formularios modales crean copias de los datos y permiten:

- Alta y edición de ficha, dirección, país, correo, teléfono, web y estado activo/inactivo.
- Alta y edición individual de contactos, cargo, contacto principal y estado, conservando los demás contactos. La pantalla explica el cambio de principal en dos guardados.
- Alta y edición de referencias por producto, SKU externo, costo nullable de seis decimales, moneda, MOQ de cuatro decimales, plazo, preferencia y estado. La selección de producto usa el catálogo existente; para buscarlo se necesita permiso de inventario. Se conserva la moneda histórica al desactivar referencias y puede elegirse «Sin moneda» cuando el contrato lo permite.

Cada guardado solicita motivo y confirmación; recarga ficha y subregistros, incluyendo la versión del agregado. Un proveedor inactivo permite editar/reactivar su ficha y consultar sus subregistros; no permite modificar contactos/referencias hasta reactivarlo. El lector de compras consulta sin acciones de escritura; los costos sólo se presentan con `CanManage`, y SQL continúa ocultándolos según permisos reales. Un costo ausente nunca se transforma en cero.

Ante `CONCURRENCY_CONFLICT` o `INVALID_STATE`, el formulario bloquea otro envío y solicita cerrarlo/reabrirlo desde la ficha recargada. Ante pérdida de respuesta, no vuelve a enviar automáticamente: obliga a consultar el resultado. Un cambio confirmado cuya recarga falla se informa como guardado.

## Interfaz, sesión y arquitectura

Se reutilizan recursos Light/Dark y el contenedor modal con encabezado/acciones fijos, desplazamiento interno y confirmación de descarte. Los listados incluyen carga, vacío, mensajes recuperables y tablas con columnas explícitas. El menú lateral permite desplazamiento cuando no cabe a 125 % DPI. Los formularios y cantidades tienen nombres accesibles; Enter en el buscador de productos ejecuta la búsqueda, sin guardar el formulario contenedor.

Los ViewModels reciben los servicios por constructor. `WorkflowViewModel` comparte exclusión de operaciones, mensajes seguros, cancelación de lecturas y comprobación de revisión de sesión. Al cambiar sesión se limpian datos y se ignoran respuestas tardías. Las escrituras no tienen reintentos automáticos ni dependen de un bloqueo local para integridad. El cierre de sesión se deshabilita durante estos formularios/operaciones. SQL, permisos transaccionales, versiones y auditoría siguen en Application/Infrastructure.

## Validación y límites del entorno

Se añaden pruebas xUnit de los ViewModels y formularios enlazando sus archivos independientes de WPF al proyecto de pruebas. Los repositorios controlados sólo viven en `tests/`; no se registran en la aplicación. Cubren cero/pendiente, captura en varios grupos y versiones, confirmación, cierre de consulta, conflicto, respuesta perdida, permisos, cambio de sesión, descarte de copias, versión de contactos, precisión comercial y selección paginada.

Se amplía smoke WPF para Conteos/Proveedores, ambas paletas, formularios, pestañas, lector de compras y tamaño lógico de 1093×614 equivalente a 1366×768 al 125 %. Estas comprobaciones quedan preparadas para Windows; no equivalen a una prueba física de DPI/hardware.

Validación ejecutada: compilación Release de toda la solución con cero errores y cero advertencias; 56 casos xUnit correctos, incluidos 16 casos nuevos de UI/lógica de presentación. `git diff --check` sin errores.

En este entorno Linux se compila con `-p:EnableWindowsTargeting=true`. La ejecución de smoke se bloquea por ausencia de `Microsoft.WindowsDesktop.App`; LocalDB devuelve `PlatformNotSupportedException`; `scripts/verify.ps1 -SqlIntegration` no puede ejecutarse porque no está instalado PowerShell. No se certifica la integración visual/SQL como aprobada por una compilación y pruebas unitarias correctas. La consulta de PR abiertos mediante `gh` también devolvió `Forbidden` desde la API de GitHub; sí se pudieron actualizar las referencias Git y recuperar `feature/desarrollo`.

Antes de integrar en `main`, ejecutar en Windows con SQL disponible:

```powershell
.\scripts\verify.ps1 -SqlIntegration
```

La base de desarrollo necesita la migración 011 según DEV-11. Este incremento no la instala sobre la base del usuario. Clientes, órdenes, recepciones/importaciones, etiquetas, series e inventario inicial continúan fuera de este cambio. No se inicia Clientes hasta completar la validación de estos flujos en Windows.
