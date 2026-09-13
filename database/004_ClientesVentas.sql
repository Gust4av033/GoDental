/*
===============================================================================
 GoDental - 004_ClientesVentas.sql
 Versión: 0.1
 Base documental: DM-01 v0.2 + DB-01 v0.1
 Motor: Microsoft SQL Server

 Dependencias:
   - 001_Core.sql
   - 002_Inventario.sql

 Alcance:
   1) Clientes, contactos y direcciones.
   2) Caja y turnos.
   3) Listas de precio y promociones.
   4) Cotizaciones.
   5) Ventas y pagos mixtos.
   6) Crédito, cuentas por cobrar y abonos.
   7) Devoluciones y evidencias.

 Fuera de alcance:
   - DTE / documentos fiscales (005_Fiscal_Base.sql)
   - Comercial Online (006_Comercial_Base.sql)
   - Datos iniciales (007_Seed.sql)
   - Vistas/procedimientos de operación (008_ViewsProcedures.sql)

 Decisiones de diseño:
   - No se utilizan triggers en esta versión.
   - Las cotizaciones no afectan inventario.
   - Las ventas confirmadas deberán procesarse en una transacción explícita.
   - Los pagos mixtos se representan con múltiples filas en ven.VentaPagos.
   - No se almacenan PAN completo, CVV ni datos sensibles de tarjetas.
   - Venta y DTE permanecen desacoplados.
===============================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF DB_ID(N'GoDental') IS NULL
    THROW 50020, N'La base de datos GoDental no existe. Ejecute primero 001_Core.sql.', 1;
GO

USE [GoDental];
GO

IF OBJECT_ID(N'cat.TiposCliente', N'U') IS NULL
    THROW 50021, N'Falta cat.TiposCliente. Ejecute primero 001_Core.sql.', 1;
IF OBJECT_ID(N'cat.MetodosPago', N'U') IS NULL
    THROW 50022, N'Falta cat.MetodosPago. Ejecute primero 001_Core.sql.', 1;
IF OBJECT_ID(N'seg.Usuarios', N'U') IS NULL
    THROW 50023, N'Falta seg.Usuarios. Ejecute primero 001_Core.sql.', 1;
IF OBJECT_ID(N'sis.EquiposTrabajo', N'U') IS NULL
    THROW 50024, N'Falta sis.EquiposTrabajo. Ejecute primero 001_Core.sql.', 1;
IF OBJECT_ID(N'inv.Almacenes', N'U') IS NULL OR OBJECT_ID(N'inv.Productos', N'U') IS NULL
    THROW 50025, N'Falta Inventario base. Ejecute primero 002_Inventario.sql.', 1;
GO

/* ==========================================================================
   1. PRECIOS BASE
   Se crean antes de cli.Clientes porque el cliente puede tener lista asignada.
   ========================================================================== */

IF OBJECT_ID(N'ven.ListasPrecio', N'U') IS NULL
BEGIN
    CREATE TABLE ven.ListasPrecio
    (
        IdListaPrecio      INT IDENTITY(1,1) NOT NULL,
        Codigo             NVARCHAR(30) NOT NULL,
        Nombre             NVARCHAR(100) NOT NULL,
        IdTipoCliente      SMALLINT NULL,
        MontoMinimoVenta   DECIMAL(18,2) NULL,
        Prioridad          SMALLINT NOT NULL,
        FechaInicio        DATE NULL,
        FechaFin           DATE NULL,
        Activo             BIT NOT NULL CONSTRAINT DF_ListasPrecio_Activo DEFAULT (1),

        CONSTRAINT PK_ListasPrecio PRIMARY KEY CLUSTERED (IdListaPrecio),
        CONSTRAINT UQ_ListasPrecio_Codigo UNIQUE (Codigo),
        CONSTRAINT FK_ListasPrecio_TiposCliente
            FOREIGN KEY (IdTipoCliente) REFERENCES cat.TiposCliente(IdTipoCliente),
        CONSTRAINT CK_ListasPrecio_MontoMinimo
            CHECK (MontoMinimoVenta IS NULL OR MontoMinimoVenta >= 0),
        CONSTRAINT CK_ListasPrecio_Prioridad
            CHECK (Prioridad >= 0),
        CONSTRAINT CK_ListasPrecio_Fechas
            CHECK (FechaInicio IS NULL OR FechaFin IS NULL OR FechaFin >= FechaInicio)
    );
END;
GO

IF OBJECT_ID(N'ven.ListaPrecioProductos', N'U') IS NULL
BEGIN
    CREATE TABLE ven.ListaPrecioProductos
    (
        IdListaPrecio   INT NOT NULL,
        IdProducto      INT NOT NULL,
        CantidadMinima  DECIMAL(18,4) NOT NULL,
        Precio          DECIMAL(18,2) NOT NULL,
        Activo          BIT NOT NULL CONSTRAINT DF_ListaPrecioProductos_Activo DEFAULT (1),

        CONSTRAINT PK_ListaPrecioProductos
            PRIMARY KEY CLUSTERED (IdListaPrecio, IdProducto, CantidadMinima),
        CONSTRAINT FK_ListaPrecioProductos_ListasPrecio
            FOREIGN KEY (IdListaPrecio) REFERENCES ven.ListasPrecio(IdListaPrecio),
        CONSTRAINT FK_ListaPrecioProductos_Productos
            FOREIGN KEY (IdProducto) REFERENCES inv.Productos(IdProducto),
        CONSTRAINT CK_ListaPrecioProductos_Cantidad CHECK (CantidadMinima > 0),
        CONSTRAINT CK_ListaPrecioProductos_Precio CHECK (Precio >= 0)
    );
END;
GO

/* ==========================================================================
   2. CLIENTES
   ========================================================================== */

