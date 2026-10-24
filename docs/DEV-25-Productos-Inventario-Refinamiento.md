# DEV-25 · Refinamiento de Productos e Inventario

20/10/2026 · Incremento sobre `b541bba`, en `feature/desarrollo`, con commits identificados como `productos,inventario`. Se conservan la rama y el refinamiento aprobado de Clientes. Consultados AGENTS, DM-01, DB-01, ARQ-01, INV-01, UX-01, DEV-02, DEV-03, DEV-09 y DEV-12.

## Referencias y composición

Se inspeccionaron las composiciones de `Imagenes/`. Las referencias más próximas son `221124` (tabla de productos por proveedor), `221146` (búsqueda/selección de productos) y `221207` (inventario crítico y próximos vencimientos). Se extiende ese lenguaje a ficha, movimientos y conteos, que no tienen una captura específica completa: título y acción compactos, buscador flexible, filtros desplegables, símbolos de producto, pestañas subrayadas, campos de dos columnas y tablas ligeras. Los símbolos son provisionales; el contrato de catálogo no ofrece fotos.

Los nuevos estilos, estados y geometrías se definen en `Themes/Inventory.xaml`, con los colores dinámicos Light/Dark existentes. Las acciones mantienen 152×36 DIP; las herramientas de icono, 36×36 DIP. Se adapta la composición XAML para ordenar los flujos completos.

## Pantallas y conexiones

- **Productos:** catálogo con búsqueda por Enter, filtros por categoría/marca/stock bajo/inactivos, actualización, precio de referencia USD y disponible físico. El color del disponible distingue agotado, bajo (menor o igual al mínimo del DTO) y con stock; el tooltip nombra ese estado. Se conserva el badge independiente activo/inactivo. Los nombres largos se recortan dentro de su columna y conservan el nombre completo en el tooltip. El disponible físico agregado no se presenta como vendible.
- El nombre del producto, Enter sobre la selección y el doble clic abren su inventario. Se conserva el botón Ver inventario y la edición explícita por permiso. La vista anidada toma visibilidad y comandos del contexto de Productos de forma explícita.
- **Edición de producto:** ventana independiente con identidad/SKU automático, información general, precio/stock y lotes/vencimiento. Campos flexibles, descripción multilínea y alta de categoría en un desplegable secundario. El foco inicial va al nombre. Guardar vuelve al catálogo actualizado para evitar mostrar una ficha con los datos previos.
- **Categorías:** acceso en la cabecera del catálogo al gestor existente; conserva selección protegida ante cambios, nombre, estado, descarte y guardado mediante CategoryService. El alta sigue disponible desde el producto.
- **Ficha e inventario:** cuatro pestañas: ficha del producto, existencias, lotes/vencimientos y Kardex. La ficha presenta datos reales del catálogo; editar usa el comando existente de Productos. El almacén se selecciona y se actualiza explícitamente.
- **Existencias:** tarjetas de físico, reservado, disponible físico y vendible obtenidas de la fila del almacén seleccionado en InventorySnapshot; tabla con los almacenes devueltos por el servicio. Al cambiar de almacén se limpia la consulta hasta actualizar. Costos promedio e históricos con seis decimales USD, visibles sólo con permiso; un costo ausente se muestra como «—».
- **Lotes:** estado devuelto por SQL (vigente, próximo a vencer, vencido, bloqueado, sin fecha o agotado), fechas, cantidad y consulta del costo histórico por permiso. La UI no calcula vencimientos con el reloj del equipo.
- **Kardex:** filtros existentes de fechas, tipo y lote, paginación, cantidades con signo y color, saldo anterior/posterior, usuario, referencia y motivo. Los textos largos se pueden consultar por tooltip. No se editan movimientos.
- **Movimientos:** ventana separada con pestañas de operación y lote, identidad de producto/almacén, destino visible para transferencia, cantidades/costo, lote existente/nuevo y fechas. Motivo y confirmación siguen siendo comunes a registrar o bloquear/reactivar lote.
- **Conteos:** listado separado del detalle. Cabecera con almacén/estado y líneas totales, cantidades guardadas y pendientes del servicio. Captura y cierre/cancelación tienen pestañas independientes. Guardar cantidades permanece en la cabecera, visible al desplazarse por las líneas; Descartar captura se ubica sobre la tabla. Volver o recargar bloquea la pérdida de cantidades editadas; Descartar captura restaura explícitamente la fotografía guardada. Una celda vacía continúa pendiente y cero sigue siendo una cantidad física.
- **Apertura de conteo y selector compartido:** almacén, selección directa con checkbox, contador de seleccionados, paginación, motivo y confirmación. SKU/nombre del selector son de consulta; la selección entre páginas conserva el conjunto existente. Este selector también sirve a las pantallas que ya lo utilizaban.

## Integridad y límites

Se reutilizan ProductService, CategoryService, InventoryService e InventoryCountService. Se conservan validadores, rowversion, versiones de fotografía, permisos SQL, auditoría, costos, confirmación y manejo de respuestas inciertas. Los cambios de selección y presentación no ejecutan SQL ni cambian las reglas transaccionales. La diferencia preliminar de captura existente se conserva; el cierre sigue bajo validación definitiva del servicio/SQL. Al cambiar sesión se limpian el producto y el detalle de conteo.

No se agregan recepción de compras/importaciones, inventario inicial, seriales, mantenimiento/impresión de códigos de barras, fotos ni reportes analíticos sin contratos de aplicación. Las referencias de compras/reportes se usan como guía visual. Se conserva la búsqueda por códigos de barras ya implementada; no se modifica Application, Infrastructure ni la base del usuario.

## Validación

Compilación Release local con `EnableWindowsTargeting=true`, cero errores/advertencias. 146 pruebas xUnit correctas; el nuevo escenario verifica que la navegación de Conteos conserve las cantidades hasta guardar o descartarlas explícitamente, y que cerrar sesión elimine el detalle.

El smoke se amplía con un catálogo poblado exclusivamente mediante fixtures de prueba, niveles de stock, precisión de costos, ejecución del enlace real de fila, conexión del editor, cuatro pestañas del inventario, operación por lote, listado/detalle/cierre de conteos y posiciones de acciones a 1366×768 y 1093×614 DIP. Las ventanas y formularios se renderizan en ambos temas.

Resultado definitivo del código `fc0cde7`: [CI Windows correcto](https://github.com/Gust4av033/GoDental/actions/runs/39457605855), con compilación Release sin errores/advertencias, **146 xUnit**, **349 comprobaciones SQL** (40 de inventario y 67 de conteos, además de la regresión de los demás módulos) y **190 comprobaciones WPF**, sin errores de binding. El smoke confirma la apertura desde el enlace real del catálogo, el comando de edición, los nombres largos contenidos en su columna y Guardar cantidades visible a 1366×768 y 1093×614 DIP en ambos temas. Las capturas quedan en el artefacto de resultados de ese run.

La inspección de las referencias fue directa. Las capturas nuevas las genera el runner; no se presenta esta ejecución como inspección manual de esas imágenes ni como comprobación del DPI físico del monitor del usuario. No se ejecutó F5 en su laptop ni se modificó su base; las suites SQL usan bases aisladas de CI.
