# DEV-19 · Interfaces del recorrido MVP

19 de octubre de 2026 · `feature/ui`, actualizada desde `origin/feature/ui` (`47101ac`) y compatible con `origin/feature/logica` (`bd6681b`). Consultados AGENTS, UX-01, DM-01, DB-01, ARQ-01, SEC-01, VEN-01A, DEV-15 y DEV-17.

Antecedente histórico; el desarrollo vigente continúa en `feature/desarrollo`. DEV-26 registra la composición visual posterior. Las tres mejoras de negocio siguientes siguen pendientes; las correcciones de interfaz autorizadas se distinguen de ellas.

## Recorrido conectado

- **Caja:** selector de caja, turno actual o consulta por ID, fondo de apertura, ingreso/retiro con motivo, movimientos paginados y arqueo/cierre con versión leída. Alta/edición de cajas en diálogo, con catálogo real de almacenes.
- **POS:** caja/almacén, búsqueda por código/nombre/SKU, cliente, lista de precio, cantidades/descuentos por línea, descuento global y entrega. El servicio calcula líneas, impuestos, descuentos, umbrales y total. Cobro en diálogo independiente, con pagos mixtos, referencias, efectivo recibido separado del aplicado y cambio obtenido de la venta confirmada. Descuentos administrativos se autorizan puntualmente sin cambiar la sesión ni conservar contraseñas.
- **Cotizaciones:** emisión con vigencia/motivo; página de consulta y detalle. Recuperación de carrito vacío en POS, recálculo y revisión de precios actuales, enlace de origen y nueva autorización cuando corresponde. El carrito vinculado se conserva; para cambiar sus productos/cliente/caja se debe vaciar e iniciar otra operación. Emitir desde POS conserva también el enlace para su posterior conversión. No reserva existencias.
- **Crédito:** cuentas/vencidos paginados, selección de cuenta y registro de abono en diálogo, con referencia, turno para efectivo y versión de CxC. Recarga saldo e historial de abonos del cliente después de confirmar. Caja y ficha de Clientes se vuelven a consultar al navegar a ellas.
- **Pedidos:** listado y detalle en pestañas, alta con selección separada de cliente/dirección/productos y datos de entrega; enlace de venta por ID; transiciones permitidas por `OrderRules`, con versión/motivo e historial paginado. Estado de pago y saldo vienen del servicio.
- **Ventas y PDF:** listado, detalle de líneas/pagos, efectivo/cambio y exportación mediante `SaleReceiptService` a un destino elegido. PDF interno, sin simular DTE ni enviar comunicaciones.

## Mejoras pendientes del recorrido de pedidos

- **Prioridad alta: Pasar al POS / Crear venta desde pedido.** Sustituir el ingreso habitual del ID manual por la precarga de cliente, productos, cantidades y cargo de entrega, conservando la referencia al pedido. El POS recalcula precios/promociones/descuentos y permite el cobro normal; una venta confirmada se vincula automáticamente al pedido con las validaciones de compatibilidad existentes. `Vincular venta` manual permanece como recuperación/compatibilidad. Coordinación, versiones, `OperationId` y recuperación del enlace deben definirse antes de implementar, según DEV-17.
- **Regla operativa recomendada:** **RECIBIDO → PREPARANDO → CONFIRMAR VENTA → LISTO → EN_RUTA → ENTREGADO**. Confirmar venta es un paso, no otro estado. El pedido no reserva ni descuenta stock; `RECIBIDO`, `PREPARANDO` y `LISTO` no representan reservas. Antes de considerar listo el pedido para salir debe tener venta confirmada asociada; ésta aplica inventario, lotes, Kardex y pago/crédito. Hoy sólo `EN_RUTA` y `ENTREGADO` exigen esa venta mediante validación implementada. La recomendación no cambia todavía `OrderRules` ni WPF. Reservas ligeras: evolución futura si el negocio las necesita.
- **Cobro contra entrega:** pendiente de diseño, no un método nuevo disponible ni crédito comercial automático. No debe consumir límite, generar mora, alterar indicadores de crédito ni crear CxC comercial ordinaria por unas horas de reparto. Antes de diseñar la pantalla deben resolverse protección de unidades en ruta, descuento definitivo de inventario, confirmación de venta, rechazo/fallo de entrega, registro del efectivo, efecto en Caja y devolución de disponibilidad. Ver DEV-17.

### Correcciones de interfaz autorizadas · 20/10/2026

El usuario amplió expresamente el alcance para resolver estos dos problemas, manteniendo las tres decisiones anteriores sólo documentales:

