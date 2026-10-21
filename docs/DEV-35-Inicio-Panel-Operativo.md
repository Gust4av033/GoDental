# DEV-35 · Inicio y panel de operación

21/10/2026 · `feature/desarrollo`, sobre la rama actualizada hasta `ae441ac`. Se consultan AGENTS, DM-01 §14, DB-01, ARQ-01, UX-01, DEV-15 y DEV-30. Se conservan los incrementos recientes de recuperación, compras, impresión y marca.

## Diseño previo a implementación

Referencia inspeccionada: `Imagenes/Captura de pantalla 2026-10-18 221207.png`, pantalla 1 (Dashboard general). Encabezado compacto con saludo/fecha, cuatro indicadores con icono y dos paneles de actividad. Se sustituyen las tarjetas grandes de accesos y la explicación de inventario por un resumen útil, con composición adaptable y recursos compartidos Light/Dark.

- `HomeView` separa Inicio del XAML del shell. El saludo usa el primer nombre; la identidad completa permanece en el encabezado de sesión. Actualizar y los accesos operativos conservan tamaños uniformes y permisos.
- `HomeViewModel` consulta servicios existentes sin modificar filtros, selección, páginas, carrito o fichas abiertas de otros módulos. Recarga al entrar, al iniciar sesión y por petición explícita; no hay polling.
- Indicadores: **ventas registradas** (total histórico de `SalesService.SearchAsync`, todos los estados del contrato actual), **pedidos registrados** (`OrderService.SearchAsync(null, ...)`, todos los estados), **productos activos** (`ProductService.SearchAsync` sin filtros ni inactivos) y **stock bajo o agotado** (mismo servicio con `LowStockOnly=true`, según su criterio de mínimo). Se usa el `Total` devuelto; no se calcula stock, suman ventas ni estiman ingresos en WPF.
- Actividad: hasta cinco ventas/pedidos recientes según el orden del servicio (ID descendente). Los importes, fechas y estados son los del DTO; venta conserva cliente histórico. Los enlaces de fila releen la ficha con los comandos existentes. No se muestra un total histórico como ventas del día/mes, ventas confirmadas exclusivamente ni pedidos pendientes.
- Se ocultan indicadores y consultas sin permiso. Carga, vacío, fallo parcial y resultado sin consultar se distinguen; `—` no equivale a cero. Cerrar/cambiar sesión limpia datos y descarta respuestas antiguas, incluso si llega una nueva consulta antes de terminar la anterior.
- Accesos a POS, Caja, pedidos, productos, clientes y Ajustes usan navegación existente; compras y crédito conservan sus entradas del menú. Inicio sólo lee; no abre caja, confirma ventas, resuelve pendientes ni cambia inventario. Las protecciones de OperationId y recuperación se conservan.

No hay cambios de SQL ni migraciones. Analítica por período, evolución/comparativas, vencimientos agregados y rentabilidad requieren contratos adicionales; se registran en DEV-15 para un incremento posterior, sin gráficas ni cifras simuladas.

## Validación

Compilación Release local con `EnableWindowsTargeting=true`, sin errores/advertencias; **223 pruebas xUnit correctas**, incluidas cinco nuevas de Inicio: consultas acotadas/sin filtros heredados, permisos, fallo parcial sin ceros falsos, respuesta tardía de otra sesión y vacío real.

Smoke Windows ampliado con Inicio en Light/Dark a 1366×768 y 1093×614 DIP; estado cargando, vacío y fallo parcial; acceso desde ventas/pedidos, dimensiones uniformes, columnas adaptables y acción POS visible. Resultado del código `0f97616`: [CI Windows #123 correcto](https://github.com/Gust4av033/GoDental/actions/runs/39689099445), Release sin errores/advertencias, **223 xUnit, 477 comprobaciones SQL y 681 WPF**, sin errores de binding. Los indicadores autorizados caben y mantienen alturas uniformes; POS conserva 152×36 DIP y permanece visible; las columnas de actividad cambian según el ancho. En el render completo de 1366×768, el borde inferior de los paneles queda a 709 DIP, dentro del área de contenido. Se comprueban fichas de venta/pedido, carga, vacío y fallo parcial.

Capturas y resultados en [test-results-123](https://github.com/Gust4av033/GoDental/actions/runs/39689099445/artifacts/12388425759). Las referencias se inspeccionan directamente; las capturas WPF automáticas no certifican el monitor ni DPI físico. SQL se valida en bases aisladas de CI.
