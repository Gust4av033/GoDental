/* ============================================================================
   GoDental - 008_ViewsProcedures.sql
   Versión: 0.1
   Motor: Microsoft SQL Server

   Requisitos previos:
       001_Core.sql
       002_Inventario.sql
       003_Compras.sql
       004_ClientesVentas.sql
       005_Fiscal_Base.sql
       006_Comercial_Base.sql
       007_Seed.sql

   Objetivo:
       - Crear vistas operativas/reporte de uso frecuente.
       - Crear procedimientos almacenados para operaciones críticas y concurrentes.
       - Mantener explícita la lógica transaccional.
       - NO crear triggers.

   Nota de arquitectura:
       La UI WPF y los servicios C# continuarán siendo responsables de validación de
       experiencia de usuario y orquestación. Los procedimientos de este archivo
       protegen operaciones donde la atomicidad/concurrencia es crítica.
   ============================================================================ */

USE [GoDental];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================================
   0. VALIDACIÓN DE PRERREQUISITOS
   ============================================================================ */
IF OBJECT_ID(N'inv.Productos', N'U') IS NULL
   OR OBJECT_ID(N'cmp.RecepcionesCompra', N'U') IS NULL
   OR OBJECT_ID(N'ven.Ventas', N'U') IS NULL
   OR OBJECT_ID(N'fis.DocumentosFiscales', N'U') IS NULL
   OR OBJECT_ID(N'com.Prospectos', N'U') IS NULL
BEGIN
    THROW 51000, N'Faltan objetos base. Ejecute 001 a 007 antes de 008_ViewsProcedures.sql.', 1;
END;
GO

/* ============================================================================
   1. VISTAS
   ============================================================================ */

/* --------------------------------------------------------------------------
   1.1 Inventario actual por almacén
   -------------------------------------------------------------------------- */
CREATE OR ALTER VIEW inv.vwInventarioActual
AS
SELECT
    a.IdAlmacen,
    a.Codigo AS CodigoAlmacen,
    a.Nombre AS Almacen,
    p.IdProducto,
    p.SKU,
    p.Nombre AS Producto,
    c.Nombre AS Categoria,
    m.Nombre AS Marca,
    u.Codigo AS UnidadMedida,
    p.PrecioVentaReferencia,
    ep.StockActual,
    ep.StockReservado,
    CAST(ep.StockActual - ep.StockReservado AS DECIMAL(18,4)) AS StockDisponible,
    ep.CostoPromedio,
    CAST(ep.StockActual * ep.CostoPromedio AS DECIMAL(18,2)) AS ValorInventario,
    p.StockMinimo,
    p.StockMaximo,
    CASE
        WHEN ep.StockActual - ep.StockReservado <= 0 THEN N'SIN_STOCK'
        WHEN ep.StockActual - ep.StockReservado <= p.StockMinimo THEN N'STOCK_BAJO'
        ELSE N'NORMAL'
    END AS EstadoStock,
    ep.FechaUltimoMovimiento,
    p.ControlaLote,
    p.ControlaVencimiento,
    p.Activo
FROM inv.ExistenciasProducto ep
INNER JOIN inv.Almacenes a ON a.IdAlmacen = ep.IdAlmacen
INNER JOIN inv.Productos p ON p.IdProducto = ep.IdProducto
INNER JOIN cat.CategoriasProducto c ON c.IdCategoria = p.IdCategoria
LEFT JOIN cat.Marcas m ON m.IdMarca = p.IdMarca
INNER JOIN cat.UnidadesMedida u ON u.IdUnidadMedida = p.IdUnidadMedida;
GO

/* --------------------------------------------------------------------------
   1.2 Lotes con vencimiento
   -------------------------------------------------------------------------- */
CREATE OR ALTER VIEW inv.vwLotesVencimiento
AS
SELECT
    el.IdAlmacen,
    a.Codigo AS CodigoAlmacen,
    a.Nombre AS Almacen,
    lp.IdLote,
    lp.IdProducto,
    p.SKU,
    p.Nombre AS Producto,
    lp.NumeroLote,
    lp.FechaFabricacion,
    lp.FechaVencimiento,
    el.StockActual,
    lp.CostoUnitarioIngreso,
    DATEDIFF(DAY, CONVERT(date, SYSDATETIMEOFFSET()), lp.FechaVencimiento) AS DiasParaVencer,
    CASE
        WHEN lp.FechaVencimiento IS NULL THEN N'SIN_FECHA'
        WHEN lp.FechaVencimiento < CONVERT(date, SYSDATETIMEOFFSET()) THEN N'VENCIDO'
        WHEN DATEDIFF(DAY, CONVERT(date, SYSDATETIMEOFFSET()), lp.FechaVencimiento) <= 30 THEN N'URGENTE'
        WHEN DATEDIFF(DAY, CONVERT(date, SYSDATETIMEOFFSET()), lp.FechaVencimiento) <= ISNULL(p.DiasAlertaVencimiento, 90) THEN N'PROXIMO'
        ELSE N'NORMAL'
    END AS EstadoVencimiento
FROM inv.ExistenciasLote el
INNER JOIN inv.Almacenes a ON a.IdAlmacen = el.IdAlmacen
INNER JOIN inv.LotesProducto lp ON lp.IdLote = el.IdLote
INNER JOIN inv.Productos p ON p.IdProducto = lp.IdProducto
WHERE el.StockActual > 0
  AND lp.Activo = 1;
GO

/* --------------------------------------------------------------------------
   1.3 Seguimiento de órdenes de compra
   -------------------------------------------------------------------------- */
CREATE OR ALTER VIEW cmp.vwOrdenesCompraSeguimiento
AS
SELECT
    oc.IdOrdenCompra,
    oc.NumeroOrden,
    oc.FechaOrden,
    oc.Estado,
    pr.IdProveedor,
    pr.Codigo AS CodigoProveedor,
    pr.Nombre AS Proveedor,
    mo.CodigoISO AS Moneda,
    oc.Subtotal,
    oc.Total,
    SUM(od.Cantidad) AS CantidadOrdenada,
    SUM(od.CantidadRecibida) AS CantidadRecibida,
    SUM(od.Cantidad - od.CantidadRecibida) AS CantidadPendiente,
    CASE
        WHEN SUM(od.Cantidad) = 0 THEN CAST(0 AS DECIMAL(9,2))
        ELSE CAST((SUM(od.CantidadRecibida) / NULLIF(SUM(od.Cantidad), 0)) * 100.0 AS DECIMAL(9,2))
    END AS PorcentajeRecibido,
    oc.CreadoPor,
    u.NombreCompleto AS CreadoPorNombre
FROM cmp.OrdenesCompra oc
INNER JOIN cmp.Proveedores pr ON pr.IdProveedor = oc.IdProveedor
INNER JOIN cat.Monedas mo ON mo.IdMoneda = oc.IdMoneda
INNER JOIN seg.Usuarios u ON u.IdUsuario = oc.CreadoPor
LEFT JOIN cmp.OrdenCompraDetalle od ON od.IdOrdenCompra = oc.IdOrdenCompra
GROUP BY
    oc.IdOrdenCompra, oc.NumeroOrden, oc.FechaOrden, oc.Estado,
    pr.IdProveedor, pr.Codigo, pr.Nombre, mo.CodigoISO,
    oc.Subtotal, oc.Total, oc.CreadoPor, u.NombreCompleto;
GO

/* --------------------------------------------------------------------------
   1.4 Seguimiento de importaciones
   -------------------------------------------------------------------------- */
CREATE OR ALTER VIEW cmp.vwImportacionesSeguimiento
AS
SELECT
    i.IdImportacion,
    i.IdOrdenCompra,
    oc.NumeroOrden,
    pr.Nombre AS Proveedor,
    i.Courier,
    i.NumeroGuia,
    i.Estado,
    i.FechaPedido,
    i.FechaDespacho,
    i.FechaEstimada,
    i.FechaRecepcion,
    CASE
        WHEN i.FechaRecepcion IS NOT NULL THEN 0
        WHEN i.FechaEstimada IS NULL THEN NULL
        ELSE DATEDIFF(DAY, CONVERT(date, SYSDATETIMEOFFSET()), i.FechaEstimada)
    END AS DiasHastaEstimado,
    ISNULL(g.TotalGastosUSD, 0) AS GastosImportacionUSD,
    ev.FechaEvento AS FechaUltimoEvento,
    ev.TipoEvento AS UltimoEvento,
    ev.Descripcion AS UltimoEventoDescripcion