- **Datos del cliente:** seleccionar un cliente consulta su ficha y precarga contacto/teléfono y dirección de entrega. Sugiere una opción principal única o la única disponible; con varias opciones ambiguas exige elegir, sin asumir exclusividad de «principal». Datos de entrega permite revisar/corregir los valores sólo para el pedido. Editar la dirección retira su ID guardado y envía la dirección específica; no modifica la ficha del cliente. Cambiar de cliente limpia los datos anteriores y descarta respuestas tardías. Los campos de entrega se bloquean durante la carga; el registro espera a que termine y conserva la validación/snapshot existentes del servicio.
- **Existencias iniciales:** se conecta la UI a `InventorySetupService.InitializeAsync(InitialInventory)` ya implementado. Acceso: **Productos → ficha/inventario → almacén → Actualizar → Existencias iniciales**, cuando la consulta no devuelve saldo para ese almacén y se tiene permiso de ajuste. El formulario solicita cantidad física, costo unitario USD, motivo y lote/vencimiento según producto. SQL sigue exigiendo ausencia de saldo e historial y genera Kardex/auditoría. No se modifica stock desde el maestro ni se agrega SQL. La solicitud incierta conserva su GUID y bloquea nuevas operaciones, cierre y sesión hasta resolverla; al confirmar se recargan existencias y catálogo.

La selección de cliente/dirección original no equivalía a precargar todos los campos, y el ajuste ordinario requería una versión de saldo que aún no existía. Se conectan los contratos existentes; no se implementan Pasar al POS, reservas ni cobro contra entrega. Validación ampliada en DEV-26.

## Presentación

Seis páginas de navegación propias y diálogos para cada escritura; no se agrupan todos los formularios en una sola pantalla. Recursos Light/Dark compartidos. Botones de acciones y pies de diálogo de 152×40 DIP, con estilos ActionButton/PrimaryActionButton. Campos redondeados del sistema existente y desplegables con opciones redondeadas, foco, selección y apertura discreta. Tablas de consulta de sólo lectura; carga, estado vacío, errores y paginación explícitos. La navegación lateral se desplaza de forma independiente.

POS requiere VENTAS.VER y CAJA.VER para seleccionar la caja del catálogo existente. Seleccionar cliente requiere CLIENTES.VER; gestionar cajas requiere AJUSTES.EDITAR e INVENTARIO.VER para consultar almacenes. Abonos y acciones administrativas aplican los permisos de DEV-15/17; Application/SQL mantienen la validación definitiva. No se modificaron servicios, SQL ni contratos.

## Operaciones inciertas

`MvpOperationModel` conserva la solicitud completa y su GUID en memoria mientras su resultado sea incierto. Deshabilita nuevas escrituras en ese flujo y permite **Consultar estado** o **Reenviar solicitud**, siempre con los mismos datos y GUID. No hay reintentos automáticos. Un resultado de consulta nulo no habilita crear otra operación; tampoco se descarta una solicitud incierta porque un reenvío posterior sea rechazado. La resolución negativa definitiva requiere el contrato pendiente descrito en DEV-15. La confirmación se separa de la recarga: un fallo de lectura posterior nunca vuelve a abrir una escritura confirmada. Ante conflicto/estado incompatible se bloquea el formulario anterior y se pide volver, actualizar y revisar.

Se bloquean el cierre normal de la aplicación y el cierre de sesión mientras haya solicitudes pendientes o formularios/procesos activos. No se persisten contraseñas; para reenviar una autorización incierta se vuelve a introducir la clave con la misma solicitud y usuario administrativo. Un cambio de sesión invalida formularios/resultados anteriores. El cierre forzado del proceso no es recuperable desde memoria; antes de registrar otra operación se debe revisar el estado de negocio. La persistencia local de solicitudes ante caída del proceso queda fuera de este incremento.

## Validación

- Release de toda la solución con `EnableWindowsTargeting=true`: 0 errores, 0 advertencias.
- 139 casos xUnit correctos en Linux, incluidos 23 nuevos de presentación MVP.
- Smoke ampliado para las seis páginas, ventanas compactas, diálogos y controles en Light/Dark. Intento local bloqueado por ausencia de `Microsoft.WindowsDesktop.App` en Linux.
- [CI Windows #49](https://github.com/Gust4av033/GoDental/actions/runs/39330815005) aprobó la primera entrega (`8123688`): Release, 132 pruebas xUnit, suites SQL completas y 123 comprobaciones WPF, sin errores de binding. [CI Windows #50](https://github.com/Gust4av033/GoDental/actions/runs/39331437129) aprobó el código final `10610a8`: Release sin errores/advertencias, **139 xUnit**, **329 comprobaciones SQL** (102 MVP) y **123 comprobaciones WPF**, sin errores de binding. Las bases SQL son aisladas del runner; no se alteró la base del usuario. No se considera validado el recorrido manual entre dos estaciones ni el DPI físico por compilar/renderizar a tamaños compactos.

El refinamiento avanzado permanece para fase dos. No se agregaron compras, devoluciones, fiscal real, saldos iniciales ni códigos de barras en estas pantallas.
