/*
===============================================================================
 GoDental - 003_Compras.sql
 Versión: 0.1
 Base documental: DM-01 v0.2 + DB-01 v0.1
 Motor: Microsoft SQL Server

 Requiere:
   - 001_Core.sql
   - 002_Inventario.sql

 Alcance:
   1) Proveedores y contactos.
   2) Relación producto-proveedor.
   3) Órdenes de compra y detalle.
   4) Seguimiento de importaciones (DHL/manual/otro).
   5) Gastos de importación y costo puesto en bodega.
   6) Recepciones parciales/totales de compra.
   7) Completar FK de inv.LotesProducto hacia recepción de compra.

 Decisiones de diseño:
   - No se utilizan triggers.
   - La aplicación/procedimientos transaccionales serán responsables de:
       * Aplicar recepciones.
       * Actualizar CantidadRecibida de la OC.
       * Calcular/prorratear gastos de importación.
       * Actualizar costo promedio de inventario.
       * Generar movimientos de inventario.
   - Este script crea estructura, integridad e índices; no ejecuta lógica de negocio.
===============================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ==========================================================================
   0. VALIDACIONES DE DEPENDENCIAS
   ========================================================================== */
IF DB_ID(N'GoDental') IS NULL
    THROW 50001, N'La base de datos GoDental no existe. Ejecute primero 001_Core.sql.', 1;
GO

USE [GoDental];
GO

IF OBJECT_ID(N'seg.Usuarios', N'U') IS NULL
    THROW 50002, N'Falta seg.Usuarios. Ejecute primero 001_Core.sql.', 1;
IF OBJECT_ID(N'cat.Monedas', N'U') IS NULL
    THROW 50003, N'Falta cat.Monedas. Ejecute primero 001_Core.sql.', 1;
IF OBJECT_ID(N'inv.Productos', N'U') IS NULL
    THROW 50004, N'Falta inv.Productos. Ejecute primero 002_Inventario.sql.', 1;
IF OBJECT_ID(N'inv.Almacenes', N'U') IS NULL
    THROW 50005, N'Falta inv.Almacenes. Ejecute primero 002_Inventario.sql.', 1;
IF OBJECT_ID(N'inv.LotesProducto', N'U') IS NULL
    THROW 50006, N'Falta inv.LotesProducto. Ejecute primero 002_Inventario.sql.', 1;
GO

/* ==========================================================================
   1. PROVEEDORES
   ========================================================================== */