FROM cmp.Importaciones i
INNER JOIN cmp.OrdenesCompra oc ON oc.IdOrdenCompra = i.IdOrdenCompra
INNER JOIN cmp.Proveedores pr ON pr.IdProveedor = oc.IdProveedor
OUTER APPLY
(
    SELECT SUM(gi.MontoUSD) AS TotalGastosUSD
    FROM cmp.GastosImportacion gi
    WHERE gi.IdImportacion = i.IdImportacion
) g
OUTER APPLY
(
    SELECT TOP (1)
        e.FechaEvento,
        e.TipoEvento,
        e.Descripcion
    FROM cmp.EventosImportacion e
    WHERE e.IdImportacion = i.IdImportacion
    ORDER BY e.FechaEvento DESC, e.IdEvento DESC
) ev;
GO

/* --------------------------------------------------------------------------
   1.5 Resumen de ventas
   -------------------------------------------------------------------------- */
CREATE OR ALTER VIEW ven.vwVentasResumen
AS
SELECT
    v.IdVenta,
    v.NumeroVenta,
    v.Fecha,
    v.Estado,
    v.IdCliente,
    c.Codigo AS CodigoCliente,
    c.NombreRazonSocial AS Cliente,
    v.IdVendedor,
    u.NombreCompleto AS Vendedor,
    v.IdCaja,
    ca.Codigo AS CodigoCaja,
    v.Subtotal,
    v.DescuentoTotal,
    v.ImpuestoTotal,
    v.Total,
    v.CostoTotal,
    CAST((v.Subtotal - v.DescuentoTotal) - v.CostoTotal AS DECIMAL(18,2)) AS UtilidadEstimada,
    v.SaldoPendiente,
    ISNULL(p.TotalPagadoNoCredito, 0) AS TotalPagadoNoCredito,
    ISNULL(p.TotalCredito, 0) AS TotalCredito
FROM ven.Ventas v
LEFT JOIN cli.Clientes c ON c.IdCliente = v.IdCliente
INNER JOIN seg.Usuarios u ON u.IdUsuario = v.IdVendedor
INNER JOIN ven.Cajas ca ON ca.IdCaja = v.IdCaja
OUTER APPLY
(
    SELECT
        SUM(CASE WHEN mp.Codigo <> N'CREDITO' AND vp.Estado <> N'ANULADO' THEN vp.Monto ELSE 0 END) AS TotalPagadoNoCredito,
        SUM(CASE WHEN mp.Codigo = N'CREDITO' AND vp.Estado <> N'ANULADO' THEN vp.Monto ELSE 0 END) AS TotalCredito
    FROM ven.VentaPagos vp
    INNER JOIN cat.MetodosPago mp ON mp.IdMetodoPago = vp.IdMetodoPago
    WHERE vp.IdVenta = v.IdVenta
) p;
GO

/* --------------------------------------------------------------------------
   1.6 Cuentas por cobrar
   -------------------------------------------------------------------------- */
CREATE OR ALTER VIEW ven.vwCuentasPorCobrarEstado
AS
SELECT
    cx.IdCuenta,
    cx.IdVenta,
    v.NumeroVenta,
    cx.IdCliente,
    c.Codigo AS CodigoCliente,
    c.NombreRazonSocial AS Cliente,
    cx.FechaEmision,
    cx.FechaVencimiento,
    cx.MontoOriginal,
    cx.Saldo,
    cx.Estado,
    CASE
        WHEN cx.Saldo <= 0 THEN 0
        WHEN cx.FechaVencimiento >= CONVERT(date, SYSDATETIMEOFFSET()) THEN 0
        ELSE DATEDIFF(DAY, cx.FechaVencimiento, CONVERT(date, SYSDATETIMEOFFSET()))
    END AS DiasMora,
    cx.UltimoAviso,
    cx.ProximoAviso,
    c.LimiteCredito,
    c.BloqueadoCredito,
    c.MotivoBloqueoCredito
FROM ven.CuentasPorCobrar cx
INNER JOIN ven.Ventas v ON v.IdVenta = cx.IdVenta
INNER JOIN cli.Clientes c ON c.IdCliente = cx.IdCliente;
GO

/* --------------------------------------------------------------------------
   1.7 Estado fiscal / DTE por venta
   -------------------------------------------------------------------------- */
CREATE OR ALTER VIEW fis.vwEstadoFiscalVentas
AS
SELECT
    v.IdVenta,
    v.NumeroVenta,
    v.Fecha AS FechaVenta,
    v.Total,
    df.IdDocumentoFiscal,
    df.TipoDocumento,
    df.NumeroInterno,
    df.Estado AS EstadoDocumentoFiscal,
    dte.IdDte,
    dte.TipoDte,
    dte.CodigoGeneracion,
    dte.NumeroControl,
    dte.SelloRecepcion,
    dte.Estado AS EstadoDte
FROM ven.Ventas v
LEFT JOIN fis.DocumentosFiscales df ON df.IdVenta = v.IdVenta
LEFT JOIN fis.DocumentosDte dte ON dte.IdDocumentoFiscal = df.IdDocumentoFiscal;
GO

/* --------------------------------------------------------------------------
   1.8 Prospectos pendientes de trabajo comercial
   -------------------------------------------------------------------------- */
CREATE OR ALTER VIEW com.vwProspectosPendientes
AS
SELECT
    p.IdProspecto,
    p.Nombre,
    p.TipoProspecto,
    p.TelefonoPublico,
    p.WhatsAppPublico,
    p.EmailPublico,
    p.Instagram,
    p.Facebook,
    p.Departamento,
    p.Municipio,
    p.Estado,
    p.NoContactar,
    p.IdClienteConvertido,
    CASE
        WHEN p.NoContactar = 1 THEN N'NO_CONTACTAR'
        WHEN p.IdClienteConvertido IS NOT NULL THEN N'CONVERTIDO'
        WHEN EXISTS
        (
            SELECT 1
            FROM com.ListaNoContactar lnc
            WHERE lnc.Activo = 1
              AND
              (
                  (lnc.Canal = N'WHATSAPP' AND lnc.Identificador = p.WhatsAppPublico AND p.WhatsAppPublico IS NOT NULL)
                  OR (lnc.Canal = N'WHATSAPP' AND lnc.Identificador = p.TelefonoPublico AND p.TelefonoPublico IS NOT NULL)
                  OR (lnc.Canal = N'EMAIL' AND lnc.Identificador = p.EmailPublico AND p.EmailPublico IS NOT NULL)
                  OR (lnc.Canal = N'INSTAGRAM' AND lnc.Identificador = p.Instagram AND p.Instagram IS NOT NULL)
                  OR (lnc.Canal = N'FACEBOOK' AND lnc.Identificador = p.Facebook AND p.Facebook IS NOT NULL)
              )
        ) THEN N'SUPRIMIDO'
        ELSE N'DISPONIBLE'
    END AS EstadoContacto
FROM com.Prospectos p
WHERE p.Estado IN (N'NUEVO', N'REVISADO', N'CONTACTADO', N'INTERESADO');
GO

/* ============================================================================
   2. PROCEDIMIENTOS DE SEGURIDAD / SISTEMA
   ============================================================================ */

