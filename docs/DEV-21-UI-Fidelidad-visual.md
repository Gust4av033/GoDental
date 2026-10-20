# DEV-21 · Ajuste de interfaz a las capturas de referencia

19/10/2026, hora de El Salvador. Trabajo en `feature/ui`, actualizado desde `origin/feature/ui` (`8291049`). La punta de lógica `184e1cf` ya está incorporada en esa base. Consultados DM-01, DB-01, ARQ-01, UX-01 y los documentos de inventario, clientes, proveedores y operación MVP.

## Referencia y alcance

Las ocho composiciones proporcionadas por el usuario muestran superficies blancas sobre celeste, navegación lateral compacta, azul vivo, esquinas pequeñas, filtros con lupa, tablas ligeras, pestañas subrayadas y etiquetas de estado. Se aplica ese sistema a las ventanas existentes; no se crean los módulos futuros ilustrados en las composiciones.

- Recursos compartidos: azul `#007BFF`, fondo `#EAF6FC`, sidebar `#F2FAFF`, campos y botones con radio de 4 DIP, tarjetas de 6 DIP, acciones de 36 DIP y ancho mínimo de 120 DIP que crece con su contenido; tablas con filas de 38 DIP. Las acciones dejan de estirarse verticalmente dentro de los filtros.
- Shell: menú de 184 DIP, modo compacto de 64 DIP, barra superior de 44 DIP y contenido blanco sobre fondo celeste. El símbolo dental es vectorial y provisional; la identidad final de UX-01 sigue pendiente.
- Listados: búsqueda con lupa y ayuda visual, fechas e importes con formato legible, paginación compacta, avatares de cliente/proveedor y etiquetas de activo/inactivo y otros estados. Los estados desconocidos conservan su texto y presentación neutra.
- Clientes y proveedores: listado y ficha en vistas separadas, regreso que conserva filtros y selección, actualización independiente de la selección del listado, encabezado de identidad/estado y datos generales en columnas. Se conservan las ocho pestañas del cliente y las tres del proveedor. Alta/edición permanece en diálogos.
- POS: selección de caja/lista/cliente arriba, catálogo o carrito a la izquierda, totales y cobro a la derecha. El resumen usa el cálculo del servicio existente. No se reconstruyen impuestos, descuentos ni disponibilidad en presentación.
- Caja: controles compactos, resumen en tarjetas y acciones junto al turno. Cobro: total en panel lateral y pagos mixtos con los controles existentes.
- Ajustes: pestañas de conexión y apariencia con guardado de conexión y preferencia de tema separados.
- Se corrige el desplegable compartido para respetar el selector de plantilla de `DisplayMemberPath`; la selección de caja muestra su nombre, en lugar del nombre de la clase C#.
- Ambos temas conservan recursos para estados, foco y controles. Se mantiene el host nativo de texto/contraseña, el popup de selección, el calendario, la selección de filas y la navegación por teclado. La selección de una pestaña colorea su encabezado sin teñir todo el formulario.

## Verificación

Las capturas de `TestResults/UiPreviews/` se generan con datos controlados exclusivos de pruebas; no se añaden datos ficticios a la aplicación ni se modifica la base del usuario. Se conserva una comparación local anterior en `TestResults/UiBeforeReference/`, también ignorada por Git.

El smoke cubre Light/Dark, ventanas de 1366×768 y 1093×614 DIP, fichas/listados y catálogo/carrito. Verifica el nombre visible de objetos en desplegables, el tamaño de las acciones, la visibilidad de Cobrar a 1366×768, los enlaces WPF y el cierre real del proceso. Las pruebas de navegación comprueban regreso al listado, conservación de filtros/selección, actualización sin selección y limpieza al cambiar sesión. Los tamaños compactos son comprobaciones de distribución lógica, no una certificación de DPI físico en todos los monitores.

No se cambian SQL, contratos, reglas, permisos ni gestión de solicitudes financieras pendientes. La validación correspondiente es `scripts/verify.ps1`; este incremento no requiere instalar migraciones.

Resultado en Windows: compilación Release sin advertencias ni errores, **142 casos xUnit** y **134 comprobaciones WPF** correctas, sin errores de binding. Se revisaron las capturas de catálogo/carrito, clientes, proveedores, caja y cobro en ambos temas. Se corrigió además el segundo pase de medida del arnés al cambiar de pestaña para que las columnas virtualizadas aparezcan completas en las capturas.