/* --------------------------------------------------------------------------
   cmp.Proveedores
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cmp.Proveedores', N'U') IS NULL
BEGIN
    CREATE TABLE cmp.Proveedores
    (
        IdProveedor INT IDENTITY(1,1) NOT NULL,
        Codigo      NVARCHAR(30) NOT NULL,
        Nombre      NVARCHAR(200) NOT NULL,
        Pais        NVARCHAR(80) NULL,
        Direccion   NVARCHAR(400) NULL,
        Email       NVARCHAR(254) NULL,
        Telefono    NVARCHAR(40) NULL,
        SitioWeb    NVARCHAR(300) NULL,
        Activo      BIT NOT NULL CONSTRAINT DF_Proveedores_Activo DEFAULT (1),
        RowVersion  ROWVERSION NOT NULL,

        CONSTRAINT PK_Proveedores PRIMARY KEY CLUSTERED (IdProveedor),
        CONSTRAINT UQ_Proveedores_Codigo UNIQUE (Codigo)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.Proveedores')
      AND name = N'IX_Proveedores_Nombre'
)
BEGIN
    CREATE INDEX IX_Proveedores_Nombre
        ON cmp.Proveedores (Nombre)
        INCLUDE (Codigo, Pais, Activo);
END;
GO

/* --------------------------------------------------------------------------
   cmp.ProveedorContactos
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cmp.ProveedorContactos', N'U') IS NULL
BEGIN
    CREATE TABLE cmp.ProveedorContactos
    (
        IdContacto  INT IDENTITY(1,1) NOT NULL,
        IdProveedor INT NOT NULL,
        Nombre      NVARCHAR(150) NOT NULL,
        Cargo       NVARCHAR(100) NULL,
        Email       NVARCHAR(254) NULL,
        Telefono    NVARCHAR(40) NULL,
        EsPrincipal BIT NOT NULL CONSTRAINT DF_ProveedorContactos_EsPrincipal DEFAULT (0),
        Activo      BIT NOT NULL CONSTRAINT DF_ProveedorContactos_Activo DEFAULT (1),

        CONSTRAINT PK_ProveedorContactos PRIMARY KEY CLUSTERED (IdContacto),
        CONSTRAINT FK_ProveedorContactos_Proveedores
            FOREIGN KEY (IdProveedor) REFERENCES cmp.Proveedores(IdProveedor)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.ProveedorContactos')
      AND name = N'IX_ProveedorContactos_Proveedor'
)
BEGIN
    CREATE INDEX IX_ProveedorContactos_Proveedor
        ON cmp.ProveedorContactos (IdProveedor, Activo)
        INCLUDE (Nombre, Cargo, Email, Telefono, EsPrincipal);
END;
GO

/* --------------------------------------------------------------------------
   cmp.ProveedorProductos
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cmp.ProveedorProductos', N'U') IS NULL
BEGIN
    CREATE TABLE cmp.ProveedorProductos
    (
        IdProveedor   INT NOT NULL,
        IdProducto    INT NOT NULL,
        SkuProveedor  NVARCHAR(100) NULL,
        CostoUltimo   DECIMAL(18,6) NULL,
        IdMoneda      SMALLINT NULL,
        CantidadMinima DECIMAL(18,4) NULL,
        LeadTimeDias  SMALLINT NULL,
        EsPreferido   BIT NOT NULL CONSTRAINT DF_ProveedorProductos_EsPreferido DEFAULT (0),
        Activo        BIT NOT NULL CONSTRAINT DF_ProveedorProductos_Activo DEFAULT (1),

        CONSTRAINT PK_ProveedorProductos PRIMARY KEY CLUSTERED (IdProveedor, IdProducto),
        CONSTRAINT FK_ProveedorProductos_Proveedores
            FOREIGN KEY (IdProveedor) REFERENCES cmp.Proveedores(IdProveedor),
        CONSTRAINT FK_ProveedorProductos_Productos
            FOREIGN KEY (IdProducto) REFERENCES inv.Productos(IdProducto),
        CONSTRAINT FK_ProveedorProductos_Monedas
            FOREIGN KEY (IdMoneda) REFERENCES cat.Monedas(IdMoneda),
        CONSTRAINT CK_ProveedorProductos_CostoUltimo
            CHECK (CostoUltimo IS NULL OR CostoUltimo >= 0),
        CONSTRAINT CK_ProveedorProductos_CantidadMinima
            CHECK (CantidadMinima IS NULL OR CantidadMinima > 0),
        CONSTRAINT CK_ProveedorProductos_LeadTimeDias
            CHECK (LeadTimeDias IS NULL OR LeadTimeDias >= 0)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.ProveedorProductos')
      AND name = N'IX_ProveedorProductos_Producto'
)
BEGIN
    CREATE INDEX IX_ProveedorProductos_Producto
        ON cmp.ProveedorProductos (IdProducto, Activo)
        INCLUDE (IdProveedor, SkuProveedor, CostoUltimo, IdMoneda, EsPreferido);
END;
GO

/* ==========================================================================
   2. ÓRDENES DE COMPRA
   ========================================================================== */