/* --------------------------------------------------------------------------
   2.1 Obtener correlativo de forma concurrente y segura
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE sis.sp_SiguienteCorrelativo
    @TipoDocumento NVARCHAR(40),
    @NumeroDocumento NVARCHAR(60) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @InicioTransaccion BIT = 0;
    DECLARE @Prefijo NVARCHAR(20);
    DECLARE @UltimoNumero BIGINT;
    DECLARE @Longitud TINYINT;
    DECLARE @ReinicioAnual BIT;
    DECLARE @AnioActual SMALLINT;
    DECLARE @AnioHoy SMALLINT = YEAR(GETDATE());

    BEGIN TRY
        IF @@TRANCOUNT = 0
        BEGIN
            BEGIN TRANSACTION;
            SET @InicioTransaccion = 1;
        END;

        SELECT
            @Prefijo = Prefijo,
            @UltimoNumero = UltimoNumero,
            @Longitud = Longitud,
            @ReinicioAnual = ReinicioAnual,
            @AnioActual = AnioActual
        FROM sis.SecuenciasDocumento WITH (UPDLOCK, HOLDLOCK)
        WHERE TipoDocumento = @TipoDocumento;

        IF @Longitud IS NULL
            THROW 51001, N'No existe la secuencia documental solicitada.', 1;

        IF @ReinicioAnual = 1 AND ISNULL(@AnioActual, 0) <> @AnioHoy
        BEGIN
            SET @UltimoNumero = 0;
            SET @AnioActual = @AnioHoy;
        END;

        SET @UltimoNumero = @UltimoNumero + 1;

        UPDATE sis.SecuenciasDocumento
        SET UltimoNumero = @UltimoNumero,
            AnioActual = CASE WHEN @ReinicioAnual = 1 THEN @AnioHoy ELSE AnioActual END
        WHERE TipoDocumento = @TipoDocumento;

        SET @NumeroDocumento =
            ISNULL(@Prefijo, N'')
            + CASE WHEN @ReinicioAnual = 1 THEN CONVERT(NVARCHAR(4), @AnioHoy) + N'-' ELSE N'' END
            + RIGHT(REPLICATE(N'0', @Longitud) + CONVERT(NVARCHAR(20), @UltimoNumero), @Longitud);

        IF @InicioTransaccion = 1
            COMMIT TRANSACTION;

    END TRY
    BEGIN CATCH
        IF @InicioTransaccion = 1 AND XACT_STATE() <> 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* --------------------------------------------------------------------------
   2.2 Datos base para autenticación
   La verificación criptográfica del PasswordHash se hace en C#.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE seg.sp_ObtenerUsuarioLogin
    @Usuario NVARCHAR(60)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        u.IdUsuario,
        u.Usuario,
        u.NombreCompleto,
        u.PasswordHash,
        u.Email,
        u.DebeCambiarClave,
        u.IntentosFallidos,
        u.BloqueadoHasta,
        u.UltimoAcceso,
        u.Activo,
        p.Tema,
        p.SidebarCompacta,
        p.PaginaInicio,
        p.TamanoDocumentoPreferido
    FROM seg.Usuarios u
    LEFT JOIN sis.PreferenciasUsuario p ON p.IdUsuario = u.IdUsuario
    WHERE u.Usuario = @Usuario;
END;
GO

/* --------------------------------------------------------------------------
   2.3 Permisos efectivos del usuario
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE seg.sp_ObtenerPermisosUsuario
    @IdUsuario INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        p.IdPermiso,
        p.Codigo,
        p.Nombre,
        p.Modulo,
        p.Descripcion
    FROM seg.UsuarioRoles ur
    INNER JOIN seg.Roles r ON r.IdRol = ur.IdRol AND r.Activo = 1
    INNER JOIN seg.RolPermisos rp ON rp.IdRol = r.IdRol AND rp.Concedido = 1
    INNER JOIN seg.Permisos p ON p.IdPermiso = rp.IdPermiso AND p.Activo = 1
    WHERE ur.IdUsuario = @IdUsuario
    ORDER BY p.Modulo, p.Codigo;
END;
GO

/* ============================================================================
   3. PROCEDIMIENTOS DE INVENTARIO
   ============================================================================ */

/* --------------------------------------------------------------------------
   3.1 Buscar producto por SKU, nombre o código de barras
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE inv.sp_BuscarProducto
    @Busqueda NVARCHAR(200),
    @IdAlmacen INT = NULL,
    @SoloActivos BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF @IdAlmacen IS NULL
    BEGIN
        SELECT TOP (1) @IdAlmacen = IdAlmacen
        FROM inv.Almacenes
        WHERE Activo = 1
        ORDER BY EsPrincipal DESC, IdAlmacen;
    END;

    SELECT TOP (100)
        p.IdProducto,
        p.SKU,
        p.Nombre,
        p.Descripcion,
        c.Nombre AS Categoria,
        m.Nombre AS Marca,
        u.Codigo AS UnidadMedida,
        p.PrecioVentaReferencia,
        cb.Codigo AS CodigoBarraPrincipal,
        ISNULL(ep.StockActual, 0) AS StockActual,
        ISNULL(ep.StockReservado, 0) AS StockReservado,
        ISNULL(ep.StockActual - ep.StockReservado, 0) AS StockDisponible,
        ISNULL(ep.CostoPromedio, 0) AS CostoPromedio,
        p.ControlaLote,
        p.ControlaVencimiento,
        p.Activo
    FROM inv.Productos p
    INNER JOIN cat.CategoriasProducto c ON c.IdCategoria = p.IdCategoria
    LEFT JOIN cat.Marcas m ON m.IdMarca = p.IdMarca
    INNER JOIN cat.UnidadesMedida u ON u.IdUnidadMedida = p.IdUnidadMedida
    LEFT JOIN inv.ExistenciasProducto ep
        ON ep.IdProducto = p.IdProducto
       AND ep.IdAlmacen = @IdAlmacen
    OUTER APPLY
    (
        SELECT TOP (1) pcb.Codigo
        FROM inv.ProductoCodigosBarra pcb
        WHERE pcb.IdProducto = p.IdProducto
          AND pcb.Activo = 1
        ORDER BY pcb.EsPrincipal DESC, pcb.IdCodigoBarra
    ) cb
    WHERE (@SoloActivos = 0 OR p.Activo = 1)
      AND
      (
          NULLIF(LTRIM(RTRIM(@Busqueda)), N'') IS NULL
          OR p.SKU LIKE N'%' + @Busqueda + N'%'
          OR p.Nombre LIKE N'%' + @Busqueda + N'%'
          OR EXISTS
          (
              SELECT 1
              FROM inv.ProductoCodigosBarra x
              WHERE x.IdProducto = p.IdProducto
                AND x.Activo = 1
                AND x.Codigo LIKE N'%' + @Busqueda + N'%'
          )
      )
    ORDER BY
        CASE WHEN p.SKU = @Busqueda THEN 0 ELSE 1 END,
        p.Nombre;
END;
GO

/* --------------------------------------------------------------------------
   3.2 Obtener stock de un producto, incluyendo lotes
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE inv.sp_ObtenerStockProducto
    @IdProducto INT,
    @IdAlmacen INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ep.IdAlmacen,
        a.Codigo AS CodigoAlmacen,
        a.Nombre AS Almacen,
        ep.IdProducto,
        p.SKU,
        p.Nombre AS Producto,
        ep.StockActual,
        ep.StockReservado,
        ep.StockActual - ep.StockReservado AS StockDisponible,
        ep.CostoPromedio,
        ep.FechaUltimoMovimiento
    FROM inv.ExistenciasProducto ep
    INNER JOIN inv.Almacenes a ON a.IdAlmacen = ep.IdAlmacen
    INNER JOIN inv.Productos p ON p.IdProducto = ep.IdProducto
    WHERE ep.IdProducto = @IdProducto
      AND (@IdAlmacen IS NULL OR ep.IdAlmacen = @IdAlmacen)
    ORDER BY a.EsPrincipal DESC, a.Nombre;

    SELECT
        el.IdAlmacen,
        el.IdLote,
        lp.NumeroLote,
        lp.FechaFabricacion,
        lp.FechaVencimiento,
        el.StockActual,
        lp.CostoUnitarioIngreso
    FROM inv.ExistenciasLote el
    INNER JOIN inv.LotesProducto lp ON lp.IdLote = el.IdLote
    WHERE lp.IdProducto = @IdProducto
      AND el.StockActual > 0
      AND (@IdAlmacen IS NULL OR el.IdAlmacen = @IdAlmacen)
    ORDER BY
        CASE WHEN lp.FechaVencimiento IS NULL THEN 1 ELSE 0 END,
        lp.FechaVencimiento,
        lp.NumeroLote;
END;
GO

/* ============================================================================
   4. PROCEDIMIENTOS DE CAJA / VENTAS / CRÉDITO
   ============================================================================ */