IF OBJECT_ID(N'cli.Clientes', N'U') IS NULL
BEGIN
    CREATE TABLE cli.Clientes
    (
        IdCliente                BIGINT IDENTITY(1,1) NOT NULL,
        Codigo                   NVARCHAR(30) NOT NULL,
        IdTipoCliente            SMALLINT NOT NULL,
        NombreRazonSocial        NVARCHAR(200) NOT NULL,
        NombreComercial          NVARCHAR(200) NULL,
        NIT                      NVARCHAR(30) NULL,
        NRC                      NVARCHAR(30) NULL,
        DUI                      NVARCHAR(20) NULL,
        Giro                     NVARCHAR(250) NULL,
        Telefono                 NVARCHAR(30) NULL,
        WhatsApp                 NVARCHAR(30) NULL,
        Email                    NVARCHAR(254) NULL,
        IdListaPrecio            INT NULL,
        PermiteCredito           BIT NOT NULL CONSTRAINT DF_Clientes_PermiteCredito DEFAULT (0),
        LimiteCredito            DECIMAL(18,2) NULL,
        DiasCredito              SMALLINT NULL,
        IntervaloAvisoDias       SMALLINT NULL,
        BloqueadoCredito         BIT NOT NULL CONSTRAINT DF_Clientes_BloqueadoCredito DEFAULT (0),
        MotivoBloqueoCredito     NVARCHAR(500) NULL,
        Activo                   BIT NOT NULL CONSTRAINT DF_Clientes_Activo DEFAULT (1),
        RowVersion               ROWVERSION NOT NULL,

        CONSTRAINT PK_Clientes PRIMARY KEY CLUSTERED (IdCliente),
        CONSTRAINT UQ_Clientes_Codigo UNIQUE (Codigo),
        CONSTRAINT FK_Clientes_TiposCliente
            FOREIGN KEY (IdTipoCliente) REFERENCES cat.TiposCliente(IdTipoCliente),
        CONSTRAINT FK_Clientes_ListasPrecio
            FOREIGN KEY (IdListaPrecio) REFERENCES ven.ListasPrecio(IdListaPrecio),
        CONSTRAINT CK_Clientes_LimiteCredito
            CHECK (LimiteCredito IS NULL OR LimiteCredito >= 0),
        CONSTRAINT CK_Clientes_DiasCredito
            CHECK (DiasCredito IS NULL OR DiasCredito >= 0),
        CONSTRAINT CK_Clientes_IntervaloAviso
            CHECK (IntervaloAvisoDias IS NULL OR IntervaloAvisoDias > 0)
    );
END;
GO

IF OBJECT_ID(N'cli.ClienteContactos', N'U') IS NULL
BEGIN
    CREATE TABLE cli.ClienteContactos
    (
        IdContacto   BIGINT IDENTITY(1,1) NOT NULL,
        IdCliente    BIGINT NOT NULL,
        Nombre       NVARCHAR(150) NOT NULL,
        Cargo        NVARCHAR(100) NULL,
        Telefono     NVARCHAR(30) NULL,
        WhatsApp     NVARCHAR(30) NULL,
        Email        NVARCHAR(254) NULL,
        EsPrincipal  BIT NOT NULL CONSTRAINT DF_ClienteContactos_EsPrincipal DEFAULT (0),
        Activo       BIT NOT NULL CONSTRAINT DF_ClienteContactos_Activo DEFAULT (1),

        CONSTRAINT PK_ClienteContactos PRIMARY KEY CLUSTERED (IdContacto),
        CONSTRAINT FK_ClienteContactos_Clientes
            FOREIGN KEY (IdCliente) REFERENCES cli.Clientes(IdCliente)
    );
END;
GO

IF OBJECT_ID(N'cli.ClienteDirecciones', N'U') IS NULL
BEGIN
    CREATE TABLE cli.ClienteDirecciones
    (
        IdDireccion   BIGINT IDENTITY(1,1) NOT NULL,
        IdCliente     BIGINT NOT NULL,
        Tipo          NVARCHAR(20) NOT NULL,
        Departamento  NVARCHAR(100) NULL,
        Municipio     NVARCHAR(100) NULL,
        Direccion     NVARCHAR(500) NOT NULL,
        Referencia    NVARCHAR(300) NULL,
        EsPrincipal   BIT NOT NULL CONSTRAINT DF_ClienteDirecciones_EsPrincipal DEFAULT (0),
        Activo        BIT NOT NULL CONSTRAINT DF_ClienteDirecciones_Activo DEFAULT (1),

        CONSTRAINT PK_ClienteDirecciones PRIMARY KEY CLUSTERED (IdDireccion),
        CONSTRAINT FK_ClienteDirecciones_Clientes
            FOREIGN KEY (IdCliente) REFERENCES cli.Clientes(IdCliente),
        CONSTRAINT CK_ClienteDirecciones_Tipo
            CHECK (Tipo IN (N'FISCAL', N'ENTREGA', N'OTRA'))
    );
END;
GO

/* ==========================================================================
   3. CAJA
   ========================================================================== */

IF OBJECT_ID(N'ven.Cajas', N'U') IS NULL
BEGIN
    CREATE TABLE ven.Cajas
    (
        IdCaja             INT IDENTITY(1,1) NOT NULL,
        Codigo             NVARCHAR(30) NOT NULL,
        Nombre             NVARCHAR(100) NOT NULL,
        IdAlmacen          INT NOT NULL,
        IdEquipoPreferido  INT NULL,
        Activo             BIT NOT NULL CONSTRAINT DF_Cajas_Activo DEFAULT (1),

        CONSTRAINT PK_Cajas PRIMARY KEY CLUSTERED (IdCaja),
        CONSTRAINT UQ_Cajas_Codigo UNIQUE (Codigo),
        CONSTRAINT FK_Cajas_Almacenes
            FOREIGN KEY (IdAlmacen) REFERENCES inv.Almacenes(IdAlmacen),
        CONSTRAINT FK_Cajas_EquiposTrabajo
            FOREIGN KEY (IdEquipoPreferido) REFERENCES sis.EquiposTrabajo(IdEquipo)
    );
END;
GO