/* --------------------------------------------------------------------------
   cmp.OrdenesCompra
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cmp.OrdenesCompra', N'U') IS NULL
BEGIN
    CREATE TABLE cmp.OrdenesCompra
    (
        IdOrdenCompra       BIGINT IDENTITY(1,1) NOT NULL,
        NumeroOrden         NVARCHAR(40) NOT NULL,
        IdProveedor         INT NOT NULL,
        IdMoneda            SMALLINT NOT NULL,
        TipoCambioEstimado  DECIMAL(18,8) NULL,
        FechaOrden          DATE NOT NULL,
        Estado              NVARCHAR(30) NOT NULL
            CONSTRAINT DF_OrdenesCompra_Estado DEFAULT (N'BORRADOR'),
        Subtotal            DECIMAL(18,2) NOT NULL CONSTRAINT DF_OrdenesCompra_Subtotal DEFAULT (0),
        Total               DECIMAL(18,2) NOT NULL CONSTRAINT DF_OrdenesCompra_Total DEFAULT (0),
        CreadoPor           INT NOT NULL,
        Observaciones       NVARCHAR(1000) NULL,
        RowVersion          ROWVERSION NOT NULL,

        CONSTRAINT PK_OrdenesCompra PRIMARY KEY CLUSTERED (IdOrdenCompra),
        CONSTRAINT UQ_OrdenesCompra_NumeroOrden UNIQUE (NumeroOrden),
        CONSTRAINT FK_OrdenesCompra_Proveedores
            FOREIGN KEY (IdProveedor) REFERENCES cmp.Proveedores(IdProveedor),
        CONSTRAINT FK_OrdenesCompra_Monedas
            FOREIGN KEY (IdMoneda) REFERENCES cat.Monedas(IdMoneda),
        CONSTRAINT FK_OrdenesCompra_Usuarios
            FOREIGN KEY (CreadoPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_OrdenesCompra_Estado
            CHECK (Estado IN
            (
                N'BORRADOR', N'ENVIADA', N'CONFIRMADA', N'EN_PRODUCCION',
                N'DESPACHADA', N'RECIBIDA', N'CANCELADA'
            )),
        CONSTRAINT CK_OrdenesCompra_SubtotalNoNegativo CHECK (Subtotal >= 0),
        CONSTRAINT CK_OrdenesCompra_TotalNoNegativo CHECK (Total >= 0),
        CONSTRAINT CK_OrdenesCompra_TipoCambioEstimado
            CHECK (TipoCambioEstimado IS NULL OR TipoCambioEstimado > 0)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.OrdenesCompra')
      AND name = N'IX_OrdenesCompra_Proveedor_Fecha'
)
BEGIN
    CREATE INDEX IX_OrdenesCompra_Proveedor_Fecha
        ON cmp.OrdenesCompra (IdProveedor, FechaOrden DESC)
        INCLUDE (NumeroOrden, Estado, Total, IdMoneda);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.OrdenesCompra')
      AND name = N'IX_OrdenesCompra_Estado_Fecha'
)
BEGIN
    CREATE INDEX IX_OrdenesCompra_Estado_Fecha
        ON cmp.OrdenesCompra (Estado, FechaOrden DESC)
        INCLUDE (NumeroOrden, IdProveedor, Total);
END;
GO

/* --------------------------------------------------------------------------
   cmp.OrdenCompraDetalle
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cmp.OrdenCompraDetalle', N'U') IS NULL
BEGIN
    CREATE TABLE cmp.OrdenCompraDetalle
    (
        IdDetalle          BIGINT IDENTITY(1,1) NOT NULL,
        IdOrdenCompra      BIGINT NOT NULL,
        IdProducto         INT NOT NULL,
        Cantidad           DECIMAL(18,4) NOT NULL,
        CostoUnitario      DECIMAL(18,6) NOT NULL,
        Descuento          DECIMAL(18,2) NOT NULL CONSTRAINT DF_OrdenCompraDetalle_Descuento DEFAULT (0),
        TotalLinea         DECIMAL(18,2) NOT NULL,
        CantidadRecibida   DECIMAL(18,4) NOT NULL CONSTRAINT DF_OrdenCompraDetalle_CantidadRecibida DEFAULT (0),

        CONSTRAINT PK_OrdenCompraDetalle PRIMARY KEY CLUSTERED (IdDetalle),
        CONSTRAINT FK_OrdenCompraDetalle_OrdenesCompra
            FOREIGN KEY (IdOrdenCompra) REFERENCES cmp.OrdenesCompra(IdOrdenCompra),
        CONSTRAINT FK_OrdenCompraDetalle_Productos
            FOREIGN KEY (IdProducto) REFERENCES inv.Productos(IdProducto),
        CONSTRAINT CK_OrdenCompraDetalle_Cantidad CHECK (Cantidad > 0),
        CONSTRAINT CK_OrdenCompraDetalle_CostoUnitario CHECK (CostoUnitario >= 0),
        CONSTRAINT CK_OrdenCompraDetalle_Descuento CHECK (Descuento >= 0),
        CONSTRAINT CK_OrdenCompraDetalle_TotalLinea CHECK (TotalLinea >= 0),
        CONSTRAINT CK_OrdenCompraDetalle_CantidadRecibida
            CHECK (CantidadRecibida >= 0 AND CantidadRecibida <= Cantidad)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.OrdenCompraDetalle')
      AND name = N'IX_OrdenCompraDetalle_Orden'
)
BEGIN
    CREATE INDEX IX_OrdenCompraDetalle_Orden
        ON cmp.OrdenCompraDetalle (IdOrdenCompra)
        INCLUDE (IdProducto, Cantidad, CantidadRecibida, CostoUnitario, TotalLinea);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.OrdenCompraDetalle')
      AND name = N'IX_OrdenCompraDetalle_Producto'
)
BEGIN
    CREATE INDEX IX_OrdenCompraDetalle_Producto
        ON cmp.OrdenCompraDetalle (IdProducto)
        INCLUDE (IdOrdenCompra, Cantidad, CantidadRecibida, CostoUnitario);
END;
GO

/* ==========================================================================
   3. IMPORTACIONES
   ========================================================================== */