/* --------------------------------------------------------------------------
   4.1 Abrir turno de caja
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE ven.sp_AbrirTurnoCaja
    @IdCaja INT,
    @IdUsuario INT,
    @MontoInicial DECIMAL(18,2),
    @Observaciones NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @MontoInicial < 0
        THROW 51010, N'El monto inicial no puede ser negativo.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM ven.Cajas WITH (UPDLOCK, HOLDLOCK) WHERE IdCaja = @IdCaja AND Activo = 1)
            THROW 51011, N'La caja no existe o está inactiva.', 1;

        IF NOT EXISTS (SELECT 1 FROM seg.Usuarios WHERE IdUsuario = @IdUsuario AND Activo = 1)
            THROW 51012, N'El usuario no existe o está inactivo.', 1;

        IF EXISTS (SELECT 1 FROM ven.TurnosCaja WITH (UPDLOCK, HOLDLOCK) WHERE IdCaja = @IdCaja AND Estado = N'ABIERTO')
            THROW 51013, N'La caja ya tiene un turno abierto.', 1;

        INSERT INTO ven.TurnosCaja
        (
            IdCaja, AbiertoPor, MontoInicial, Estado, Observaciones
        )
        VALUES
        (
            @IdCaja, @IdUsuario, @MontoInicial, N'ABIERTO', @Observaciones
        );

        DECLARE @IdTurno BIGINT = SCOPE_IDENTITY();

        INSERT INTO sis.Bitacora
        (
            IdUsuario, Modulo, Accion, Entidad, EntidadId, Descripcion
        )
        VALUES
        (
            @IdUsuario, N'Caja', N'ABRIR_TURNO', N'TurnosCaja', @IdTurno,
            CONCAT(N'Apertura de caja. Monto inicial: $', CONVERT(NVARCHAR(40), @MontoInicial))
        );

        COMMIT TRANSACTION;

        SELECT @IdTurno AS IdTurno;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* --------------------------------------------------------------------------
   4.2 Cerrar turno de caja
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE ven.sp_CerrarTurnoCaja
    @IdTurno BIGINT,
    @IdUsuario INT,
    @EfectivoContado DECIMAL(18,2),
    @Observaciones NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @EfectivoContado < 0
        THROW 51020, N'El efectivo contado no puede ser negativo.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @MontoInicial DECIMAL(18,2);
        DECLARE @Estado NVARCHAR(20);
        DECLARE @EfectivoEsperado DECIMAL(18,2);

        SELECT
            @MontoInicial = MontoInicial,
            @Estado = Estado
        FROM ven.TurnosCaja WITH (UPDLOCK, HOLDLOCK)
        WHERE IdTurno = @IdTurno;

        IF @Estado IS NULL
            THROW 51021, N'El turno de caja no existe.', 1;

        IF @Estado <> N'ABIERTO'
            THROW 51022, N'El turno ya no está abierto.', 1;

        SELECT @EfectivoEsperado =
            @MontoInicial
            + ISNULL(SUM
            (
                CASE
                    WHEN Tipo IN (N'VENTA_EFECTIVO', N'INGRESO', N'AJUSTE') THEN Monto
                    WHEN Tipo IN (N'RETIRO', N'DEVOLUCION_EFECTIVO') THEN -Monto
                    ELSE 0
                END
            ), 0)
        FROM ven.MovimientosCaja WITH (HOLDLOCK)
        WHERE IdTurno = @IdTurno;

        UPDATE ven.TurnosCaja
        SET FechaCierre = SYSDATETIMEOFFSET(),
            CerradoPor = @IdUsuario,
            EfectivoEsperado = @EfectivoEsperado,
            EfectivoContado = @EfectivoContado,
            Diferencia = @EfectivoContado - @EfectivoEsperado,
            Estado = N'CERRADO',
            Observaciones = COALESCE(@Observaciones, Observaciones)
        WHERE IdTurno = @IdTurno;

        INSERT INTO sis.Bitacora
        (
            IdUsuario, Modulo, Accion, Entidad, EntidadId, Descripcion
        )
        VALUES
        (
            @IdUsuario, N'Caja', N'CERRAR_TURNO', N'TurnosCaja', @IdTurno,
            CONCAT(
                N'Cierre de caja. Esperado: $', CONVERT(NVARCHAR(40), @EfectivoEsperado),
                N'; contado: $', CONVERT(NVARCHAR(40), @EfectivoContado),
                N'; diferencia: $', CONVERT(NVARCHAR(40), @EfectivoContado - @EfectivoEsperado)
            )
        );

        COMMIT TRANSACTION;

        SELECT
            @IdTurno AS IdTurno,
            @EfectivoEsperado AS EfectivoEsperado,
            @EfectivoContado AS EfectivoContado,
            @EfectivoContado - @EfectivoEsperado AS Diferencia;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* --------------------------------------------------------------------------
   4.3 Confirmar venta existente en BORRADOR

   Flujo esperado desde C#:
       1) Crear cabecera BORRADOR + detalles + pagos.
       2) Llamar este procedimiento.
       3) El procedimiento valida descuentos, pago, crédito y stock.
       4) Descuenta existencias, registra Kardex/Caja/CxC y confirma la venta.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE ven.sp_ConfirmarVenta
    @IdVenta BIGINT,
    @IdUsuario INT,
    @MotivoDescuento NVARCHAR(1000) = NULL,
    @IdAutorizacionDescuento BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Estado NVARCHAR(20);
        DECLARE @IdCliente BIGINT;
        DECLARE @IdCaja INT;
        DECLARE @IdTurno BIGINT;
        DECLARE @FechaVenta DATETIMEOFFSET(0);
        DECLARE @Total DECIMAL(18,2);
        DECLARE @MaxDescuento DECIMAL(9,4);
        DECLARE @UmbralJustificacion DECIMAL(9,4) =
            ISNULL((SELECT TRY_CONVERT(DECIMAL(9,4), Valor) FROM sis.Configuracion WHERE Clave = N'Descuento.RequiereJustificacionSobre'), 7.00);
        DECLARE @UmbralAutorizacion DECIMAL(9,4) =
            ISNULL((SELECT TRY_CONVERT(DECIMAL(9,4), Valor) FROM sis.Configuracion WHERE Clave = N'Descuento.RequiereAutorizacionSobre'), 35.00);

        SELECT
            @Estado = Estado,
            @IdCliente = IdCliente,
            @IdCaja = IdCaja,
            @IdTurno = IdTurno,
            @FechaVenta = Fecha
        FROM ven.Ventas WITH (UPDLOCK, HOLDLOCK)
        WHERE IdVenta = @IdVenta;

        IF @Estado IS NULL
            THROW 51030, N'La venta no existe.', 1;

        IF @Estado <> N'BORRADOR'
            THROW 51031, N'Sólo se puede confirmar una venta en estado BORRADOR.', 1;

        IF NOT EXISTS (SELECT 1 FROM ven.VentaDetalle WHERE IdVenta = @IdVenta)
            THROW 51032, N'La venta no contiene productos.', 1;

        SELECT @MaxDescuento = ISNULL(MAX(DescuentoPorcentaje), 0)
        FROM ven.VentaDetalle
        WHERE IdVenta = @IdVenta;

        IF @MaxDescuento > @UmbralJustificacion
           AND NULLIF(LTRIM(RTRIM(@MotivoDescuento)), N'') IS NULL
            THROW 51033, N'El descuento supera el límite ordinario y requiere justificación.', 1;

        IF @MaxDescuento > @UmbralAutorizacion
        BEGIN
            IF @IdAutorizacionDescuento IS NULL
                THROW 51034, N'El descuento requiere autorización administrativa.', 1;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sis.AutorizacionesOperacion ao WITH (UPDLOCK, HOLDLOCK)
                WHERE ao.IdAutorizacion = @IdAutorizacionDescuento
                  AND ao.TipoOperacion = N'DESCUENTO_MAYOR_35'
                  AND ao.EntidadTipo = N'VENTA'
                  AND ao.EntidadId = @IdVenta
                  AND ao.Resultado = N'APROBADA'
                  AND ao.FechaAutorizacion IS NOT NULL
                  AND ISNULL(ao.PorcentajeValor, 0) >= @MaxDescuento
            )
                THROW 51035, N'La autorización administrativa no es válida para este descuento.', 1;
        END;

        SELECT
            @Total = CAST(ISNULL(SUM(TotalLinea), 0) AS DECIMAL(18,2))
        FROM ven.VentaDetalle
        WHERE IdVenta = @IdVenta;

        IF @Total <= 0
            THROW 51036, N'El total de la venta debe ser mayor que cero.', 1;

        DECLARE @TotalPagos DECIMAL(18,2);
        DECLARE @MontoCredito DECIMAL(18,2);
        DECLARE @MontoEfectivo DECIMAL(18,2);

        SELECT
            @TotalPagos = ISNULL(SUM(CASE WHEN vp.Estado <> N'ANULADO' THEN vp.Monto ELSE 0 END), 0),
            @MontoCredito = ISNULL(SUM(CASE WHEN vp.Estado <> N'ANULADO' AND mp.Codigo = N'CREDITO' THEN vp.Monto ELSE 0 END), 0),
            @MontoEfectivo = ISNULL(SUM(CASE WHEN vp.Estado <> N'ANULADO' AND mp.AfectaCajaEfectivo = 1 THEN vp.Monto ELSE 0 END), 0)
        FROM ven.VentaPagos vp
        INNER JOIN cat.MetodosPago mp ON mp.IdMetodoPago = vp.IdMetodoPago
        WHERE vp.IdVenta = @IdVenta;

        IF ABS(@TotalPagos - @Total) > 0.01
            THROW 51037, N'La suma de pagos no coincide con el total de la venta.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM ven.VentaPagos vp
            INNER JOIN cat.MetodosPago mp ON mp.IdMetodoPago = vp.IdMetodoPago
            WHERE vp.IdVenta = @IdVenta
              AND vp.Estado <> N'ANULADO'
              AND mp.RequiereReferencia = 1
              AND NULLIF(LTRIM(RTRIM(vp.Referencia)), N'') IS NULL
        )
            THROW 51038, N'Existe un método de pago que requiere referencia y no la tiene.', 1;

        IF @MontoEfectivo > 0
        BEGIN
            IF @IdTurno IS NULL
                THROW 51039, N'Una venta con efectivo requiere un turno de caja abierto.', 1;

            IF NOT EXISTS
            (
                SELECT 1
                FROM ven.TurnosCaja WITH (UPDLOCK, HOLDLOCK)
                WHERE IdTurno = @IdTurno
                  AND IdCaja = @IdCaja
                  AND Estado = N'ABIERTO'
            )
                THROW 51040, N'El turno de caja no está abierto o no corresponde a la caja de la venta.', 1;
        END;

        IF @MontoCredito > 0
        BEGIN
            DECLARE @PermiteCredito BIT;
            DECLARE @BloqueadoCredito BIT;
            DECLARE @LimiteCredito DECIMAL(18,2);
            DECLARE @DiasCredito SMALLINT;
            DECLARE @SaldoActualCredito DECIMAL(18,2);
            DECLARE @DiasCreditoDefault INT =
                ISNULL((SELECT TRY_CONVERT(INT, Valor) FROM sis.Configuracion WHERE Clave = N'Credito.DiasDefault'), 15);

            IF @IdCliente IS NULL
                THROW 51041, N'No se puede vender a crédito sin cliente identificado.', 1;

            SELECT
                @PermiteCredito = PermiteCredito,
                @BloqueadoCredito = BloqueadoCredito,
                @LimiteCredito = ISNULL(LimiteCredito, 0),
                @DiasCredito = ISNULL(DiasCredito, @DiasCreditoDefault)
            FROM cli.Clientes WITH (UPDLOCK, HOLDLOCK)
            WHERE IdCliente = @IdCliente AND Activo = 1;

            IF @PermiteCredito IS NULL
                THROW 51042, N'El cliente no existe o está inactivo.', 1;

            IF @PermiteCredito = 0
                THROW 51043, N'El cliente no tiene crédito habilitado.', 1;

            IF @BloqueadoCredito = 1
                THROW 51044, N'El crédito del cliente está bloqueado.', 1;

            SELECT @SaldoActualCredito = ISNULL(SUM(Saldo), 0)
            FROM ven.CuentasPorCobrar WITH (UPDLOCK, HOLDLOCK)
            WHERE IdCliente = @IdCliente
              AND Saldo > 0
              AND Estado IN (N'PENDIENTE', N'PARCIAL', N'VENCIDA');

            IF @MontoCredito > (@LimiteCredito - @SaldoActualCredito)
                THROW 51045, N'La venta excede el crédito disponible del cliente.', 1;
        END;

        DECLARE
            @IdDetalle BIGINT,
            @IdProducto INT,
            @IdLote BIGINT,
            @Cantidad DECIMAL(18,4),
            @IdAlmacen INT,
            @ControlaLote BIT,
            @StockAnterior DECIMAL(18,4),
            @StockReservado DECIMAL(18,4),
            @StockPosterior DECIMAL(18,4),
            @CostoPromedio DECIMAL(18,6),
            @StockLote DECIMAL(18,4);

        SELECT @IdAlmacen = IdAlmacen
        FROM ven.Cajas
        WHERE IdCaja = @IdCaja;

        DECLARE curVenta CURSOR LOCAL FAST_FORWARD FOR
            SELECT vd.IdDetalle, vd.IdProducto, vd.IdLote, vd.Cantidad
            FROM ven.VentaDetalle vd
            WHERE vd.IdVenta = @IdVenta
            ORDER BY vd.IdProducto, ISNULL(vd.IdLote, 0), vd.IdDetalle;

        OPEN curVenta;
        FETCH NEXT FROM curVenta INTO @IdDetalle, @IdProducto, @IdLote, @Cantidad;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @ControlaLote = NULL;

            SELECT @ControlaLote = ControlaLote
            FROM inv.Productos
            WHERE IdProducto = @IdProducto AND Activo = 1;

            IF @ControlaLote IS NULL
                THROW 51046, N'La venta contiene un producto inexistente o inactivo.', 1;

            IF @ControlaLote = 1 AND @IdLote IS NULL
                THROW 51047, N'Un producto controlado por lote requiere lote en la venta.', 1;

            SELECT
                @StockAnterior = StockActual,
                @StockReservado = StockReservado,
                @CostoPromedio = CostoPromedio
            FROM inv.ExistenciasProducto WITH (UPDLOCK, HOLDLOCK)
            WHERE IdAlmacen = @IdAlmacen
              AND IdProducto = @IdProducto;

            IF @StockAnterior IS NULL
                THROW 51048, N'No existe saldo de inventario para uno de los productos.', 1;

            IF (@StockAnterior - @StockReservado) < @Cantidad
                THROW 51049, N'Stock disponible insuficiente para confirmar la venta.', 1;

            IF @IdLote IS NOT NULL
            BEGIN
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM inv.LotesProducto
                    WHERE IdLote = @IdLote
                      AND IdProducto = @IdProducto
                      AND Activo = 1
                )
                    THROW 51050, N'El lote no pertenece al producto de la venta.', 1;

                SELECT @StockLote = StockActual
                FROM inv.ExistenciasLote WITH (UPDLOCK, HOLDLOCK)
                WHERE IdAlmacen = @IdAlmacen
                  AND IdLote = @IdLote;

                IF @StockLote IS NULL OR @StockLote < @Cantidad
                    THROW 51051, N'Stock insuficiente en el lote seleccionado.', 1;

                UPDATE inv.ExistenciasLote
                SET StockActual = StockActual - @Cantidad
                WHERE IdAlmacen = @IdAlmacen
                  AND IdLote = @IdLote;
            END;

            SET @StockPosterior = @StockAnterior - @Cantidad;

            UPDATE inv.ExistenciasProducto
            SET StockActual = @StockPosterior,
                FechaUltimoMovimiento = SYSDATETIMEOFFSET()
            WHERE IdAlmacen = @IdAlmacen
              AND IdProducto = @IdProducto;

            UPDATE ven.VentaDetalle
            SET CostoUnitario = @CostoPromedio
            WHERE IdDetalle = @IdDetalle;

            INSERT INTO inv.MovimientosInventario
            (
                IdAlmacen, IdProducto, IdLote, TipoMovimiento, Cantidad,
                CostoUnitario, ExistenciaAnterior, ExistenciaPosterior,
                ReferenciaTipo, ReferenciaId, IdUsuario, Observacion
            )
            VALUES
            (
                @IdAlmacen, @IdProducto, @IdLote, N'VENTA', -@Cantidad,
                @CostoPromedio, @StockAnterior, @StockPosterior,
                N'VENTA', @IdVenta, @IdUsuario, N'Salida por venta confirmada.'
            );

            SET @StockAnterior = NULL;
            SET @StockReservado = NULL;
            SET @StockLote = NULL;

            FETCH NEXT FROM curVenta INTO @IdDetalle, @IdProducto, @IdLote, @Cantidad;
        END;

        CLOSE curVenta;
        DEALLOCATE curVenta;

        DECLARE @Subtotal DECIMAL(18,2);
        DECLARE @DescuentoTotal DECIMAL(18,2);
        DECLARE @ImpuestoTotal DECIMAL(18,2);
        DECLARE @CostoTotal DECIMAL(18,6);

        SELECT
            @Subtotal = CAST(ISNULL(SUM(Cantidad * PrecioUnitario), 0) AS DECIMAL(18,2)),
            @DescuentoTotal = CAST(ISNULL(SUM(DescuentoMonto), 0) AS DECIMAL(18,2)),
            @ImpuestoTotal = CAST(ISNULL(SUM(ImpuestoMonto), 0) AS DECIMAL(18,2)),
            @CostoTotal = CAST(ISNULL(SUM(Cantidad * CostoUnitario), 0) AS DECIMAL(18,6)),
            @Total = CAST(ISNULL(SUM(TotalLinea), 0) AS DECIMAL(18,2))
        FROM ven.VentaDetalle
        WHERE IdVenta = @IdVenta;

        UPDATE ven.VentaPagos
        SET Estado = N'CONFIRMADO'
        WHERE IdVenta = @IdVenta
          AND Estado = N'REGISTRADO';

        IF @MontoEfectivo > 0
        BEGIN
            INSERT INTO ven.MovimientosCaja
            (
                IdTurno, Tipo, Monto, ReferenciaTipo, ReferenciaId, IdUsuario, Motivo
            )
            VALUES
            (
                @IdTurno, N'VENTA_EFECTIVO', @MontoEfectivo,
                N'VENTA', @IdVenta, @IdUsuario, N'Cobro en efectivo de venta.'
            );
        END;

        IF @MontoCredito > 0
        BEGIN
            DECLARE @DiasFinalCredito INT =
                ISNULL((SELECT DiasCredito FROM cli.Clientes WHERE IdCliente = @IdCliente),
                       ISNULL((SELECT TRY_CONVERT(INT, Valor) FROM sis.Configuracion WHERE Clave = N'Credito.DiasDefault'), 15));
            DECLARE @IntervaloAviso INT =
                ISNULL((SELECT IntervaloAvisoDias FROM cli.Clientes WHERE IdCliente = @IdCliente),
                       ISNULL((SELECT TRY_CONVERT(INT, Valor) FROM sis.Configuracion WHERE Clave = N'Credito.IntervaloAvisoDias'), 5));
            DECLARE @FechaEmision DATE = CONVERT(date, @FechaVenta);
            DECLARE @FechaVencimiento DATE = DATEADD(DAY, @DiasFinalCredito, CONVERT(date, @FechaVenta));

            INSERT INTO ven.CuentasPorCobrar
            (
                IdVenta, IdCliente, FechaEmision, FechaVencimiento,
                MontoOriginal, Saldo, Estado, ProximoAviso
            )
            VALUES
            (
                @IdVenta, @IdCliente, @FechaEmision, @FechaVencimiento,
                @MontoCredito, @MontoCredito, N'PENDIENTE',
                DATEADD(DAY, @IntervaloAviso, @FechaEmision)
            );
        END;

        UPDATE ven.Ventas
        SET Estado = N'CONFIRMADA',
            Subtotal = @Subtotal,
            DescuentoTotal = @DescuentoTotal,
            ImpuestoTotal = @ImpuestoTotal,
            Total = @Total,
            CostoTotal = @CostoTotal,
            SaldoPendiente = @MontoCredito
        WHERE IdVenta = @IdVenta;

        INSERT INTO sis.Bitacora
        (
            IdUsuario, Modulo, Accion, Entidad, EntidadId, Descripcion
        )
        VALUES
        (
            @IdUsuario, N'Ventas', N'CONFIRMAR_VENTA', N'Ventas', @IdVenta,
            CONCAT(N'Venta confirmada. Total: $', CONVERT(NVARCHAR(40), @Total))
        );

        IF @MaxDescuento > @UmbralJustificacion
        BEGIN
            INSERT INTO sis.Bitacora
            (
                IdUsuario, Modulo, Accion, Entidad, EntidadId, Descripcion
            )
            VALUES
            (
                @IdUsuario, N'Ventas', N'DESCUENTO_JUSTIFICADO', N'Ventas', @IdVenta,
                CONCAT(N'Descuento máximo: ', CONVERT(NVARCHAR(40), @MaxDescuento), N'%. Motivo: ', @MotivoDescuento)
            );
        END;

        COMMIT TRANSACTION;

        SELECT
            @IdVenta AS IdVenta,
            @Total AS Total,
            @MontoCredito AS SaldoPendiente,
            N'CONFIRMADA' AS Estado;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local', 'curVenta') >= -1
        BEGIN
            IF CURSOR_STATUS('local', 'curVenta') > -1 CLOSE curVenta;
            DEALLOCATE curVenta;
        END;
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* --------------------------------------------------------------------------
   4.4 Registrar abono a cuenta por cobrar
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE ven.sp_RegistrarAbono
    @IdCuenta BIGINT,
    @IdMetodoPago SMALLINT,
    @Monto DECIMAL(18,2),
    @IdUsuario INT,
    @Referencia NVARCHAR(150) = NULL,
    @IdTurno BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Monto <= 0
        THROW 51060, N'El monto del abono debe ser mayor que cero.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Saldo DECIMAL(18,2);
        DECLARE @IdVenta BIGINT;
        DECLARE @IdCliente BIGINT;
        DECLARE @RequiereReferencia BIT;
        DECLARE @AfectaCaja BIT;
        DECLARE @NuevoSaldo DECIMAL(18,2);
        DECLARE @NumeroRecibo NVARCHAR(60);

        SELECT
            @Saldo = Saldo,
            @IdVenta = IdVenta,
            @IdCliente = IdCliente
        FROM ven.CuentasPorCobrar WITH (UPDLOCK, HOLDLOCK)
        WHERE IdCuenta = @IdCuenta
          AND Estado <> N'CASTIGADA';

        IF @Saldo IS NULL
            THROW 51061, N'La cuenta por cobrar no existe o no admite abonos.', 1;

        IF @Monto > @Saldo
            THROW 51062, N'El abono no puede exceder el saldo pendiente.', 1;

        SELECT
            @RequiereReferencia = RequiereReferencia,
            @AfectaCaja = AfectaCajaEfectivo
        FROM cat.MetodosPago
        WHERE IdMetodoPago = @IdMetodoPago AND Activo = 1;

        IF @RequiereReferencia IS NULL
            THROW 51063, N'El método de pago no existe o está inactivo.', 1;

        IF @RequiereReferencia = 1 AND NULLIF(LTRIM(RTRIM(@Referencia)), N'') IS NULL
            THROW 51064, N'El método de pago requiere referencia.', 1;

        IF @AfectaCaja = 1
        BEGIN
            IF @IdTurno IS NULL
                THROW 51065, N'Un abono en efectivo requiere turno de caja.', 1;

            IF NOT EXISTS
            (
                SELECT 1
                FROM ven.TurnosCaja WITH (UPDLOCK, HOLDLOCK)
                WHERE IdTurno = @IdTurno AND Estado = N'ABIERTO'
            )
                THROW 51066, N'El turno de caja no está abierto.', 1;
        END;

        EXEC sis.sp_SiguienteCorrelativo
            @TipoDocumento = N'RECIBO',
            @NumeroDocumento = @NumeroRecibo OUTPUT;

        INSERT INTO ven.AbonosCliente
        (
            IdCuenta, IdMetodoPago, Monto, Referencia,
            RegistradoPor, NumeroRecibo, Anulado
        )
        VALUES
        (
            @IdCuenta, @IdMetodoPago, @Monto, @Referencia,
            @IdUsuario, @NumeroRecibo, 0
        );

        DECLARE @IdAbono BIGINT = SCOPE_IDENTITY();
        SET @NuevoSaldo = @Saldo - @Monto;

        DECLARE @IntervaloAviso INT =
            ISNULL((SELECT IntervaloAvisoDias FROM cli.Clientes WHERE IdCliente = @IdCliente),
                   ISNULL((SELECT TRY_CONVERT(INT, Valor) FROM sis.Configuracion WHERE Clave = N'Credito.IntervaloAvisoDias'), 5));

        UPDATE ven.CuentasPorCobrar
        SET Saldo = @NuevoSaldo,
            Estado = CASE WHEN @NuevoSaldo <= 0 THEN N'PAGADA' ELSE N'PARCIAL' END,
            ProximoAviso = CASE WHEN @NuevoSaldo <= 0 THEN NULL ELSE DATEADD(DAY, @IntervaloAviso, CONVERT(date, SYSDATETIMEOFFSET())) END
        WHERE IdCuenta = @IdCuenta;

        UPDATE ven.Ventas
        SET SaldoPendiente = @NuevoSaldo
        WHERE IdVenta = @IdVenta;

        IF @AfectaCaja = 1
        BEGIN
            INSERT INTO ven.MovimientosCaja
            (
                IdTurno, Tipo, Monto, ReferenciaTipo, ReferenciaId, IdUsuario, Motivo
            )
            VALUES
            (
                @IdTurno, N'INGRESO', @Monto,
                N'ABONO', @IdAbono, @IdUsuario, N'Abono de cuenta por cobrar.'
            );
        END;

        INSERT INTO sis.Bitacora
        (
            IdUsuario, Modulo, Accion, Entidad, EntidadId, Descripcion
        )
        VALUES
        (
            @IdUsuario, N'Ventas', N'REGISTRAR_ABONO', N'AbonosCliente', @IdAbono,
            CONCAT(N'Abono registrado. Recibo ', @NumeroRecibo, N'; monto: $', CONVERT(NVARCHAR(40), @Monto))
        );

        COMMIT TRANSACTION;

        SELECT
            @IdAbono AS IdAbono,
            @NumeroRecibo AS NumeroRecibo,
            @NuevoSaldo AS SaldoRestante;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* --------------------------------------------------------------------------
   4.5 Actualizar estado de cuentas vencidas
   No bloquea clientes automáticamente: la política de bloqueo sigue configurable.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE ven.sp_ActualizarCuentasVencidas
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE ven.CuentasPorCobrar
    SET Estado = N'VENCIDA'
    WHERE Saldo > 0
      AND FechaVencimiento < CONVERT(date, SYSDATETIMEOFFSET())
      AND Estado IN (N'PENDIENTE', N'PARCIAL');

    SELECT @@ROWCOUNT AS CuentasMarcadasVencidas;
END;
GO

/* ============================================================================
   5. PROCEDIMIENTOS DE COMPRAS / RECEPCIÓN
   ============================================================================ */