IF OBJECT_ID(N'ven.TurnosCaja', N'U') IS NULL
BEGIN
    CREATE TABLE ven.TurnosCaja
    (
        IdTurno           BIGINT IDENTITY(1,1) NOT NULL,
        IdCaja            INT NOT NULL,
        FechaApertura     DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_TurnosCaja_FechaApertura DEFAULT (SYSDATETIMEOFFSET()),
        AbiertoPor        INT NOT NULL,
        MontoInicial      DECIMAL(18,2) NOT NULL,
        FechaCierre       DATETIMEOFFSET(0) NULL,
        CerradoPor        INT NULL,
        EfectivoEsperado  DECIMAL(18,2) NULL,
        EfectivoContado   DECIMAL(18,2) NULL,
        Diferencia        DECIMAL(18,2) NULL,
        Estado            NVARCHAR(20) NOT NULL CONSTRAINT DF_TurnosCaja_Estado DEFAULT (N'ABIERTO'),
        Observaciones     NVARCHAR(500) NULL,

        CONSTRAINT PK_TurnosCaja PRIMARY KEY CLUSTERED (IdTurno),
        CONSTRAINT FK_TurnosCaja_Cajas
            FOREIGN KEY (IdCaja) REFERENCES ven.Cajas(IdCaja),
        CONSTRAINT FK_TurnosCaja_AbiertoPor
            FOREIGN KEY (AbiertoPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT FK_TurnosCaja_CerradoPor
            FOREIGN KEY (CerradoPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_TurnosCaja_MontoInicial CHECK (MontoInicial >= 0),
        CONSTRAINT CK_TurnosCaja_EfectivoEsperado CHECK (EfectivoEsperado IS NULL OR EfectivoEsperado >= 0),
        CONSTRAINT CK_TurnosCaja_EfectivoContado CHECK (EfectivoContado IS NULL OR EfectivoContado >= 0),
        CONSTRAINT CK_TurnosCaja_Estado CHECK (Estado IN (N'ABIERTO', N'CERRADO', N'CANCELADO')),
        CONSTRAINT CK_TurnosCaja_Fechas CHECK (FechaCierre IS NULL OR FechaCierre >= FechaApertura)
    );
END;
GO

IF OBJECT_ID(N'ven.MovimientosCaja', N'U') IS NULL
BEGIN
    CREATE TABLE ven.MovimientosCaja
    (
        IdMovimientoCaja  BIGINT IDENTITY(1,1) NOT NULL,
        IdTurno           BIGINT NOT NULL,
        Fecha             DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_MovimientosCaja_Fecha DEFAULT (SYSDATETIMEOFFSET()),
        Tipo              NVARCHAR(30) NOT NULL,
        Monto             DECIMAL(18,2) NOT NULL,
        ReferenciaTipo    NVARCHAR(30) NULL,
        ReferenciaId      BIGINT NULL,
        IdUsuario         INT NOT NULL,
        Motivo            NVARCHAR(500) NULL,

        CONSTRAINT PK_MovimientosCaja PRIMARY KEY CLUSTERED (IdMovimientoCaja),
        CONSTRAINT FK_MovimientosCaja_TurnosCaja
            FOREIGN KEY (IdTurno) REFERENCES ven.TurnosCaja(IdTurno),
        CONSTRAINT FK_MovimientosCaja_Usuarios
            FOREIGN KEY (IdUsuario) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_MovimientosCaja_Monto CHECK (Monto > 0),
        CONSTRAINT CK_MovimientosCaja_Tipo
            CHECK (Tipo IN (N'VENTA_EFECTIVO', N'INGRESO', N'RETIRO', N'DEVOLUCION_EFECTIVO', N'AJUSTE')),
        CONSTRAINT CK_MovimientosCaja_Motivo
            CHECK (Tipo NOT IN (N'RETIRO', N'AJUSTE') OR NULLIF(LTRIM(RTRIM(Motivo)), N'') IS NOT NULL)
    );
END;
GO

/* ==========================================================================
   4. COTIZACIONES
   ========================================================================== */

IF OBJECT_ID(N'ven.Cotizaciones', N'U') IS NULL
BEGIN
    CREATE TABLE ven.Cotizaciones
    (
        IdCotizacion      BIGINT IDENTITY(1,1) NOT NULL,
        NumeroCotizacion  NVARCHAR(40) NOT NULL,
        IdCliente         BIGINT NULL,
        Fecha             DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_Cotizaciones_Fecha DEFAULT (SYSDATETIMEOFFSET()),
        ValidaHasta       DATE NULL,
        Estado            NVARCHAR(20) NOT NULL CONSTRAINT DF_Cotizaciones_Estado DEFAULT (N'BORRADOR'),
        Subtotal          DECIMAL(18,2) NOT NULL CONSTRAINT DF_Cotizaciones_Subtotal DEFAULT (0),
        Descuento         DECIMAL(18,2) NOT NULL CONSTRAINT DF_Cotizaciones_Descuento DEFAULT (0),
        Impuesto          DECIMAL(18,2) NOT NULL CONSTRAINT DF_Cotizaciones_Impuesto DEFAULT (0),
        Total             DECIMAL(18,2) NOT NULL CONSTRAINT DF_Cotizaciones_Total DEFAULT (0),
        CreadaPor         INT NOT NULL,
        Observaciones     NVARCHAR(1000) NULL,

        CONSTRAINT PK_Cotizaciones PRIMARY KEY CLUSTERED (IdCotizacion),
        CONSTRAINT UQ_Cotizaciones_Numero UNIQUE (NumeroCotizacion),
        CONSTRAINT FK_Cotizaciones_Clientes
            FOREIGN KEY (IdCliente) REFERENCES cli.Clientes(IdCliente),
        CONSTRAINT FK_Cotizaciones_Usuarios
            FOREIGN KEY (CreadaPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_Cotizaciones_Estado
            CHECK (Estado IN (N'BORRADOR', N'EMITIDA', N'ACEPTADA', N'VENCIDA', N'CANCELADA', N'CONVERTIDA')),
        CONSTRAINT CK_Cotizaciones_Totales
            CHECK (Subtotal >= 0 AND Descuento >= 0 AND Impuesto >= 0 AND Total >= 0)
    );
END;
GO

IF OBJECT_ID(N'ven.CotizacionDetalle', N'U') IS NULL
BEGIN
    CREATE TABLE ven.CotizacionDetalle
    (
        IdDetalle              BIGINT IDENTITY(1,1) NOT NULL,
        IdCotizacion           BIGINT NOT NULL,
        IdProducto             INT NOT NULL,
        DescripcionSnapshot    NVARCHAR(250) NOT NULL,
        Cantidad               DECIMAL(18,4) NOT NULL,
        PrecioUnitario         DECIMAL(18,2) NOT NULL,
        DescuentoPorcentaje    DECIMAL(9,4) NOT NULL CONSTRAINT DF_CotizacionDetalle_DescPct DEFAULT (0),
        DescuentoMonto         DECIMAL(18,2) NOT NULL CONSTRAINT DF_CotizacionDetalle_DescMonto DEFAULT (0),
        ImpuestoMonto          DECIMAL(18,2) NOT NULL CONSTRAINT DF_CotizacionDetalle_Impuesto DEFAULT (0),
        TotalLinea             DECIMAL(18,2) NOT NULL,

        CONSTRAINT PK_CotizacionDetalle PRIMARY KEY CLUSTERED (IdDetalle),
        CONSTRAINT FK_CotizacionDetalle_Cotizaciones
            FOREIGN KEY (IdCotizacion) REFERENCES ven.Cotizaciones(IdCotizacion),
        CONSTRAINT FK_CotizacionDetalle_Productos
            FOREIGN KEY (IdProducto) REFERENCES inv.Productos(IdProducto),
        CONSTRAINT CK_CotizacionDetalle_Cantidad CHECK (Cantidad > 0),
        CONSTRAINT CK_CotizacionDetalle_Precio CHECK (PrecioUnitario >= 0),
        CONSTRAINT CK_CotizacionDetalle_DescuentoPorcentaje CHECK (DescuentoPorcentaje >= 0 AND DescuentoPorcentaje <= 100),
        CONSTRAINT CK_CotizacionDetalle_DescuentoMonto CHECK (DescuentoMonto >= 0),
        CONSTRAINT CK_CotizacionDetalle_Impuesto CHECK (ImpuestoMonto >= 0),
        CONSTRAINT CK_CotizacionDetalle_Total CHECK (TotalLinea >= 0)
    );
END;
GO

/* ==========================================================================
   5. VENTAS Y PAGOS
   ========================================================================== */

IF OBJECT_ID(N'ven.Ventas', N'U') IS NULL
BEGIN
    CREATE TABLE ven.Ventas
    (
        IdVenta           BIGINT IDENTITY(1,1) NOT NULL,
        NumeroVenta       NVARCHAR(40) NOT NULL,
        IdCliente         BIGINT NULL,
        IdCotizacion      BIGINT NULL,
        IdCaja            INT NOT NULL,
        IdTurno           BIGINT NULL,
        IdVendedor        INT NOT NULL,
        IdEquipo          INT NOT NULL,
        Fecha             DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_Ventas_Fecha DEFAULT (SYSDATETIMEOFFSET()),
        Estado            NVARCHAR(20) NOT NULL CONSTRAINT DF_Ventas_Estado DEFAULT (N'BORRADOR'),
        Subtotal          DECIMAL(18,2) NOT NULL CONSTRAINT DF_Ventas_Subtotal DEFAULT (0),
        DescuentoTotal    DECIMAL(18,2) NOT NULL CONSTRAINT DF_Ventas_Descuento DEFAULT (0),
        ImpuestoTotal     DECIMAL(18,2) NOT NULL CONSTRAINT DF_Ventas_Impuesto DEFAULT (0),
        Total             DECIMAL(18,2) NOT NULL CONSTRAINT DF_Ventas_Total DEFAULT (0),
        CostoTotal        DECIMAL(18,6) NOT NULL CONSTRAINT DF_Ventas_Costo DEFAULT (0),
        SaldoPendiente    DECIMAL(18,2) NOT NULL CONSTRAINT DF_Ventas_Saldo DEFAULT (0),
        Observaciones     NVARCHAR(1000) NULL,
        RowVersion        ROWVERSION NOT NULL,

        CONSTRAINT PK_Ventas PRIMARY KEY CLUSTERED (IdVenta),
        CONSTRAINT UQ_Ventas_NumeroVenta UNIQUE (NumeroVenta),
        CONSTRAINT FK_Ventas_Clientes
            FOREIGN KEY (IdCliente) REFERENCES cli.Clientes(IdCliente),
        CONSTRAINT FK_Ventas_Cotizaciones
            FOREIGN KEY (IdCotizacion) REFERENCES ven.Cotizaciones(IdCotizacion),
        CONSTRAINT FK_Ventas_Cajas
            FOREIGN KEY (IdCaja) REFERENCES ven.Cajas(IdCaja),
        CONSTRAINT FK_Ventas_TurnosCaja
            FOREIGN KEY (IdTurno) REFERENCES ven.TurnosCaja(IdTurno),
        CONSTRAINT FK_Ventas_Vendedores
            FOREIGN KEY (IdVendedor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT FK_Ventas_EquiposTrabajo
            FOREIGN KEY (IdEquipo) REFERENCES sis.EquiposTrabajo(IdEquipo),
        CONSTRAINT CK_Ventas_Estado
            CHECK (Estado IN (N'BORRADOR', N'CONFIRMADA', N'ANULADA', N'DEVUELTA_PARCIAL', N'DEVUELTA_TOTAL')),
        CONSTRAINT CK_Ventas_Totales
            CHECK (Subtotal >= 0 AND DescuentoTotal >= 0 AND ImpuestoTotal >= 0 AND Total >= 0 AND CostoTotal >= 0 AND SaldoPendiente >= 0)
    );
END;
GO

IF OBJECT_ID(N'ven.VentaDetalle', N'U') IS NULL
BEGIN
    CREATE TABLE ven.VentaDetalle
    (
        IdDetalle               BIGINT IDENTITY(1,1) NOT NULL,
        IdVenta                 BIGINT NOT NULL,
        IdProducto              INT NOT NULL,
        IdLote                  BIGINT NULL,
        SkuSnapshot             NVARCHAR(60) NOT NULL,
        DescripcionSnapshot     NVARCHAR(250) NOT NULL,
        Cantidad                DECIMAL(18,4) NOT NULL,
        PrecioLista             DECIMAL(18,2) NOT NULL,
        PrecioUnitario          DECIMAL(18,2) NOT NULL,
        DescuentoPorcentaje     DECIMAL(9,4) NOT NULL CONSTRAINT DF_VentaDetalle_DescPct DEFAULT (0),
        DescuentoMonto          DECIMAL(18,2) NOT NULL CONSTRAINT DF_VentaDetalle_DescMonto DEFAULT (0),
        CostoUnitario           DECIMAL(18,6) NOT NULL,
        ImpuestoMonto           DECIMAL(18,2) NOT NULL CONSTRAINT DF_VentaDetalle_Impuesto DEFAULT (0),
        TotalLinea              DECIMAL(18,2) NOT NULL,

        CONSTRAINT PK_VentaDetalle PRIMARY KEY CLUSTERED (IdDetalle),
        CONSTRAINT FK_VentaDetalle_Ventas
            FOREIGN KEY (IdVenta) REFERENCES ven.Ventas(IdVenta),
        CONSTRAINT FK_VentaDetalle_Productos
            FOREIGN KEY (IdProducto) REFERENCES inv.Productos(IdProducto),
        CONSTRAINT FK_VentaDetalle_Lotes
            FOREIGN KEY (IdLote) REFERENCES inv.LotesProducto(IdLote),
        CONSTRAINT CK_VentaDetalle_Cantidad CHECK (Cantidad > 0),
        CONSTRAINT CK_VentaDetalle_PrecioLista CHECK (PrecioLista >= 0),
        CONSTRAINT CK_VentaDetalle_PrecioUnitario CHECK (PrecioUnitario >= 0),
        CONSTRAINT CK_VentaDetalle_DescuentoPorcentaje CHECK (DescuentoPorcentaje >= 0 AND DescuentoPorcentaje <= 100),
        CONSTRAINT CK_VentaDetalle_DescuentoMonto CHECK (DescuentoMonto >= 0),
        CONSTRAINT CK_VentaDetalle_Costo CHECK (CostoUnitario >= 0),
        CONSTRAINT CK_VentaDetalle_Impuesto CHECK (ImpuestoMonto >= 0),
        CONSTRAINT CK_VentaDetalle_Total CHECK (TotalLinea >= 0)
    );
END;
GO

IF OBJECT_ID(N'ven.VentaPagos', N'U') IS NULL
BEGIN
    CREATE TABLE ven.VentaPagos
    (
        IdPago             BIGINT IDENTITY(1,1) NOT NULL,
        IdVenta            BIGINT NOT NULL,
        IdMetodoPago       SMALLINT NOT NULL,
        Monto              DECIMAL(18,2) NOT NULL,
        Fecha              DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_VentaPagos_Fecha DEFAULT (SYSDATETIMEOFFSET()),
        Referencia         NVARCHAR(150) NULL,
        EntidadFinanciera  NVARCHAR(120) NULL,
        Ultimos4           CHAR(4) NULL,
        IdUsuario          INT NOT NULL,
        Estado             NVARCHAR(20) NOT NULL CONSTRAINT DF_VentaPagos_Estado DEFAULT (N'REGISTRADO'),
        Observacion        NVARCHAR(300) NULL,

        CONSTRAINT PK_VentaPagos PRIMARY KEY CLUSTERED (IdPago),
        CONSTRAINT FK_VentaPagos_Ventas
            FOREIGN KEY (IdVenta) REFERENCES ven.Ventas(IdVenta),
        CONSTRAINT FK_VentaPagos_MetodosPago
            FOREIGN KEY (IdMetodoPago) REFERENCES cat.MetodosPago(IdMetodoPago),
        CONSTRAINT FK_VentaPagos_Usuarios
            FOREIGN KEY (IdUsuario) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_VentaPagos_Monto CHECK (Monto > 0),
        CONSTRAINT CK_VentaPagos_Estado CHECK (Estado IN (N'REGISTRADO', N'CONFIRMADO', N'ANULADO')),
        CONSTRAINT CK_VentaPagos_Ultimos4
            CHECK (Ultimos4 IS NULL OR Ultimos4 NOT LIKE '%[^0-9]%')
    );
END;
GO

/* ==========================================================================
   6. CRÉDITO Y ABONOS
   ========================================================================== */

IF OBJECT_ID(N'ven.CuentasPorCobrar', N'U') IS NULL
BEGIN
    CREATE TABLE ven.CuentasPorCobrar
    (
        IdCuenta          BIGINT IDENTITY(1,1) NOT NULL,
        IdVenta           BIGINT NOT NULL,
        IdCliente         BIGINT NOT NULL,
        FechaEmision      DATE NOT NULL,
        FechaVencimiento  DATE NOT NULL,
        MontoOriginal     DECIMAL(18,2) NOT NULL,
        Saldo             DECIMAL(18,2) NOT NULL,
        Estado            NVARCHAR(20) NOT NULL CONSTRAINT DF_CuentasPorCobrar_Estado DEFAULT (N'PENDIENTE'),
        UltimoAviso       DATE NULL,
        ProximoAviso      DATE NULL,

        CONSTRAINT PK_CuentasPorCobrar PRIMARY KEY CLUSTERED (IdCuenta),
        CONSTRAINT UQ_CuentasPorCobrar_IdVenta UNIQUE (IdVenta),
        CONSTRAINT FK_CuentasPorCobrar_Ventas
            FOREIGN KEY (IdVenta) REFERENCES ven.Ventas(IdVenta),
        CONSTRAINT FK_CuentasPorCobrar_Clientes
            FOREIGN KEY (IdCliente) REFERENCES cli.Clientes(IdCliente),
        CONSTRAINT CK_CuentasPorCobrar_Monto CHECK (MontoOriginal >= 0),
        CONSTRAINT CK_CuentasPorCobrar_Saldo CHECK (Saldo >= 0 AND Saldo <= MontoOriginal),
        CONSTRAINT CK_CuentasPorCobrar_Estado
            CHECK (Estado IN (N'PENDIENTE', N'PARCIAL', N'PAGADA', N'VENCIDA', N'CASTIGADA')),
        CONSTRAINT CK_CuentasPorCobrar_Fechas CHECK (FechaVencimiento >= FechaEmision)
    );
END;
GO

IF OBJECT_ID(N'ven.AbonosCliente', N'U') IS NULL
BEGIN
    CREATE TABLE ven.AbonosCliente
    (
        IdAbono        BIGINT IDENTITY(1,1) NOT NULL,
        IdCuenta       BIGINT NOT NULL,
        IdMetodoPago   SMALLINT NOT NULL,
        Monto          DECIMAL(18,2) NOT NULL,
        Fecha          DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_AbonosCliente_Fecha DEFAULT (SYSDATETIMEOFFSET()),
        Referencia     NVARCHAR(150) NULL,
        RegistradoPor  INT NOT NULL,
        NumeroRecibo   NVARCHAR(40) NOT NULL,
        Anulado        BIT NOT NULL CONSTRAINT DF_AbonosCliente_Anulado DEFAULT (0),

        CONSTRAINT PK_AbonosCliente PRIMARY KEY CLUSTERED (IdAbono),
        CONSTRAINT UQ_AbonosCliente_NumeroRecibo UNIQUE (NumeroRecibo),
        CONSTRAINT FK_AbonosCliente_CuentasPorCobrar
            FOREIGN KEY (IdCuenta) REFERENCES ven.CuentasPorCobrar(IdCuenta),
        CONSTRAINT FK_AbonosCliente_MetodosPago
            FOREIGN KEY (IdMetodoPago) REFERENCES cat.MetodosPago(IdMetodoPago),
        CONSTRAINT FK_AbonosCliente_Usuarios
            FOREIGN KEY (RegistradoPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_AbonosCliente_Monto CHECK (Monto > 0)
    );
END;
GO

/* ==========================================================================
   7. DEVOLUCIONES
   ========================================================================== */

IF OBJECT_ID(N'ven.DevolucionesVenta', N'U') IS NULL
BEGIN
    CREATE TABLE ven.DevolucionesVenta
    (
        IdDevolucion       BIGINT IDENTITY(1,1) NOT NULL,
        NumeroDevolucion   NVARCHAR(40) NOT NULL,
        IdVenta            BIGINT NOT NULL,
        FechaSolicitud     DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_DevolucionesVenta_FechaSolicitud DEFAULT (SYSDATETIMEOFFSET()),
        SolicitadaPor      INT NOT NULL,
        AutorizadaPor      INT NULL,
        FechaAutorizacion  DATETIMEOFFSET(0) NULL,
        Motivo             NVARCHAR(1000) NOT NULL,
        FormaReintegro     NVARCHAR(20) NOT NULL,
        MontoReintegro     DECIMAL(18,2) NOT NULL,
        Estado             NVARCHAR(20) NOT NULL CONSTRAINT DF_DevolucionesVenta_Estado DEFAULT (N'SOLICITADA'),
        IdAutorizacion     BIGINT NULL,

        CONSTRAINT PK_DevolucionesVenta PRIMARY KEY CLUSTERED (IdDevolucion),
        CONSTRAINT UQ_DevolucionesVenta_Numero UNIQUE (NumeroDevolucion),
        CONSTRAINT FK_DevolucionesVenta_Ventas
            FOREIGN KEY (IdVenta) REFERENCES ven.Ventas(IdVenta),
        CONSTRAINT FK_DevolucionesVenta_SolicitadaPor
            FOREIGN KEY (SolicitadaPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT FK_DevolucionesVenta_AutorizadaPor
            FOREIGN KEY (AutorizadaPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT FK_DevolucionesVenta_AutorizacionesOperacion
            FOREIGN KEY (IdAutorizacion) REFERENCES sis.AutorizacionesOperacion(IdAutorizacion),
        CONSTRAINT CK_DevolucionesVenta_FormaReintegro
            CHECK (FormaReintegro IN (N'EFECTIVO', N'CREDITO_CLIENTE', N'OTRO')),
        CONSTRAINT CK_DevolucionesVenta_Monto CHECK (MontoReintegro >= 0),
        CONSTRAINT CK_DevolucionesVenta_Estado
            CHECK (Estado IN (N'SOLICITADA', N'APROBADA', N'RECHAZADA', N'APLICADA')),
        CONSTRAINT CK_DevolucionesVenta_Fechas
            CHECK (FechaAutorizacion IS NULL OR FechaAutorizacion >= FechaSolicitud)
    );
END;
GO

IF OBJECT_ID(N'ven.DevolucionDetalle', N'U') IS NULL
BEGIN
    CREATE TABLE ven.DevolucionDetalle
    (
        IdDetalle          BIGINT IDENTITY(1,1) NOT NULL,
        IdDevolucion       BIGINT NOT NULL,
        IdVentaDetalle     BIGINT NOT NULL,
        Cantidad           DECIMAL(18,4) NOT NULL,
        Condicion          NVARCHAR(30) NOT NULL,
        ReingresaInventario BIT NOT NULL CONSTRAINT DF_DevolucionDetalle_Reingresa DEFAULT (0),
        MotivoNoReingreso  NVARCHAR(500) NULL,
        MontoReintegrado   DECIMAL(18,2) NOT NULL,

        CONSTRAINT PK_DevolucionDetalle PRIMARY KEY CLUSTERED (IdDetalle),
        CONSTRAINT FK_DevolucionDetalle_DevolucionesVenta
            FOREIGN KEY (IdDevolucion) REFERENCES ven.DevolucionesVenta(IdDevolucion),
        CONSTRAINT FK_DevolucionDetalle_VentaDetalle
            FOREIGN KEY (IdVentaDetalle) REFERENCES ven.VentaDetalle(IdDetalle),
        CONSTRAINT CK_DevolucionDetalle_Cantidad CHECK (Cantidad > 0),
        CONSTRAINT CK_DevolucionDetalle_Condicion
            CHECK (Condicion IN (N'SELLADO', N'ABIERTO_DEFECTUOSO', N'DAÑADO', N'OTRO')),
        CONSTRAINT CK_DevolucionDetalle_MotivoNoReingreso
            CHECK (ReingresaInventario = 1 OR NULLIF(LTRIM(RTRIM(MotivoNoReingreso)), N'') IS NOT NULL),
        CONSTRAINT CK_DevolucionDetalle_Monto CHECK (MontoReintegrado >= 0)
    );
END;
GO

IF OBJECT_ID(N'ven.DevolucionEvidencias', N'U') IS NULL
BEGIN
    CREATE TABLE ven.DevolucionEvidencias
    (
        IdEvidencia          BIGINT IDENTITY(1,1) NOT NULL,
        IdDevolucionDetalle  BIGINT NOT NULL,
        NombreArchivo        NVARCHAR(255) NOT NULL,
        MimeType             NVARCHAR(100) NOT NULL,
        RutaRelativa         NVARCHAR(500) NOT NULL,
        HashSha256           CHAR(64) NULL,
        SubidaPor            INT NOT NULL,
        Fecha                DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_DevolucionEvidencias_Fecha DEFAULT (SYSDATETIMEOFFSET()),

        CONSTRAINT PK_DevolucionEvidencias PRIMARY KEY CLUSTERED (IdEvidencia),
        CONSTRAINT FK_DevolucionEvidencias_Detalle
            FOREIGN KEY (IdDevolucionDetalle) REFERENCES ven.DevolucionDetalle(IdDetalle),
        CONSTRAINT FK_DevolucionEvidencias_Usuarios
            FOREIGN KEY (SubidaPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_DevolucionEvidencias_Hash
            CHECK (HashSha256 IS NULL OR HashSha256 NOT LIKE '%[^0-9A-Fa-f]%')
    );
END;
GO

/* ==========================================================================
   8. PROMOCIONES
   ========================================================================== */

IF OBJECT_ID(N'ven.Promociones', N'U') IS NULL
BEGIN
    CREATE TABLE ven.Promociones
    (
        IdPromocion   BIGINT IDENTITY(1,1) NOT NULL,
        Nombre        NVARCHAR(150) NOT NULL,
        Descripcion   NVARCHAR(500) NULL,
        FechaInicio   DATETIMEOFFSET(0) NOT NULL,
        FechaFin      DATETIMEOFFSET(0) NOT NULL,
        Tipo          NVARCHAR(30) NOT NULL,
        Valor         DECIMAL(18,4) NULL,
        Prioridad     SMALLINT NOT NULL,
        Acumulable    BIT NOT NULL CONSTRAINT DF_Promociones_Acumulable DEFAULT (0),
        Activo        BIT NOT NULL CONSTRAINT DF_Promociones_Activo DEFAULT (1),

        CONSTRAINT PK_Promociones PRIMARY KEY CLUSTERED (IdPromocion),
        CONSTRAINT CK_Promociones_Fechas CHECK (FechaFin > FechaInicio),
        CONSTRAINT CK_Promociones_Tipo
            CHECK (Tipo IN (N'PORCENTAJE', N'PRECIO_FIJO', N'COMBO', N'OTRO')),
        CONSTRAINT CK_Promociones_Valor CHECK (Valor IS NULL OR Valor >= 0),
        CONSTRAINT CK_Promociones_Prioridad CHECK (Prioridad >= 0)
    );
END;
GO

IF OBJECT_ID(N'ven.PromocionDetalle', N'U') IS NULL
BEGIN
    CREATE TABLE ven.PromocionDetalle
    (
        IdPromocion          BIGINT NOT NULL,
        IdProducto           INT NOT NULL,
        CantidadMinima       DECIMAL(18,4) NULL,
        PrecioPromocional    DECIMAL(18,2) NULL,
        DescuentoPorcentaje  DECIMAL(9,4) NULL,

        CONSTRAINT PK_PromocionDetalle PRIMARY KEY CLUSTERED (IdPromocion, IdProducto),
        CONSTRAINT FK_PromocionDetalle_Promociones
            FOREIGN KEY (IdPromocion) REFERENCES ven.Promociones(IdPromocion),
        CONSTRAINT FK_PromocionDetalle_Productos
            FOREIGN KEY (IdProducto) REFERENCES inv.Productos(IdProducto),
        CONSTRAINT CK_PromocionDetalle_Cantidad CHECK (CantidadMinima IS NULL OR CantidadMinima > 0),
        CONSTRAINT CK_PromocionDetalle_Precio CHECK (PrecioPromocional IS NULL OR PrecioPromocional >= 0),
        CONSTRAINT CK_PromocionDetalle_Descuento
            CHECK (DescuentoPorcentaje IS NULL OR (DescuentoPorcentaje >= 0 AND DescuentoPorcentaje <= 100))
    );
END;
GO

/* ==========================================================================
   9. ÍNDICES
   ========================================================================== */

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'cli.Clientes') AND name = N'UX_Clientes_NIT')
    CREATE UNIQUE INDEX UX_Clientes_NIT ON cli.Clientes(NIT) WHERE NIT IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'cli.Clientes') AND name = N'UX_Clientes_NRC')
    CREATE UNIQUE INDEX UX_Clientes_NRC ON cli.Clientes(NRC) WHERE NRC IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'cli.Clientes') AND name = N'IX_Clientes_NombreRazonSocial')
    CREATE INDEX IX_Clientes_NombreRazonSocial ON cli.Clientes(NombreRazonSocial) INCLUDE (Codigo, NombreComercial, Telefono, WhatsApp, Email, Activo);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'cli.Clientes') AND name = N'IX_Clientes_Tipo_Activo')
    CREATE INDEX IX_Clientes_Tipo_Activo ON cli.Clientes(IdTipoCliente, Activo) INCLUDE (Codigo, NombreRazonSocial, PermiteCredito, BloqueadoCredito);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'cli.ClienteContactos') AND name = N'IX_ClienteContactos_Cliente')
    CREATE INDEX IX_ClienteContactos_Cliente ON cli.ClienteContactos(IdCliente, Activo) INCLUDE (Nombre, Cargo, Telefono, WhatsApp, Email, EsPrincipal);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'cli.ClienteDirecciones') AND name = N'IX_ClienteDirecciones_Cliente')
    CREATE INDEX IX_ClienteDirecciones_Cliente ON cli.ClienteDirecciones(IdCliente, Activo) INCLUDE (Tipo, Departamento, Municipio, EsPrincipal);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.TurnosCaja') AND name = N'UX_TurnosCaja_CajaAbierta')
    CREATE UNIQUE INDEX UX_TurnosCaja_CajaAbierta ON ven.TurnosCaja(IdCaja) WHERE Estado = N'ABIERTO';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.MovimientosCaja') AND name = N'IX_MovimientosCaja_Turno_Fecha')
    CREATE INDEX IX_MovimientosCaja_Turno_Fecha ON ven.MovimientosCaja(IdTurno, Fecha) INCLUDE (Tipo, Monto, ReferenciaTipo, ReferenciaId, IdUsuario);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.ListaPrecioProductos') AND name = N'IX_ListaPrecioProductos_Producto')
    CREATE INDEX IX_ListaPrecioProductos_Producto ON ven.ListaPrecioProductos(IdProducto, Activo, CantidadMinima) INCLUDE (IdListaPrecio, Precio);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.Cotizaciones') AND name = N'IX_Cotizaciones_Cliente_Fecha')
    CREATE INDEX IX_Cotizaciones_Cliente_Fecha ON ven.Cotizaciones(IdCliente, Fecha) INCLUDE (NumeroCotizacion, Estado, Total);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.CotizacionDetalle') AND name = N'IX_CotizacionDetalle_Cotizacion')
    CREATE INDEX IX_CotizacionDetalle_Cotizacion ON ven.CotizacionDetalle(IdCotizacion) INCLUDE (IdProducto, Cantidad, PrecioUnitario, TotalLinea);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.Ventas') AND name = N'IX_Ventas_Fecha')
    CREATE INDEX IX_Ventas_Fecha ON ven.Ventas(Fecha) INCLUDE (NumeroVenta, IdCliente, IdVendedor, Estado, Total, SaldoPendiente);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.Ventas') AND name = N'IX_Ventas_Cliente_Fecha')
    CREATE INDEX IX_Ventas_Cliente_Fecha ON ven.Ventas(IdCliente, Fecha) INCLUDE (NumeroVenta, Estado, Total, SaldoPendiente);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.Ventas') AND name = N'IX_Ventas_Vendedor_Fecha')
    CREATE INDEX IX_Ventas_Vendedor_Fecha ON ven.Ventas(IdVendedor, Fecha) INCLUDE (NumeroVenta, IdCliente, Estado, Total);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.Ventas') AND name = N'IX_Ventas_Estado')
    CREATE INDEX IX_Ventas_Estado ON ven.Ventas(Estado, Fecha) INCLUDE (NumeroVenta, IdCliente, Total, SaldoPendiente);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.VentaDetalle') AND name = N'IX_VentaDetalle_Producto')
    CREATE INDEX IX_VentaDetalle_Producto ON ven.VentaDetalle(IdProducto) INCLUDE (IdVenta, IdLote, Cantidad, PrecioUnitario, CostoUnitario, TotalLinea);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.VentaDetalle') AND name = N'IX_VentaDetalle_Venta')
    CREATE INDEX IX_VentaDetalle_Venta ON ven.VentaDetalle(IdVenta) INCLUDE (IdProducto, IdLote, Cantidad, PrecioUnitario, TotalLinea);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.VentaPagos') AND name = N'IX_VentaPagos_Venta')
    CREATE INDEX IX_VentaPagos_Venta ON ven.VentaPagos(IdVenta, Estado) INCLUDE (IdMetodoPago, Monto, Fecha, Referencia);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.CuentasPorCobrar') AND name = N'IX_CuentasPorCobrar_Cliente_Estado')
    CREATE INDEX IX_CuentasPorCobrar_Cliente_Estado ON ven.CuentasPorCobrar(IdCliente, Estado) INCLUDE (IdVenta, FechaVencimiento, MontoOriginal, Saldo, ProximoAviso);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.CuentasPorCobrar') AND name = N'IX_CuentasPorCobrar_Vencimiento_Estado')
    CREATE INDEX IX_CuentasPorCobrar_Vencimiento_Estado ON ven.CuentasPorCobrar(FechaVencimiento, Estado) INCLUDE (IdCliente, IdVenta, Saldo, ProximoAviso);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.AbonosCliente') AND name = N'IX_AbonosCliente_Cuenta_Fecha')
    CREATE INDEX IX_AbonosCliente_Cuenta_Fecha ON ven.AbonosCliente(IdCuenta, Fecha) INCLUDE (Monto, IdMetodoPago, NumeroRecibo, Anulado);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.DevolucionesVenta') AND name = N'IX_DevolucionesVenta_Venta')
    CREATE INDEX IX_DevolucionesVenta_Venta ON ven.DevolucionesVenta(IdVenta, Estado) INCLUDE (NumeroDevolucion, FechaSolicitud, MontoReintegro, FormaReintegro);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.DevolucionDetalle') AND name = N'IX_DevolucionDetalle_Devolucion')
    CREATE INDEX IX_DevolucionDetalle_Devolucion ON ven.DevolucionDetalle(IdDevolucion) INCLUDE (IdVentaDetalle, Cantidad, Condicion, ReingresaInventario, MontoReintegrado);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'ven.Promociones') AND name = N'IX_Promociones_Vigencia')
    CREATE INDEX IX_Promociones_Vigencia ON ven.Promociones(Activo, FechaInicio, FechaFin) INCLUDE (Tipo, Valor, Prioridad, Acumulable);