/* --------------------------------------------------------------------------
   cmp.Importaciones
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cmp.Importaciones', N'U') IS NULL
BEGIN
    CREATE TABLE cmp.Importaciones
    (
        IdImportacion   BIGINT IDENTITY(1,1) NOT NULL,
        IdOrdenCompra   BIGINT NOT NULL,
        Courier         NVARCHAR(80) NULL,
        NumeroGuia      NVARCHAR(120) NULL,
        FechaPedido     DATE NULL,
        FechaDespacho   DATE NULL,
        FechaEstimada   DATE NULL,
        FechaRecepcion  DATE NULL,
        Estado          NVARCHAR(30) NOT NULL
            CONSTRAINT DF_Importaciones_Estado DEFAULT (N'CONFIRMADA'),
        Observaciones   NVARCHAR(1000) NULL,
        RowVersion      ROWVERSION NOT NULL,

        CONSTRAINT PK_Importaciones PRIMARY KEY CLUSTERED (IdImportacion),
        CONSTRAINT UQ_Importaciones_OrdenCompra UNIQUE (IdOrdenCompra),
        CONSTRAINT FK_Importaciones_OrdenesCompra
            FOREIGN KEY (IdOrdenCompra) REFERENCES cmp.OrdenesCompra(IdOrdenCompra),
        CONSTRAINT CK_Importaciones_Estado
            CHECK (Estado IN
            (
                N'CONFIRMADA', N'EN_PRODUCCION', N'DESPACHADA', N'EN_TRANSITO',
                N'ADUANA', N'RECIBIDA_PARCIAL', N'RECIBIDA'
            )),
        CONSTRAINT CK_Importaciones_FechasDespacho
            CHECK (FechaPedido IS NULL OR FechaDespacho IS NULL OR FechaDespacho >= FechaPedido),
        CONSTRAINT CK_Importaciones_FechaRecepcion
            CHECK (FechaDespacho IS NULL OR FechaRecepcion IS NULL OR FechaRecepcion >= FechaDespacho)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.Importaciones')
      AND name = N'IX_Importaciones_NumeroGuia'
)
BEGIN
    CREATE INDEX IX_Importaciones_NumeroGuia
        ON cmp.Importaciones (NumeroGuia)
        WHERE NumeroGuia IS NOT NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.Importaciones')
      AND name = N'IX_Importaciones_Estado'
)
BEGIN
    CREATE INDEX IX_Importaciones_Estado
        ON cmp.Importaciones (Estado)
        INCLUDE (IdOrdenCompra, Courier, NumeroGuia, FechaEstimada, FechaRecepcion);
END;
GO

/* --------------------------------------------------------------------------
   cmp.EventosImportacion
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cmp.EventosImportacion', N'U') IS NULL
BEGIN
    CREATE TABLE cmp.EventosImportacion
    (
        IdEvento      BIGINT IDENTITY(1,1) NOT NULL,
        IdImportacion BIGINT NOT NULL,
        FechaEvento   DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_EventosImportacion_FechaEvento DEFAULT (SYSDATETIMEOFFSET()),
        TipoEvento    NVARCHAR(50) NOT NULL,
        Descripcion   NVARCHAR(500) NOT NULL,
        Fuente        NVARCHAR(30) NOT NULL CONSTRAINT DF_EventosImportacion_Fuente DEFAULT (N'MANUAL'),
        RegistradoPor INT NULL,

        CONSTRAINT PK_EventosImportacion PRIMARY KEY CLUSTERED (IdEvento),
        CONSTRAINT FK_EventosImportacion_Importaciones
            FOREIGN KEY (IdImportacion) REFERENCES cmp.Importaciones(IdImportacion),
        CONSTRAINT FK_EventosImportacion_Usuarios
            FOREIGN KEY (RegistradoPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_EventosImportacion_Fuente
            CHECK (Fuente IN (N'MANUAL', N'DHL', N'PROVEEDOR', N'OTRO'))
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.EventosImportacion')
      AND name = N'IX_EventosImportacion_Importacion_Fecha'
)
BEGIN
    CREATE INDEX IX_EventosImportacion_Importacion_Fecha
        ON cmp.EventosImportacion (IdImportacion, FechaEvento DESC)
        INCLUDE (TipoEvento, Fuente, Descripcion, RegistradoPor);
END;
GO

/* --------------------------------------------------------------------------
   cmp.GastosImportacion
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cmp.GastosImportacion', N'U') IS NULL
BEGIN
    CREATE TABLE cmp.GastosImportacion
    (
        IdGasto          BIGINT IDENTITY(1,1) NOT NULL,
        IdImportacion    BIGINT NOT NULL,
        TipoGasto        NVARCHAR(40) NOT NULL,
        Descripcion      NVARCHAR(250) NULL,
        Monto            DECIMAL(18,2) NOT NULL,
        IdMoneda         SMALLINT NOT NULL,
        TipoCambio       DECIMAL(18,8) NULL,
        MontoUSD         DECIMAL(18,2) NOT NULL,
        FechaGasto       DATE NULL,
        ComprobanteRuta  NVARCHAR(500) NULL,

        CONSTRAINT PK_GastosImportacion PRIMARY KEY CLUSTERED (IdGasto),
        CONSTRAINT FK_GastosImportacion_Importaciones
            FOREIGN KEY (IdImportacion) REFERENCES cmp.Importaciones(IdImportacion),
        CONSTRAINT FK_GastosImportacion_Monedas
            FOREIGN KEY (IdMoneda) REFERENCES cat.Monedas(IdMoneda),
        CONSTRAINT CK_GastosImportacion_TipoGasto
            CHECK (TipoGasto IN (N'FLETE', N'IMPUESTO', N'ADUANA', N'MANEJO', N'SEGURO', N'OTRO')),
        CONSTRAINT CK_GastosImportacion_Monto CHECK (Monto >= 0),
        CONSTRAINT CK_GastosImportacion_MontoUSD CHECK (MontoUSD >= 0),
        CONSTRAINT CK_GastosImportacion_TipoCambio
            CHECK (TipoCambio IS NULL OR TipoCambio > 0)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.GastosImportacion')
      AND name = N'IX_GastosImportacion_Importacion'
)
BEGIN
    CREATE INDEX IX_GastosImportacion_Importacion
        ON cmp.GastosImportacion (IdImportacion)
        INCLUDE (TipoGasto, MontoUSD, FechaGasto);
END;
GO

/* ==========================================================================
   4. RECEPCIONES DE COMPRA
   ========================================================================== */

