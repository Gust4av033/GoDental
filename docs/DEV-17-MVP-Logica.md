# DEV-17 · Lógica del primer MVP operativo

Incremento autorizado para `feature/logica`. El objetivo es cerrar el recorrido entrada inicial → cliente → caja → presupuesto/precio → venta → cobro/crédito → comprobante → pedido/entrega → cierre. El refinamiento visual corresponde a `feature/ui`. No se considera validación de SQL una compilación ni se simulan respuestas fiscales.

Las referencias a `feature/logica` y `feature/ui` describen el incremento histórico. La rama vigente es `feature/desarrollo`; el bloque de pedidos distingue la conexión implementada al POS de las reservas y el cobro contra entrega pendientes.

## Decisiones y límites

- Se reutilizan las tablas y procedimientos 001–011. La migración aditiva 012 define `sis.OperacionesMvp` (IdOperacion GUID PK, Tipo, IdUsuario FK, HashSolicitud binary(32), EntidadId bigint, Fecha). Cada escritura nueva recibe un GUID estable y motivo; un bloqueo transaccional por GUID permite repetir la misma solicitud sin duplicar movimientos. Reutilizar GUID con otro usuario/tipo/contenido se rechaza. El registro se confirma en la misma transacción que el negocio y la auditoría; no hay reintentos automáticos de escrituras.
- 012 agrega RowVersion a turnos y CxC; a ventas, efectivo recibido, cambio, cargo de entrega y snapshot de cliente; a detalles, trazabilidad JSON del precio; a cotizaciones, RowVersion y snapshot JSON del carrito/cálculo; a autorizaciones, token del cálculo y marca de consumo. No modifica scripts históricos.
- Caja: catálogo y alta/edición bajo CAJA.VER + AJUSTES.EDITAR; una caja con historial conserva almacén/código. Apertura, cierre y movimientos usan permisos existentes. Sólo un turno abierto por caja. Ingresos y retiros manuales tienen motivo; un retiro no deja efectivo esperado negativo. Todo cambio de efectivo avanza versión del turno. Un cierre exige la versión leída y motivo si hay diferencia. Efectivo esperado = apertura + ventas/ingresos/ajustes positivos − retiros/devoluciones; no incluye transferencias/QR/depósitos/crédito.
- La carga inicial usa el ajuste existente con versión vacía, únicamente cuando todavía no hay saldo del producto/almacén. Exige costo y motivo, conserva lotes/vencimientos y deja Kardex/auditoría. Códigos de barras usan el esquema existente; el código interno generado es Code128 textual `GD` + IdProducto, sin fingir un EAN.
- Precios se calculan en una única política de Application con datos consultados de SQL: precio público → lista explícita del carrito o asignada al cliente; en su ausencia, lista elegible del tipo por prioridad, mínimo de compra y cantidad → mejor promoción válida → descuento manual de línea y global. El mínimo de compra compara el subtotal público previo a promociones/descuentos. Una línea de carrito por producto evita eludir mínimos al dividir lotes.
- Promociones de precio fijo/porcentaje por producto: no acumulables comparan contra el precio público; un porcentaje expresamente acumulable puede aplicarse al precio del cliente. Se elige un único mejor candidato, nunca se encadenan promociones. COMBO/OTRO aplicable se rechaza como configuración no soportada en este MVP, no se ignora para cobrar otro precio.
- Se soporta un impuesto activo por producto, incluido o añadido según catálogo. No se inventa IVA fijo ni reglas fiscales. El cálculo redondea importes monetarios a dos decimales, away-from-zero, por línea; conserva neto, impuesto, descuento y total exactos en JSON. Más de un impuesto aplicable requiere la ampliación posterior y se rechaza explícitamente.
- Descuento efectivo = 1 − (1 − descuento de línea) × (1 − descuento global). Se evalúa sin redondear al decidir permisos/justificación/autorización. Los umbrales proceden de configuración existente. Más del umbral administrativo exige reautenticación con el gateway existente, permiso administrativo real y autorización vinculada al token del cálculo, de un solo uso y validez de 10 minutos. Nunca se persiste la contraseña ni forma parte del hash de idempotencia.
- Confirmar vuelve a calcular bajo bloqueos y exige el token del precio aceptado. No acepta precios, impuestos ni totales calculados por WPF. La asignación de lotes es FEFO, o lote solicitado válido; se excluyen lotes inactivos/vencidos, series y unidades fraccionarias cuando no corresponden. El saldo agregado respeta reservas. Se llama `sp_ConfirmarVenta` dentro de la transacción para aplicar inventario/Kardex/pagos/caja/CxC. 012 amplía su total para incluir el cargo de entrega y endurece la igualdad de pagos y el vencimiento de lotes.
- Pagos: importes aplicados deben sumar exactamente el total. El efectivo recibido se registra aparte; el cambio sólo procede de efectivo y caja recibe el importe neto. Transferencia, QR y depósito registran referencias según catálogo. Tarjeta/procesador sigue fuera del MVP; crédito requiere cliente y permiso existente, límite/habilitación/bloqueo finales en SQL. Cargo de entrega es un importe comercial final separado; no se atribuye una regla fiscal al transporte sin configuración aprobada.
- Cotizaciones no reservan ni descuentan stock. En este primer flujo se cotiza disponibilidad existente; presupuestar artículos sin stock se difiere. Guardan cálculo y carrito; emitirlas exige también permisos de descuento y autorización de un uso cuando corresponda. La conversión exige vigencia/estado y conserva el enlace, con precios y disponibilidad revalidados antes de cobrar. Una cotización sólo puede convertirse una vez; una autorización consumida al emitirla no autoriza una venta posterior.
- Abonos usan el procedimiento existente, versión de CxC, permiso CAJA.MOVIMIENTO + CLIENTES.VER + VENTAS.VER, método real distinto de crédito/tarjeta y caja abierta si es efectivo. No admiten sobrepago ni cuentas castigadas. Los vencidos se consultan por fecha del servidor, sin inventar bloqueo automático o enviar comunicaciones.
- Pedidos/entregas: 013 añade `ven.PedidosMvp`, `ven.PedidoLineasMvp`, `ven.PedidoEventosMvp`. Cabecera con cliente/contacto, snapshot de dirección y referencias, responsable usuario o nombre libre, fecha prevista, cargo de entrega, venta nullable, estado, fecha/persona de entrega y RowVersion. Líneas conservan producto/SKU/nombre/cantidad; eventos son append-only. Estados RECIBIDO → PREPARANDO → LISTO → EN_RUTA → ENTREGADO, con entrega directa desde LISTO; CANCELADO/INCIDENCIA conservan motivo y eventos. Incidencia puede volver a PREPARANDO/LISTO/EN_RUTA. Cancelar no anula una venta ni revierte cobros. Se usa VENTAS.VER para leer y VENTAS.CREAR para gestionar, sin permisos nuevos.
- Un pedido no reserva inventario ni representa un pago. Se enlaza a una venta confirmada del mismo cliente, productos/cantidades y cargo; pago pendiente se deriva de la venta/CxC, sin permitir marcar “pagado” manualmente. EN_RUTA y ENTREGADO requieren esa venta confirmada para que la salida tenga inventario y cobro/crédito registrados; se permite saldo financiado. El cambio posterior de dirección del cliente no altera el snapshot. Una venta sólo se enlaza a un pedido. Entregar registra fecha, usuario y observación, sin tracking/GPS obligatorio. Las líneas exigen productos y unidades compatibles con el POS del MVP.
- Comprobante PDF operativo mediante QuestPDF, obtenido del snapshot de la venta confirmada, claramente interno y separado de DTE. Fiscal permite registrar FE/CCFE pendiente para una venta sin transmitir ni declarar aceptación; las credenciales, firma, esquemas y transporte reales siguen fuera de este MVP y de cualquier certificación productiva.