GO

/* ==========================================================================
   10. VALIDACIÓN FINAL DEL MÓDULO
   ========================================================================== */
DECLARE @TablasEsperadas TABLE (Nombre SYSNAME NOT NULL PRIMARY KEY);
INSERT INTO @TablasEsperadas (Nombre)
VALUES
    (N'cli.Clientes'),
    (N'cli.ClienteContactos'),
    (N'cli.ClienteDirecciones'),
    (N'ven.Cajas'),
    (N'ven.TurnosCaja'),
    (N'ven.MovimientosCaja'),
    (N'ven.ListasPrecio'),
    (N'ven.ListaPrecioProductos'),
    (N'ven.Cotizaciones'),
    (N'ven.CotizacionDetalle'),
    (N'ven.Ventas'),
    (N'ven.VentaDetalle'),
    (N'ven.VentaPagos'),
    (N'ven.CuentasPorCobrar'),
    (N'ven.AbonosCliente'),
    (N'ven.DevolucionesVenta'),
    (N'ven.DevolucionDetalle'),
    (N'ven.DevolucionEvidencias'),
    (N'ven.Promociones'),
    (N'ven.PromocionDetalle');

IF EXISTS
(
    SELECT 1
    FROM @TablasEsperadas t
    WHERE OBJECT_ID(t.Nombre, N'U') IS NULL
)
BEGIN
    SELECT t.Nombre AS TablaFaltante
    FROM @TablasEsperadas t
    WHERE OBJECT_ID(t.Nombre, N'U') IS NULL;

    THROW 50026, N'004_ClientesVentas.sql terminó con tablas faltantes.', 1;
END;

PRINT N'004_ClientesVentas.sql completado correctamente: 20 tablas de Clientes/Ventas/Caja/Crédito/Devoluciones/Promociones.';
GO