/* --------------------------------------------------------------------------
   cmp.RecepcionesCompra
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cmp.RecepcionesCompra', N'U') IS NULL
BEGIN
    CREATE TABLE cmp.RecepcionesCompra
    (
        IdRecepcion      BIGINT IDENTITY(1,1) NOT NULL,
        NumeroRecepcion  NVARCHAR(40) NOT NULL,
        IdOrdenCompra    BIGINT NOT NULL,
        IdImportacion    BIGINT NULL,
        IdAlmacen        INT NOT NULL,
        FechaRecepcion   DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_RecepcionesCompra_FechaRecepcion DEFAULT (SYSDATETIMEOFFSET()),
        RecibidoPor      INT NOT NULL,
        Estado           NVARCHAR(20) NOT NULL
            CONSTRAINT DF_RecepcionesCompra_Estado DEFAULT (N'BORRADOR'),
        Observaciones    NVARCHAR(500) NULL,

        CONSTRAINT PK_RecepcionesCompra PRIMARY KEY CLUSTERED (IdRecepcion),
        CONSTRAINT UQ_RecepcionesCompra_NumeroRecepcion UNIQUE (NumeroRecepcion),
        CONSTRAINT FK_RecepcionesCompra_OrdenesCompra
            FOREIGN KEY (IdOrdenCompra) REFERENCES cmp.OrdenesCompra(IdOrdenCompra),
        CONSTRAINT FK_RecepcionesCompra_Importaciones
            FOREIGN KEY (IdImportacion) REFERENCES cmp.Importaciones(IdImportacion),
        CONSTRAINT FK_RecepcionesCompra_Almacenes
            FOREIGN KEY (IdAlmacen) REFERENCES inv.Almacenes(IdAlmacen),
        CONSTRAINT FK_RecepcionesCompra_Usuarios
            FOREIGN KEY (RecibidoPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_RecepcionesCompra_Estado
            CHECK (Estado IN (N'BORRADOR', N'APLICADA', N'ANULADA'))
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.RecepcionesCompra')
      AND name = N'IX_RecepcionesCompra_Orden_Fecha'
)
BEGIN
    CREATE INDEX IX_RecepcionesCompra_Orden_Fecha
        ON cmp.RecepcionesCompra (IdOrdenCompra, FechaRecepcion DESC)
        INCLUDE (NumeroRecepcion, IdImportacion, IdAlmacen, Estado, RecibidoPor);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.RecepcionesCompra')
      AND name = N'IX_RecepcionesCompra_Importacion'
)
BEGIN
    CREATE INDEX IX_RecepcionesCompra_Importacion
        ON cmp.RecepcionesCompra (IdImportacion)
        INCLUDE (IdOrdenCompra, NumeroRecepcion, FechaRecepcion, Estado)
        WHERE IdImportacion IS NOT NULL;
END;
GO

/* --------------------------------------------------------------------------
   cmp.RecepcionCompraDetalle
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cmp.RecepcionCompraDetalle', N'U') IS NULL
BEGIN
    CREATE TABLE cmp.RecepcionCompraDetalle
    (
        IdDetalle              BIGINT IDENTITY(1,1) NOT NULL,
        IdRecepcion            BIGINT NOT NULL,
        IdOrdenCompraDetalle   BIGINT NOT NULL,
        IdProducto             INT NOT NULL,
        CantidadRecibida       DECIMAL(18,4) NOT NULL,
        CostoCompraUSD         DECIMAL(18,6) NOT NULL,
        GastoAsignadoUSD       DECIMAL(18,6) NOT NULL CONSTRAINT DF_RecepcionCompraDetalle_GastoAsignado DEFAULT (0),
        CostoPuestoBodega      DECIMAL(18,6) NOT NULL,
        NumeroLote             NVARCHAR(100) NULL,
        FechaVencimiento       DATE NULL,

        CONSTRAINT PK_RecepcionCompraDetalle PRIMARY KEY CLUSTERED (IdDetalle),
        CONSTRAINT FK_RecepcionCompraDetalle_RecepcionesCompra
            FOREIGN KEY (IdRecepcion) REFERENCES cmp.RecepcionesCompra(IdRecepcion),
        CONSTRAINT FK_RecepcionCompraDetalle_OrdenCompraDetalle
            FOREIGN KEY (IdOrdenCompraDetalle) REFERENCES cmp.OrdenCompraDetalle(IdDetalle),
        CONSTRAINT FK_RecepcionCompraDetalle_Productos
            FOREIGN KEY (IdProducto) REFERENCES inv.Productos(IdProducto),
        CONSTRAINT CK_RecepcionCompraDetalle_Cantidad CHECK (CantidadRecibida > 0),
        CONSTRAINT CK_RecepcionCompraDetalle_CostoCompra CHECK (CostoCompraUSD >= 0),
        CONSTRAINT CK_RecepcionCompraDetalle_GastoAsignado CHECK (GastoAsignadoUSD >= 0),
        CONSTRAINT CK_RecepcionCompraDetalle_CostoPuestoBodega CHECK (CostoPuestoBodega >= 0)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.RecepcionCompraDetalle')
      AND name = N'IX_RecepcionCompraDetalle_Recepcion'
)
BEGIN
    CREATE INDEX IX_RecepcionCompraDetalle_Recepcion
        ON cmp.RecepcionCompraDetalle (IdRecepcion)
        INCLUDE (IdProducto, CantidadRecibida, CostoCompraUSD, GastoAsignadoUSD, CostoPuestoBodega, NumeroLote, FechaVencimiento);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'cmp.RecepcionCompraDetalle')
      AND name = N'IX_RecepcionCompraDetalle_OrdenDetalle'
)
BEGIN
    CREATE INDEX IX_RecepcionCompraDetalle_OrdenDetalle
        ON cmp.RecepcionCompraDetalle (IdOrdenCompraDetalle)
        INCLUDE (IdRecepcion, IdProducto, CantidadRecibida);
END;
GO

/* ==========================================================================
   5. COMPLETAR TRAZABILIDAD DE LOTES DE INVENTARIO
   ========================================================================== */
IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'inv.LotesProducto')
      AND name = N'FK_LotesProducto_RecepcionCompraDetalle'
)
BEGIN
    ALTER TABLE inv.LotesProducto WITH CHECK
    ADD CONSTRAINT FK_LotesProducto_RecepcionCompraDetalle
        FOREIGN KEY (IdRecepcionCompraDetalle)
        REFERENCES cmp.RecepcionCompraDetalle(IdDetalle);

    ALTER TABLE inv.LotesProducto
        CHECK CONSTRAINT FK_LotesProducto_RecepcionCompraDetalle;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'inv.LotesProducto')
      AND name = N'IX_LotesProducto_RecepcionCompraDetalle'
)
BEGIN
    CREATE INDEX IX_LotesProducto_RecepcionCompraDetalle
        ON inv.LotesProducto (IdRecepcionCompraDetalle)
        WHERE IdRecepcionCompraDetalle IS NOT NULL;
END;
GO

/* ==========================================================================
   6. VALIDACIÓN FINAL DEL MÓDULO
   ========================================================================== */
DECLARE @TablasEsperadas TABLE (Nombre SYSNAME NOT NULL PRIMARY KEY);
INSERT INTO @TablasEsperadas (Nombre)
VALUES
    (N'cmp.Proveedores'),
    (N'cmp.ProveedorContactos'),
    (N'cmp.ProveedorProductos'),
    (N'cmp.OrdenesCompra'),
    (N'cmp.OrdenCompraDetalle'),
    (N'cmp.Importaciones'),
    (N'cmp.EventosImportacion'),
    (N'cmp.GastosImportacion'),
    (N'cmp.RecepcionesCompra'),
    (N'cmp.RecepcionCompraDetalle');

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

    THROW 50007, N'003_Compras.sql terminó con tablas faltantes.', 1;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'inv.LotesProducto')
      AND name = N'FK_LotesProducto_RecepcionCompraDetalle'
)
BEGIN
    THROW 50008, N'No se pudo crear la relación entre lotes y recepción de compra.', 1;
END;

PRINT N'003_Compras.sql completado correctamente: 10 tablas de Compras/Importaciones + FK de trazabilidad de lotes.';
GO