## Decisiones vigentes · Pedidos → Ventas → Entregas

### 1. Pasar al POS / Crear venta desde pedido · implementación autorizada

**Pasar al POS / Crear venta desde pedido** es el recorrido habitual implementado: carga cliente, productos, cantidades y cargo de entrega, conserva la referencia al pedido y recalcula precios, promociones y descuentos con la lógica normal del POS. El usuario revisa el cálculo y cobra normalmente; tras confirmar la venta, ésta queda vinculada automáticamente al pedido. El ID manual se conserva como opción secundaria.

El enlace conserva las validaciones existentes de cliente, productos, cantidades y cargo de entrega, además de permisos, versiones y auditoría. `Vincular venta` manual queda como mecanismo secundario de recuperación/compatibilidad.

Diseño autorizado antes de implementar (20/10/2026): `ConfirmSale` admite un origen opcional `SaleOrderOrigin(OrderId, ExpectedVersion)`, incompatible con `QuotationId`. Application valida y copia la versión; el POS conserva el origen al recalcular y reenviar. Infrastructure bloquea primero el pedido con `UPDLOCK,HOLDLOCK`, verifica versión, ausencia de venta y estado no finalizado; ejecuta la confirmación habitual y reutiliza la validación del enlace manual dentro de **la misma transacción**. Actualización de `IdVenta`, evento del pedido, inventario/lotes/Kardex, pagos/crédito/Caja, recibo por GUID y auditoría se confirman juntos o se revierten juntos. El recibo sigue identificando la venta. Dos GUID distintos no pueden confirmar dos ventas para el mismo pedido; reenviar el GUID original devuelve el mismo recibo incluso si avanzó la versión del pedido. No hay segunda escritura de enlace desde WPF ni migración nueva: se reutilizan las tablas de 012/013 y su índice único de venta vinculada.

