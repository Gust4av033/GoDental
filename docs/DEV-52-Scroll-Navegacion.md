# DEV-52 · Scroll y navegación del espacio de trabajo

Alcance: refinamiento visual de `feature/desarrollo`, conforme a UX-01. Sin cambios de servicios, permisos ni base de datos.

## Decisiones

- El lateral ocupa el alto disponible de la ventana. Marca y pie permanecen fijos; únicamente se desplazan las opciones del menú. El contenido tiene su propio scroll.
- Al cambiar de módulo, el contenido vuelve al inicio. No se conserva un desplazamiento de otra pantalla que pueda ocultar el encabezado o presentar espacios aparentemente vacíos.
- La opción activa del menú debe quedar visible también al navegar desde Inicio, desde otra pantalla o al contraer/expandir el lateral. Mover el contenido no mueve el menú.
- Una tabla o lista interna utiliza primero su propio scroll. En sus extremos, la rueda continúa en el contenedor de página más cercano que tenga recorrido. No se intercepta la edición en campos de texto, desplegables ni combinaciones Ctrl/Shift.
- Cambiar un filtro o refrescar datos dentro del mismo módulo no vuelve al inicio. Las pestañas y los formularios mantienen su comportamiento actual.

## Validación

Compilación Release con `EnableWindowsTargeting=true`: 0 errores y 0 advertencias. Pruebas locales: 418 de aplicación y 22 del servidor correctas.

El smoke WPF incorpora comprobaciones de desplazamiento independiente, navegación desde un lateral desplazado, ajuste de altura hasta 450 DIP, continuidad de la rueda en listas y tablas con scroll lógico, y conservación del comportamiento de campos de texto. Se ejecuta con temas Light/Dark y ventanas de 1366×768 y 1093×614. Ejecución nativa en Windows pendiente de CI; Linux sólo valida compilación y pruebas sin WPF.
