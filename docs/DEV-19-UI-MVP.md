# DEV-19 · Interfaces del recorrido MVP

19 de octubre de 2026 · `feature/ui`, actualizada desde `origin/feature/ui` (`47101ac`) y compatible con `origin/feature/logica` (`bd6681b`). Consultados AGENTS, UX-01, DM-01, DB-01, ARQ-01, SEC-01, VEN-01A, DEV-15 y DEV-17.

## Recorrido conectado

- **Caja:** selector de caja, turno actual o consulta por ID, fondo de apertura, ingreso/retiro con motivo, movimientos paginados y arqueo/cierre con versión leída. Alta/edición de cajas en diálogo, con catálogo real de almacenes.
- **POS:** caja/almacén, búsqueda por código/nombre/SKU, cliente, lista de precio, cantidades/descuentos por línea, descuento global y entrega. El servicio calcula líneas, impuestos, descuentos, umbrales y total. Cobro en diálogo independiente, con pagos mixtos, referencias, efectivo recibido separado del aplicado y cambio obtenido de la venta confirmada. Descuentos administrativos se autorizan puntualmente sin cambiar la sesión ni conservar contraseñas.
- **Cotizaciones:** emisión con vigencia/motivo; página de consulta y detalle. Recuperación de carrito vacío en POS, recálculo y revisión de precios actuales, enlace de origen y nueva autorización cuando corresponde. El carrito vinculado se conserva; para cambiar sus productos/cliente/caja se debe vaciar e iniciar otra operación. Emitir desde POS conserva también el enlace para su posterior conversión. No reserva existencias.
- **Crédito:** cuentas/vencidos paginados, selección de cuenta y registro de abono en diálogo, con referencia, turno para efectivo y versión de CxC. Recarga saldo e historial de abonos del cliente después de confirmar. Caja y ficha de Clientes se vuelven a consultar al navegar a ellas.
- **Pedidos:** listado y detalle en pestañas, alta con selección separada de cliente/dirección/productos y datos de entrega; enlace de venta por ID; transiciones permitidas por `OrderRules`, con versión/motivo e historial paginado. Estado de pago y saldo vienen del servicio.
- **Ventas y PDF:** listado, detalle de líneas/pagos, efectivo/cambio y exportación mediante `SaleReceiptService` a un destino elegido. PDF interno, sin simular DTE ni enviar comunicaciones.

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
- [CI Windows #49](https://github.com/Gust4av033/GoDental/actions/runs/39330815005) aprobó la primera entrega (`8123688`): Release, 132 pruebas xUnit, suites SQL completas y 123 comprobaciones WPF, sin errores de binding. Los ajustes posteriores deben validarse también en CI. No se considera validado el recorrido manual entre dos estaciones ni el DPI físico por compilar/renderizar a tamaños compactos.

El refinamiento avanzado permanece para fase dos. No se agregaron compras, devoluciones, fiscal real, saldos iniciales ni códigos de barras en estas pantallas.