/* --------------------------------------------------------------------------
   5.1 Aplicar recepción de compra al inventario y costo promedio ponderado
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE cmp.sp_AplicarRecepcionCompra
    @IdRecepcion BIGINT,
    @IdUsuario INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Estado NVARCHAR(20);
        DECLARE @IdOrdenCompra BIGINT;
        DECLARE @IdImportacion BIGINT;
        DECLARE @IdAlmacen INT;

        SELECT
            @Estado = Estado,
            @IdOrdenCompra = IdOrdenCompra,
            @IdImportacion = IdImportacion,
            @IdAlmacen = IdAlmacen
        FROM cmp.RecepcionesCompra WITH (UPDLOCK, HOLDLOCK)
        WHERE IdRecepcion = @IdRecepcion;

        IF @Estado IS NULL
            THROW 51070, N'La recepción de compra no existe.', 1;

        IF @Estado <> N'BORRADOR'
            THROW 51071, N'Sólo puede aplicarse una recepción en estado BORRADOR.', 1;

        IF NOT EXISTS (SELECT 1 FROM cmp.RecepcionCompraDetalle WHERE IdRecepcion = @IdRecepcion)
            THROW 51072, N'La recepción no contiene detalle.', 1;

        DECLARE
            @IdDetalle BIGINT,
            @IdOrdenDetalle BIGINT,
            @IdProducto INT,
            @Cantidad DECIMAL(18,4),
            @CostoPuestoBodega DECIMAL(18,6),
            @NumeroLote NVARCHAR(100),
            @FechaVencimiento DATE,
            @CantidadOrdenada DECIMAL(18,4),
            @CantidadRecibidaAnterior DECIMAL(18,4),
            @ControlaLote BIT,
            @ControlaVencimiento BIT,
            @StockAnterior DECIMAL(18,4),
            @CostoAnterior DECIMAL(18,6),
            @StockPosterior DECIMAL(18,4),
            @CostoNuevo DECIMAL(18,6),
            @IdLote BIGINT;

        DECLARE curRecepcion CURSOR LOCAL FAST_FORWARD FOR
            SELECT
                rd.IdDetalle,
                rd.IdOrdenCompraDetalle,
                rd.IdProducto,
                rd.CantidadRecibida,
                rd.CostoPuestoBodega,
                rd.NumeroLote,
                rd.FechaVencimiento
            FROM cmp.RecepcionCompraDetalle rd
            WHERE rd.IdRecepcion = @IdRecepcion
            ORDER BY rd.IdProducto, rd.IdDetalle;

        OPEN curRecepcion;
        FETCH NEXT FROM curRecepcion
            INTO @IdDetalle, @IdOrdenDetalle, @IdProducto, @Cantidad,
                 @CostoPuestoBodega, @NumeroLote, @FechaVencimiento;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            SELECT
                @CantidadOrdenada = Cantidad,
                @CantidadRecibidaAnterior = CantidadRecibida
            FROM cmp.OrdenCompraDetalle WITH (UPDLOCK, HOLDLOCK)
            WHERE IdDetalle = @IdOrdenDetalle
              AND IdOrdenCompra = @IdOrdenCompra
              AND IdProducto = @IdProducto;

            IF @CantidadOrdenada IS NULL
                THROW 51073, N'El detalle recibido no corresponde a la orden de compra.', 1;

            IF @CantidadRecibidaAnterior + @Cantidad > @CantidadOrdenada
                THROW 51074, N'La recepción excede la cantidad pendiente de la orden.', 1;

            SET @ControlaLote = NULL;
            SET @ControlaVencimiento = NULL;

            SELECT
                @ControlaLote = ControlaLote,
                @ControlaVencimiento = ControlaVencimiento
            FROM inv.Productos
            WHERE IdProducto = @IdProducto AND Activo = 1;

            IF @ControlaLote IS NULL
                THROW 51075, N'La recepción contiene un producto inexistente o inactivo.', 1;

            IF @ControlaLote = 1 AND NULLIF(LTRIM(RTRIM(@NumeroLote)), N'') IS NULL
                THROW 51076, N'El producto requiere número de lote.', 1;

            IF @ControlaVencimiento = 1 AND @FechaVencimiento IS NULL
                THROW 51077, N'El producto requiere fecha de vencimiento.', 1;

            SELECT
                @StockAnterior = StockActual,
                @CostoAnterior = CostoPromedio
            FROM inv.ExistenciasProducto WITH (UPDLOCK, HOLDLOCK)
            WHERE IdAlmacen = @IdAlmacen
              AND IdProducto = @IdProducto;

            IF @StockAnterior IS NULL
            BEGIN
                SET @StockAnterior = 0;
                SET @CostoAnterior = 0;
                SET @StockPosterior = @Cantidad;
                SET @CostoNuevo = @CostoPuestoBodega;

                INSERT INTO inv.ExistenciasProducto
                (
                    IdAlmacen, IdProducto, StockActual, StockReservado,
                    CostoPromedio, FechaUltimoMovimiento
                )
                VALUES
                (
                    @IdAlmacen, @IdProducto, @StockPosterior, 0,
                    @CostoNuevo, SYSDATETIMEOFFSET()
                );
            END
            ELSE
            BEGIN
                SET @StockPosterior = @StockAnterior + @Cantidad;
                SET @CostoNuevo =
                    CAST
                    (
                        ((@StockAnterior * @CostoAnterior) + (@Cantidad * @CostoPuestoBodega))
                        / NULLIF(@StockPosterior, 0)
                        AS DECIMAL(18,6)
                    );

                UPDATE inv.ExistenciasProducto
                SET StockActual = @StockPosterior,
                    CostoPromedio = @CostoNuevo,
                    FechaUltimoMovimiento = SYSDATETIMEOFFSET()
                WHERE IdAlmacen = @IdAlmacen
                  AND IdProducto = @IdProducto;
            END;

            SET @IdLote = NULL;

            IF @ControlaLote = 1
            BEGIN
                INSERT INTO inv.LotesProducto
                (
                    IdProducto, NumeroLote, FechaFabricacion, FechaVencimiento,
                    CostoUnitarioIngreso, IdRecepcionCompraDetalle, Activo
                )
                VALUES
                (
                    @IdProducto, @NumeroLote, NULL, @FechaVencimiento,
                    @CostoPuestoBodega, @IdDetalle, 1
                );

                SET @IdLote = SCOPE_IDENTITY();

                INSERT INTO inv.ExistenciasLote
                (
                    IdAlmacen, IdLote, StockActual
                )
                VALUES
                (
                    @IdAlmacen, @IdLote, @Cantidad
                );
            END;

            INSERT INTO inv.MovimientosInventario
            (
                IdAlmacen, IdProducto, IdLote, TipoMovimiento, Cantidad,
                CostoUnitario, ExistenciaAnterior, ExistenciaPosterior,
                ReferenciaTipo, ReferenciaId, IdUsuario, Observacion
            )
            VALUES
            (
                @IdAlmacen, @IdProducto, @IdLote, N'COMPRA', @Cantidad,
                @CostoPuestoBodega, @StockAnterior, @StockPosterior,
                N'RECEPCION_COMPRA', @IdRecepcion, @IdUsuario,
                N'Entrada por recepción de compra.'
            );

            UPDATE cmp.OrdenCompraDetalle
            SET CantidadRecibida = CantidadRecibida + @Cantidad
            WHERE IdDetalle = @IdOrdenDetalle;

            SET @CantidadOrdenada = NULL;
            SET @CantidadRecibidaAnterior = NULL;
            SET @StockAnterior = NULL;
            SET @CostoAnterior = NULL;

            FETCH NEXT FROM curRecepcion
                INTO @IdDetalle, @IdOrdenDetalle, @IdProducto, @Cantidad,
                     @CostoPuestoBodega, @NumeroLote, @FechaVencimiento;
        END;

        CLOSE curRecepcion;
        DEALLOCATE curRecepcion;

        UPDATE cmp.RecepcionesCompra
        SET Estado = N'APLICADA'
        WHERE IdRecepcion = @IdRecepcion;

        DECLARE @OrdenCompleta BIT =
            CASE
                WHEN EXISTS
                (
                    SELECT 1
                    FROM cmp.OrdenCompraDetalle
                    WHERE IdOrdenCompra = @IdOrdenCompra
                      AND CantidadRecibida < Cantidad
                ) THEN 0
                ELSE 1
            END;

        IF @OrdenCompleta = 1
        BEGIN
            UPDATE cmp.OrdenesCompra
            SET Estado = N'RECIBIDA'
            WHERE IdOrdenCompra = @IdOrdenCompra
              AND Estado <> N'CANCELADA';

            IF @IdImportacion IS NOT NULL
            BEGIN
                UPDATE cmp.Importaciones
                SET Estado = N'RECIBIDA',
                    FechaRecepcion = CONVERT(date, SYSDATETIMEOFFSET())
                WHERE IdImportacion = @IdImportacion;
            END;
        END
        ELSE IF @IdImportacion IS NOT NULL
        BEGIN
            UPDATE cmp.Importaciones
            SET Estado = N'RECIBIDA_PARCIAL'
            WHERE IdImportacion = @IdImportacion
              AND Estado <> N'RECIBIDA';
        END;

        INSERT INTO sis.Bitacora
        (
            IdUsuario, Modulo, Accion, Entidad, EntidadId, Descripcion
        )
        VALUES
        (
            @IdUsuario, N'Compras', N'APLICAR_RECEPCION', N'RecepcionesCompra', @IdRecepcion,
            N'Recepción aplicada a inventario y costo promedio ponderado.'
        );

        COMMIT TRANSACTION;

        SELECT
            @IdRecepcion AS IdRecepcion,
            N'APLICADA' AS Estado,
            @OrdenCompleta AS OrdenCompleta;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local', 'curRecepcion') >= -1
        BEGIN
            IF CURSOR_STATUS('local', 'curRecepcion') > -1 CLOSE curRecepcion;
            DEALLOCATE curRecepcion;
        END;
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* ============================================================================
   6. DASHBOARD / CONSULTAS OPERATIVAS
   ============================================================================ */