La UI lee nuevamente el pedido al llevarlo al POS, carga sus datos sólo después de recalcular correctamente y nunca sustituye un carrito en curso. Conserva cliente, productos, cantidades y entrega; permite caja/lista y descuentos con el cálculo habitual. Vaciar cancela únicamente el borrador local. Confirmar no cambia el estado operativo del pedido. Las reglas de entrega y los pendientes de reservas/cobro contra entrega permanecen intactos.

Implementación y validación en `feature/desarrollo`: [CI Windows #90](https://github.com/Gust4av033/GoDental/actions/runs/39498220393), código `b37bfd8`, con 163 xUnit, 372 comprobaciones SQL (125 MVP) y 327 WPF correctas. Incluye rollback después de vincular, compatibilidad completa, reenvío/consulta del GUID y dos solicitudes concurrentes que sólo generan una venta para el pedido. Detalle visual y límites de la evidencia en DEV-26.

### 2. Pedido y stock · recorrido operativo recomendado

**Un pedido no reserva ni descuenta inventario.** Para la operación pequeña actual se recomienda:

**RECIBIDO → PREPARANDO → CONFIRMAR VENTA → LISTO → EN_RUTA → ENTREGADO**

`CONFIRMAR VENTA` es un paso comercial, no un nuevo estado del pedido. `RECIBIDO` y `PREPARANDO` no significan stock reservado; **LISTO tampoco representa una reserva**. Antes de considerar realmente listo un pedido para salir debe existir una venta confirmada asociada. Esa venta es la operación que descuenta inventario, asigna lotes, genera Kardex y registra pago/crédito.

Esta recomendación no cambia las transiciones vigentes: hoy la validación implementada exige la venta confirmada para `EN_RUTA` y `ENTREGADO`; no se afirma que `LISTO` ya tenga ese bloqueo. No se implementa un sistema complejo de reservas. Una reserva ligera de stock queda como evolución futura únicamente si la operación del negocio la necesita.

### 3. Cobro contra entrega · decisión pendiente de diseño

Representa productos que salen a reparto y cuyo dinero se recibe al entregarlos. No debe tratarse automáticamente como crédito comercial: no debe consumir límite, generar mora, alterar indicadores de clientes con crédito ni crear una CxC comercial ordinaria sólo para representar unas horas de reparto. El crédito comercial existente no es un sustituto automático de este flujo.

Antes de implementar se debe definir cómo proteger/reservar unidades durante `EN_RUTA`, cuándo descontar definitivamente el inventario y confirmar la venta, qué ocurre ante rechazo del cliente, cómo registrar el efectivo recibido y su efecto en Caja, y cómo devolver las unidades disponibles si falla la entrega. No se añade todavía un método de pago ni se inventa comportamiento transaccional. El recorrido actual con venta confirmada y pago/crédito válido no habilita por sí solo cobro contra entrega.

**Pedido = solicitud/preparación · Venta = operación comercial · Entrega = cumplimiento físico · Cobro contra entrega = condición operativa pendiente de diseño.**

Estas decisiones son documentales: no modifican `OrderRules`, `OrderService`, `SalesService`, SQL ni WPF.

## Validación y coordinación

Los contratos se documentan en DEV-15 para el chat de UI. No se cambia WPF en esta rama. Este MVP se limita al ciclo operativo con existencias iniciales/ajustes trazables: compras, recepciones, importaciones y su costeo definitivo, dashboard/reportes avanzados, devoluciones, anulación de ventas confirmadas, promociones COMBO/OTRO, varios impuestos y conectores externos requieren incrementos posteriores. No equivale a completar todo el roadmap de DM-01 ni a habilitar producción.

### Comprobaciones

- Compilación Release de la solución en Linux con SDK 8.0.426 y `-p:EnableWindowsTargeting=true`: sin advertencias ni errores. WPF se compila pero no se ejecuta en Linux.
- 116 casos xUnit correctos: incluye cálculo comercial, impuestos incluidos/añadidos, redondeo por lote, FEFO, umbrales de autorización, estados terminales, validaciones, permisos, cambio de sesión y generación real de PDF con QuestPDF.
- Migraciones y consultas revisadas con parser T-SQL 160 de Microsoft; esto comprueba sintaxis, no transacciones en ejecución.
- Suite `MvpIntegration` añadida a CI Windows/LocalDB y `scripts/verify.ps1 -SqlIntegration`. Crea sólo `GoDental_Tests_<GUID>`: reenvíos concurrentes, última unidad, token obsoleto, listas/promociones, caja/cambio, cotización/conversión, crédito/abonos, autorización, lotes/impuestos, snapshots de entrega, transiciones, permisos revocados, rollback y reaplicación de migraciones.
- [CI Windows #44](https://github.com/Gust4av033/GoDental/actions/runs/39328112837), código `f51f75c`: Release sin advertencias/errores, 116 casos xUnit, **329 comprobaciones SQL** (40 inventario, 49 seguridad/catálogo, 67 conteos, 38 proveedores, 33 clientes y **102 MVP**) y **101 comprobaciones WPF** correctas, sin errores de binding. SQL usa instancias LocalDB aisladas del runner, no la base del usuario. Se verificaron instalación/reaplicación de 012–013, 76 tablas/8 vistas/17 procedimientos y concurrencia con conexiones independientes.
- La descarga del contenedor SQL Server fue denegada en Linux; la ejecución SQL se completó mediante CI Windows. No se ejecutaron migraciones ni se crearon usuarios en el equipo del usuario. El arnés de capturas corrige el bloqueo por confirmación de descarte según DEV-18; el mismo ajuste está integrado en ambas ramas.

En Windows, desde la raíz del repositorio:

```powershell
dotnet run --project tests/GoDental.DatabaseTests -c Release -- --run-mvp --server '.\SQLEXPRESS' .
```

Instalar/actualizar una base marcada de desarrollo se hace por separado con `--install-development --server '.\SQLEXPRESS' .`; la suite nunca utiliza esa base. La lógica ya está sincronizada hacia `feature/ui`. Tras conectar las pantallas nuevas se valida el recorrido en Windows y en las dos estaciones antes de considerar terminado el MVP funcional; el smoke actual verifica las pantallas existentes.
