# DEV-54 · Navegación interna de Online y carga visible

24/10/2026. Continúa DEV-47/48 y DEV-52; referencias UX-01, DM-01 y ARQ-01.

## Decisiones de UI

- Online ocupa el alto útil del módulo. Su menú interno y su contenido tienen scroll independiente: las secciones permanecen disponibles mientras se revisan mapas, tablas o fichas largas.
- El menú también permite desplazamiento cuando la ventana es pequeña. Cambiar de sección vuelve al encabezado de su contenido y mantiene accesible la opción activa. Se conservan enlaces, permisos, selección y comandos existentes.
- El indicador de carga usa el azul `PrimaryBrush` y un fondo del tema, sin el verde nativo de Windows. Se comparte en páginas y diálogos, con animación sólo cuando está visible.
- La cabecera del espacio de trabajo muestra la carga de la página activa y permanece visible al bajar. Los indicadores internos se ocultan cuando hay uno fijo equivalente; fuera de ese host siguen funcionando.
- La ventana independiente de Ajustes dispone del mismo indicador fijo. Los diálogos conservan su pie de acciones y carga visible.
- No se incorporan métricas ficticias, servicios, operaciones nuevas ni cambios SQL. El alcance es presentación e interacción, con recursos compartidos y temas claro/oscuro.

## Validación

Compilación Release local con `EnableWindowsTargeting=true`: 0 errores y 0 advertencias. Pruebas: 429 de aplicación y 23 de servidor correctas; las 16 específicas de Windows quedan para CI.

El smoke incorpora casos nativos de Online en 1366×768 y 1093×614 con ambos temas, reducción a 860×480, navegación de teclado y acceso a las diez secciones; menú fijo, carga azul persistente, sin duplicados, estado por página y representación de progreso determinado. También comprueba Ajustes independiente en 760×480. Validación Windows pendiente de CI. No requiere migración SQL.