/* --------------------------------------------------------------------------
   6.1 Resumen principal de dashboard
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE sis.sp_DashboardResumen
    @Fecha DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Fecha IS NULL
        SET @Fecha = CONVERT(date, SYSDATETIMEOFFSET());

    DECLARE @AlertaVencimientoDias INT =
        ISNULL((SELECT TRY_CONVERT(INT, Valor) FROM sis.Configuracion WHERE Clave = N'Inventario.AlertaVencimientoDias'), 90);

    SELECT
        @Fecha AS Fecha,
        (SELECT COUNT_BIG(*)
         FROM ven.Ventas
         WHERE Estado = N'CONFIRMADA'
           AND CONVERT(date, Fecha) = @Fecha) AS CantidadVentas,

        (SELECT CAST(ISNULL(SUM(Total), 0) AS DECIMAL(18,2))
         FROM ven.Ventas
         WHERE Estado = N'CONFIRMADA'
           AND CONVERT(date, Fecha) = @Fecha) AS VentasTotal,

        (SELECT CAST(ISNULL(SUM((Subtotal - DescuentoTotal) - CostoTotal), 0) AS DECIMAL(18,2))
         FROM ven.Ventas
         WHERE Estado = N'CONFIRMADA'
           AND CONVERT(date, Fecha) = @Fecha) AS UtilidadEstimada,

        (SELECT COUNT_BIG(*)
         FROM inv.ExistenciasProducto ep
         INNER JOIN inv.Productos p ON p.IdProducto = ep.IdProducto
         WHERE p.Activo = 1
           AND ep.StockActual - ep.StockReservado <= p.StockMinimo) AS ProductosStockCritico,

        (SELECT COUNT_BIG(*)
         FROM inv.ExistenciasLote el
         INNER JOIN inv.LotesProducto lp ON lp.IdLote = el.IdLote
         WHERE el.StockActual > 0
           AND lp.Activo = 1
           AND lp.FechaVencimiento IS NOT NULL
           AND lp.FechaVencimiento BETWEEN @Fecha AND DATEADD(DAY, @AlertaVencimientoDias, @Fecha)) AS LotesProximosVencer,

        (SELECT CAST(ISNULL(SUM(Saldo), 0) AS DECIMAL(18,2))
         FROM ven.CuentasPorCobrar
         WHERE Saldo > 0
           AND FechaVencimiento < @Fecha
           AND Estado <> N'CASTIGADA') AS CarteraVencida,

        (SELECT COUNT_BIG(*)
         FROM cmp.Importaciones
         WHERE Estado NOT IN (N'RECIBIDA')) AS ImportacionesActivas,

        (SELECT COUNT_BIG(*)
         FROM com.Prospectos
         WHERE NoContactar = 0
           AND IdClienteConvertido IS NULL
           AND Estado IN (N'NUEVO', N'REVISADO')) AS ProspectosPendientes;
END;
GO

/* ============================================================================
   7. VALIDACIÓN FINAL
   ============================================================================ */
DECLARE @VistasEsperadas TABLE (Nombre SYSNAME);
INSERT INTO @VistasEsperadas VALUES
    (N'inv.vwInventarioActual'),
    (N'inv.vwLotesVencimiento'),
    (N'cmp.vwOrdenesCompraSeguimiento'),
    (N'cmp.vwImportacionesSeguimiento'),
    (N'ven.vwVentasResumen'),
    (N'ven.vwCuentasPorCobrarEstado'),
    (N'fis.vwEstadoFiscalVentas'),
    (N'com.vwProspectosPendientes');

DECLARE @ProcedimientosEsperados TABLE (Nombre SYSNAME);
INSERT INTO @ProcedimientosEsperados VALUES
    (N'sis.sp_SiguienteCorrelativo'),
    (N'seg.sp_ObtenerUsuarioLogin'),
    (N'seg.sp_ObtenerPermisosUsuario'),
    (N'inv.sp_BuscarProducto'),
    (N'inv.sp_ObtenerStockProducto'),
    (N'ven.sp_AbrirTurnoCaja'),
    (N'ven.sp_CerrarTurnoCaja'),
    (N'ven.sp_ConfirmarVenta'),
    (N'ven.sp_RegistrarAbono'),
    (N'ven.sp_ActualizarCuentasVencidas'),
    (N'cmp.sp_AplicarRecepcionCompra'),
    (N'sis.sp_DashboardResumen');

IF EXISTS
(
    SELECT 1 FROM @VistasEsperadas
    WHERE OBJECT_ID(Nombre, N'V') IS NULL
)
BEGIN
    SELECT Nombre AS VistaFaltante
    FROM @VistasEsperadas
    WHERE OBJECT_ID(Nombre, N'V') IS NULL;

    THROW 51090, N'No se crearon todas las vistas esperadas.', 1;
END;

IF EXISTS
(
    SELECT 1 FROM @ProcedimientosEsperados
    WHERE OBJECT_ID(Nombre, N'P') IS NULL
)
BEGIN
    SELECT Nombre AS ProcedimientoFaltante
    FROM @ProcedimientosEsperados
    WHERE OBJECT_ID(Nombre, N'P') IS NULL;

    THROW 51091, N'No se crearon todos los procedimientos esperados.', 1;
END;

PRINT N'008_ViewsProcedures.sql completado correctamente.';
PRINT N'Vistas creadas/actualizadas: 8.';
PRINT N'Procedimientos creados/actualizados: 12.';
PRINT N'Triggers creados: 0.';
GO
