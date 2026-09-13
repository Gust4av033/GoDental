/*
===============================================================================
 GoDental - 002_Inventario.sql
 Versión: 0.1
 Base documental: DM-01 v0.2 + DB-01 v0.1
 Motor: Microsoft SQL Server

 Dependencia:
   - Ejecutar previamente 001_Core.sql.

 Alcance:
   1) Almacenes.
   2) Maestro de productos.
   3) Códigos de barras.
   4) Impuestos por producto.
   5) Lotes y vencimientos.
   6) Existencias agregadas y por lote.
   7) Kardex de inventario.
   8) Conteos físicos y diferencias.

 Decisiones de diseño:
   - Sin triggers.
   - Costo: promedio ponderado móvil en inv.ExistenciasProducto.CostoPromedio.
   - Stock y costo se modificarán posteriormente mediante operaciones
     transaccionales/procedimientos/servicios, nunca con lógica oculta.
   - ROWVERSION protege puntos de concurrencia de productos y existencias.
   - IdRecepcionCompraDetalle se crea en inv.LotesProducto, pero su FK se
     agregará en 003_Compras.sql, cuando exista cmp.RecepcionCompraDetalle.
===============================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================================
   0. PREVALIDACIÓN
   ============================================================================ */
IF DB_ID(N'GoDental') IS NULL
BEGIN
    THROW 52000, 'GoDental no existe. Ejecute primero 001_Core.sql.', 1;
END;
GO

USE [GoDental];
GO

IF OBJECT_ID(N'cat.CategoriasProducto', N'U') IS NULL
   OR OBJECT_ID(N'cat.Marcas', N'U') IS NULL
   OR OBJECT_ID(N'cat.UnidadesMedida', N'U') IS NULL
   OR OBJECT_ID(N'cat.Impuestos', N'U') IS NULL
   OR OBJECT_ID(N'seg.Usuarios', N'U') IS NULL
BEGIN
    THROW 52001, 'Faltan dependencias de 001_Core.sql. Ejecute 001_Core.sql antes de 002_Inventario.sql.', 1;
END;
GO

/* ============================================================================
   1. ALMACENES
   ============================================================================ */

/* --------------------------------------------------------------------------
   inv.Almacenes
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'inv.Almacenes', N'U') IS NULL
BEGIN
    CREATE TABLE inv.Almacenes
    (
        IdAlmacen   INT IDENTITY(1,1) NOT NULL,
        Codigo      NVARCHAR(30) NOT NULL,
        Nombre      NVARCHAR(120) NOT NULL,
        Direccion   NVARCHAR(300) NULL,
        EsPrincipal BIT NOT NULL CONSTRAINT DF_Almacenes_EsPrincipal DEFAULT (0),
        Activo      BIT NOT NULL CONSTRAINT DF_Almacenes_Activo DEFAULT (1),

        CONSTRAINT PK_Almacenes PRIMARY KEY CLUSTERED (IdAlmacen),
        CONSTRAINT UQ_Almacenes_Codigo UNIQUE (Codigo)
    );
END;
GO

/* ============================================================================
   2. PRODUCTOS
   ============================================================================ */

/* --------------------------------------------------------------------------
   inv.Productos
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'inv.Productos', N'U') IS NULL
BEGIN
    CREATE TABLE inv.Productos
    (
        IdProducto              INT IDENTITY(1,1) NOT NULL,
        SKU                     NVARCHAR(60) NOT NULL,
        Nombre                  NVARCHAR(200) NOT NULL,
        Descripcion             NVARCHAR(1000) NULL,
        IdCategoria             INT NOT NULL,
        IdMarca                 INT NULL,
        IdUnidadMedida          SMALLINT NOT NULL,
        PrecioVentaReferencia   DECIMAL(18,2) NOT NULL
            CONSTRAINT DF_Productos_PrecioVentaReferencia DEFAULT (0),
        StockMinimo             DECIMAL(18,4) NOT NULL
            CONSTRAINT DF_Productos_StockMinimo DEFAULT (0),
        StockMaximo             DECIMAL(18,4) NULL,
        ControlaLote            BIT NOT NULL CONSTRAINT DF_Productos_ControlaLote DEFAULT (0),
        ControlaVencimiento     BIT NOT NULL CONSTRAINT DF_Productos_ControlaVencimiento DEFAULT (0),
        ControlaSerie           BIT NOT NULL CONSTRAINT DF_Productos_ControlaSerie DEFAULT (0),
        DiasAlertaVencimiento   SMALLINT NULL,
        Activo                  BIT NOT NULL CONSTRAINT DF_Productos_Activo DEFAULT (1),
        RowVersion              ROWVERSION NOT NULL,

        CONSTRAINT PK_Productos PRIMARY KEY CLUSTERED (IdProducto),
        CONSTRAINT UQ_Productos_SKU UNIQUE (SKU),

        CONSTRAINT FK_Productos_Categorias
            FOREIGN KEY (IdCategoria) REFERENCES cat.CategoriasProducto(IdCategoria),
        CONSTRAINT FK_Productos_Marcas
            FOREIGN KEY (IdMarca) REFERENCES cat.Marcas(IdMarca),
        CONSTRAINT FK_Productos_UnidadesMedida
            FOREIGN KEY (IdUnidadMedida) REFERENCES cat.UnidadesMedida(IdUnidadMedida),

        CONSTRAINT CK_Productos_PrecioNoNegativo
            CHECK (PrecioVentaReferencia >= 0),
        CONSTRAINT CK_Productos_StockMinimoNoNegativo
            CHECK (StockMinimo >= 0),
        CONSTRAINT CK_Productos_StockMaximoValido
            CHECK (StockMaximo IS NULL OR StockMaximo >= StockMinimo),
        CONSTRAINT CK_Productos_VencimientoRequiereLote
            CHECK (ControlaVencimiento = 0 OR ControlaLote = 1),
        CONSTRAINT CK_Productos_DiasAlertaVencimiento
            CHECK (DiasAlertaVencimiento IS NULL OR DiasAlertaVencimiento >= 0)
    );
END;
GO

/* --------------------------------------------------------------------------
   inv.ProductoCodigosBarra
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'inv.ProductoCodigosBarra', N'U') IS NULL
BEGIN
    CREATE TABLE inv.ProductoCodigosBarra
    (
        IdCodigoBarra       BIGINT IDENTITY(1,1) NOT NULL,
        IdProducto          INT NOT NULL,
        Codigo              NVARCHAR(80) NOT NULL,
        Tipo                NVARCHAR(20) NOT NULL,
        EsPrincipal         BIT NOT NULL CONSTRAINT DF_ProductoCodigosBarra_EsPrincipal DEFAULT (0),
        GeneradoInternamente BIT NOT NULL CONSTRAINT DF_ProductoCodigosBarra_Generado DEFAULT (0),
        Activo              BIT NOT NULL CONSTRAINT DF_ProductoCodigosBarra_Activo DEFAULT (1),

        CONSTRAINT PK_ProductoCodigosBarra PRIMARY KEY CLUSTERED (IdCodigoBarra),
        CONSTRAINT UQ_ProductoCodigosBarra_Codigo UNIQUE (Codigo),
        CONSTRAINT FK_ProductoCodigosBarra_Productos
            FOREIGN KEY (IdProducto) REFERENCES inv.Productos(IdProducto),
        CONSTRAINT CK_ProductoCodigosBarra_Tipo
            CHECK (Tipo IN (N'CODE128', N'EAN13', N'UPC', N'OTRO'))
    );
END;
GO

/* --------------------------------------------------------------------------
   inv.ProductoImpuestos
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'inv.ProductoImpuestos', N'U') IS NULL
BEGIN
    CREATE TABLE inv.ProductoImpuestos
    (
        IdProducto INT NOT NULL,
        IdImpuesto SMALLINT NOT NULL,
        Activo     BIT NOT NULL CONSTRAINT DF_ProductoImpuestos_Activo DEFAULT (1),

        CONSTRAINT PK_ProductoImpuestos PRIMARY KEY CLUSTERED (IdProducto, IdImpuesto),
        CONSTRAINT FK_ProductoImpuestos_Productos
            FOREIGN KEY (IdProducto) REFERENCES inv.Productos(IdProducto),
        CONSTRAINT FK_ProductoImpuestos_Impuestos
            FOREIGN KEY (IdImpuesto) REFERENCES cat.Impuestos(IdImpuesto)
    );
END;
GO

/* ============================================================================
   3. LOTES Y EXISTENCIAS
   ============================================================================ */

/* --------------------------------------------------------------------------
   inv.LotesProducto
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'inv.LotesProducto', N'U') IS NULL
BEGIN
    CREATE TABLE inv.LotesProducto
    (
        IdLote                    BIGINT IDENTITY(1,1) NOT NULL,
        IdProducto                INT NOT NULL,
        NumeroLote                NVARCHAR(100) NOT NULL,
        FechaFabricacion          DATE NULL,
        FechaVencimiento          DATE NULL,
        CostoUnitarioIngreso      DECIMAL(18,6) NOT NULL,
        IdRecepcionCompraDetalle  BIGINT NULL,
        Activo                    BIT NOT NULL CONSTRAINT DF_LotesProducto_Activo DEFAULT (1),

        CONSTRAINT PK_LotesProducto PRIMARY KEY CLUSTERED (IdLote),
        CONSTRAINT FK_LotesProducto_Productos
            FOREIGN KEY (IdProducto) REFERENCES inv.Productos(IdProducto),
        CONSTRAINT UQ_LotesProducto_ProductoLoteRecepcion
            UNIQUE (IdProducto, NumeroLote, IdRecepcionCompraDetalle),
        CONSTRAINT CK_LotesProducto_CostoNoNegativo
            CHECK (CostoUnitarioIngreso >= 0),
        CONSTRAINT CK_LotesProducto_FechasValidas
            CHECK (FechaFabricacion IS NULL OR FechaVencimiento IS NULL OR FechaFabricacion <= FechaVencimiento)
    );
END;
GO

/* --------------------------------------------------------------------------
   inv.ExistenciasProducto
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'inv.ExistenciasProducto', N'U') IS NULL
BEGIN
    CREATE TABLE inv.ExistenciasProducto
    (
        IdAlmacen             INT NOT NULL,
        IdProducto            INT NOT NULL,
        StockActual           DECIMAL(18,4) NOT NULL
            CONSTRAINT DF_ExistenciasProducto_StockActual DEFAULT (0),
        StockReservado        DECIMAL(18,4) NOT NULL
            CONSTRAINT DF_ExistenciasProducto_StockReservado DEFAULT (0),
        CostoPromedio         DECIMAL(18,6) NOT NULL
            CONSTRAINT DF_ExistenciasProducto_CostoPromedio DEFAULT (0),
        FechaUltimoMovimiento DATETIMEOFFSET(0) NULL,
        RowVersion            ROWVERSION NOT NULL,

        CONSTRAINT PK_ExistenciasProducto PRIMARY KEY CLUSTERED (IdAlmacen, IdProducto),
        CONSTRAINT FK_ExistenciasProducto_Almacenes
            FOREIGN KEY (IdAlmacen) REFERENCES inv.Almacenes(IdAlmacen),
        CONSTRAINT FK_ExistenciasProducto_Productos
            FOREIGN KEY (IdProducto) REFERENCES inv.Productos(IdProducto),
        CONSTRAINT CK_ExistenciasProducto_StockActual
            CHECK (StockActual >= 0),
        CONSTRAINT CK_ExistenciasProducto_StockReservado
            CHECK (StockReservado >= 0 AND StockReservado <= StockActual),
        CONSTRAINT CK_ExistenciasProducto_CostoPromedio
            CHECK (CostoPromedio >= 0)
    );
END;
GO

/* --------------------------------------------------------------------------
   inv.ExistenciasLote
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'inv.ExistenciasLote', N'U') IS NULL
BEGIN
    CREATE TABLE inv.ExistenciasLote
    (
        IdAlmacen  INT NOT NULL,
        IdLote     BIGINT NOT NULL,
        StockActual DECIMAL(18,4) NOT NULL
            CONSTRAINT DF_ExistenciasLote_StockActual DEFAULT (0),
        RowVersion ROWVERSION NOT NULL,

        CONSTRAINT PK_ExistenciasLote PRIMARY KEY CLUSTERED (IdAlmacen, IdLote),
        CONSTRAINT FK_ExistenciasLote_Almacenes
            FOREIGN KEY (IdAlmacen) REFERENCES inv.Almacenes(IdAlmacen),
        CONSTRAINT FK_ExistenciasLote_Lotes
            FOREIGN KEY (IdLote) REFERENCES inv.LotesProducto(IdLote),
        CONSTRAINT CK_ExistenciasLote_StockActual
            CHECK (StockActual >= 0)
    );
END;
GO

/* ============================================================================
   4. KARDEX
   ============================================================================ */

/* --------------------------------------------------------------------------
   inv.MovimientosInventario
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'inv.MovimientosInventario', N'U') IS NULL
BEGIN
    CREATE TABLE inv.MovimientosInventario
    (
        IdMovimiento       BIGINT IDENTITY(1,1) NOT NULL,
        Fecha              DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_MovimientosInventario_Fecha DEFAULT (SYSDATETIMEOFFSET()),
        IdAlmacen          INT NOT NULL,
        IdProducto         INT NOT NULL,
        IdLote             BIGINT NULL,
        TipoMovimiento     NVARCHAR(30) NOT NULL,
        Cantidad           DECIMAL(18,4) NOT NULL,
        CostoUnitario      DECIMAL(18,6) NULL,
        ExistenciaAnterior DECIMAL(18,4) NOT NULL,
        ExistenciaPosterior DECIMAL(18,4) NOT NULL,
        ReferenciaTipo     NVARCHAR(40) NULL,
        ReferenciaId       BIGINT NULL,
        IdUsuario          INT NOT NULL,
        Observacion        NVARCHAR(500) NULL,

        CONSTRAINT PK_MovimientosInventario PRIMARY KEY CLUSTERED (IdMovimiento),
        CONSTRAINT FK_MovimientosInventario_Almacenes
            FOREIGN KEY (IdAlmacen) REFERENCES inv.Almacenes(IdAlmacen),
        CONSTRAINT FK_MovimientosInventario_Productos
            FOREIGN KEY (IdProducto) REFERENCES inv.Productos(IdProducto),
        CONSTRAINT FK_MovimientosInventario_Lotes
            FOREIGN KEY (IdLote) REFERENCES inv.LotesProducto(IdLote),
        CONSTRAINT FK_MovimientosInventario_Usuarios
            FOREIGN KEY (IdUsuario) REFERENCES seg.Usuarios(IdUsuario),

        CONSTRAINT CK_MovimientosInventario_Tipo
            CHECK (TipoMovimiento IN
                (N'COMPRA', N'VENTA', N'DEVOLUCION', N'AJUSTE', N'VENCIMIENTO', N'DAÑO', N'TRANSFERENCIA')),
        CONSTRAINT CK_MovimientosInventario_Cantidad
            CHECK (Cantidad <> 0),
        CONSTRAINT CK_MovimientosInventario_Costo
            CHECK (CostoUnitario IS NULL OR CostoUnitario >= 0),
        CONSTRAINT CK_MovimientosInventario_Existencias
            CHECK (ExistenciaAnterior >= 0 AND ExistenciaPosterior >= 0),
        CONSTRAINT CK_MovimientosInventario_Cuadre
            CHECK (ExistenciaPosterior = ExistenciaAnterior + Cantidad),
        CONSTRAINT CK_MovimientosInventario_Referencia
            CHECK ((ReferenciaTipo IS NULL AND ReferenciaId IS NULL)
                OR (ReferenciaTipo IS NOT NULL AND ReferenciaId IS NOT NULL))
    );
END;
GO

/* ============================================================================
   5. CONTEOS FÍSICOS
   ============================================================================ */

/* --------------------------------------------------------------------------
   inv.ConteosInventario
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'inv.ConteosInventario', N'U') IS NULL
BEGIN
    CREATE TABLE inv.ConteosInventario
    (
        IdConteo      BIGINT IDENTITY(1,1) NOT NULL,
        IdAlmacen     INT NOT NULL,
        FechaInicio   DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_ConteosInventario_FechaInicio DEFAULT (SYSDATETIMEOFFSET()),
        FechaCierre   DATETIMEOFFSET(0) NULL,
        Estado        NVARCHAR(20) NOT NULL
            CONSTRAINT DF_ConteosInventario_Estado DEFAULT (N'ABIERTO'),
        CreadoPor     INT NOT NULL,
        CerradoPor    INT NULL,
        Observaciones NVARCHAR(500) NULL,

        CONSTRAINT PK_ConteosInventario PRIMARY KEY CLUSTERED (IdConteo),
        CONSTRAINT FK_ConteosInventario_Almacenes
            FOREIGN KEY (IdAlmacen) REFERENCES inv.Almacenes(IdAlmacen),
        CONSTRAINT FK_ConteosInventario_CreadoPor
            FOREIGN KEY (CreadoPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT FK_ConteosInventario_CerradoPor
            FOREIGN KEY (CerradoPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_ConteosInventario_Estado
            CHECK (Estado IN (N'ABIERTO', N'CERRADO', N'CANCELADO')),
        CONSTRAINT CK_ConteosInventario_Fechas
            CHECK (FechaCierre IS NULL OR FechaCierre >= FechaInicio),
        CONSTRAINT CK_ConteosInventario_Cierre
            CHECK (
                (Estado = N'ABIERTO' AND FechaCierre IS NULL AND CerradoPor IS NULL)
                OR
                (Estado IN (N'CERRADO', N'CANCELADO') AND FechaCierre IS NOT NULL AND CerradoPor IS NOT NULL)
            )
    );
END;
GO

/* --------------------------------------------------------------------------
   inv.ConteoInventarioDetalle
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'inv.ConteoInventarioDetalle', N'U') IS NULL
BEGIN
    CREATE TABLE inv.ConteoInventarioDetalle
    (
        IdDetalle        BIGINT IDENTITY(1,1) NOT NULL,
        IdConteo         BIGINT NOT NULL,
        IdProducto       INT NOT NULL,
        IdLote           BIGINT NULL,
        CantidadSistema  DECIMAL(18,4) NOT NULL,
        CantidadContada  DECIMAL(18,4) NOT NULL,
        Diferencia       DECIMAL(18,4) NOT NULL,
        AjusteGenerado   BIT NOT NULL
            CONSTRAINT DF_ConteoInventarioDetalle_Ajuste DEFAULT (0),

        CONSTRAINT PK_ConteoInventarioDetalle PRIMARY KEY CLUSTERED (IdDetalle),
        CONSTRAINT FK_ConteoInventarioDetalle_Conteos
            FOREIGN KEY (IdConteo) REFERENCES inv.ConteosInventario(IdConteo),
        CONSTRAINT FK_ConteoInventarioDetalle_Productos
            FOREIGN KEY (IdProducto) REFERENCES inv.Productos(IdProducto),
        CONSTRAINT FK_ConteoInventarioDetalle_Lotes
            FOREIGN KEY (IdLote) REFERENCES inv.LotesProducto(IdLote),
        CONSTRAINT UQ_ConteoInventarioDetalle_Linea
            UNIQUE (IdConteo, IdProducto, IdLote),
        CONSTRAINT CK_ConteoInventarioDetalle_Cantidades
            CHECK (CantidadSistema >= 0 AND CantidadContada >= 0),
        CONSTRAINT CK_ConteoInventarioDetalle_Diferencia
            CHECK (Diferencia = CantidadContada - CantidadSistema)
    );
END;
GO

/* ============================================================================
   6. ÍNDICES
   ============================================================================ */

-- Sólo un almacén activo puede marcarse como principal.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'inv.Almacenes')
      AND name = N'UX_Almacenes_UnPrincipalActivo'
)
    CREATE UNIQUE INDEX UX_Almacenes_UnPrincipalActivo
        ON inv.Almacenes(EsPrincipal)
        WHERE EsPrincipal = 1 AND Activo = 1;
GO

-- Productos
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.Productos') AND name = N'IX_Productos_Nombre')
    CREATE INDEX IX_Productos_Nombre ON inv.Productos(Nombre);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.Productos') AND name = N'IX_Productos_Categoria_Activo')
    CREATE INDEX IX_Productos_Categoria_Activo ON inv.Productos(IdCategoria, Activo) INCLUDE (SKU, Nombre, PrecioVentaReferencia);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.Productos') AND name = N'IX_Productos_Marca')
    CREATE INDEX IX_Productos_Marca ON inv.Productos(IdMarca) WHERE IdMarca IS NOT NULL;
GO

-- Códigos de barras
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.ProductoCodigosBarra') AND name = N'IX_ProductoCodigosBarra_Producto_Principal')
    CREATE INDEX IX_ProductoCodigosBarra_Producto_Principal
        ON inv.ProductoCodigosBarra(IdProducto, EsPrincipal) INCLUDE (Codigo, Tipo, Activo);
GO

-- Un solo código principal activo por producto.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.ProductoCodigosBarra') AND name = N'UX_ProductoCodigosBarra_PrincipalActivo')
    CREATE UNIQUE INDEX UX_ProductoCodigosBarra_PrincipalActivo
        ON inv.ProductoCodigosBarra(IdProducto)
        WHERE EsPrincipal = 1 AND Activo = 1;
GO

-- Lotes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.LotesProducto') AND name = N'IX_LotesProducto_FechaVencimiento')
    CREATE INDEX IX_LotesProducto_FechaVencimiento
        ON inv.LotesProducto(FechaVencimiento) INCLUDE (IdProducto, NumeroLote, Activo)
        WHERE FechaVencimiento IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.LotesProducto') AND name = N'IX_LotesProducto_Producto')
    CREATE INDEX IX_LotesProducto_Producto ON inv.LotesProducto(IdProducto, Activo);
GO

-- Existencias
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.ExistenciasProducto') AND name = N'IX_ExistenciasProducto_Producto')
    CREATE INDEX IX_ExistenciasProducto_Producto
        ON inv.ExistenciasProducto(IdProducto) INCLUDE (IdAlmacen, StockActual, StockReservado, CostoPromedio);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.ExistenciasLote') AND name = N'IX_ExistenciasLote_Lote')
    CREATE INDEX IX_ExistenciasLote_Lote
        ON inv.ExistenciasLote(IdLote) INCLUDE (IdAlmacen, StockActual);
GO

-- Kardex
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.MovimientosInventario') AND name = N'IX_MovimientosInventario_Producto_Fecha')
    CREATE INDEX IX_MovimientosInventario_Producto_Fecha
        ON inv.MovimientosInventario(IdProducto, Fecha DESC)
        INCLUDE (IdAlmacen, IdLote, TipoMovimiento, Cantidad, CostoUnitario, ExistenciaPosterior);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.MovimientosInventario') AND name = N'IX_MovimientosInventario_Referencia')
    CREATE INDEX IX_MovimientosInventario_Referencia
        ON inv.MovimientosInventario(ReferenciaTipo, ReferenciaId)
        WHERE ReferenciaTipo IS NOT NULL AND ReferenciaId IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.MovimientosInventario') AND name = N'IX_MovimientosInventario_Almacen_Fecha')
    CREATE INDEX IX_MovimientosInventario_Almacen_Fecha
        ON inv.MovimientosInventario(IdAlmacen, Fecha DESC)
        INCLUDE (IdProducto, TipoMovimiento, Cantidad);
GO

-- Conteos
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.ConteosInventario') AND name = N'IX_ConteosInventario_Almacen_Estado')
    CREATE INDEX IX_ConteosInventario_Almacen_Estado
        ON inv.ConteosInventario(IdAlmacen, Estado, FechaInicio DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'inv.ConteoInventarioDetalle') AND name = N'IX_ConteoInventarioDetalle_Conteo')
    CREATE INDEX IX_ConteoInventarioDetalle_Conteo
        ON inv.ConteoInventarioDetalle(IdConteo) INCLUDE (IdProducto, IdLote, Diferencia, AjusteGenerado);
GO

/* ============================================================================
   7. VALIDACIÓN BÁSICA
   ============================================================================ */
DECLARE @TablasInventarioEsperadas INT = 10;
DECLARE @TablasInventarioEncontradas INT;

SELECT @TablasInventarioEncontradas = COUNT(*)
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = N'inv'
  AND t.name IN
  (
      N'Almacenes',
      N'Productos',
      N'ProductoCodigosBarra',
      N'ProductoImpuestos',
      N'LotesProducto',
      N'ExistenciasProducto',
      N'ExistenciasLote',
      N'MovimientosInventario',
      N'ConteosInventario',
      N'ConteoInventarioDetalle'
  );

IF @TablasInventarioEncontradas <> @TablasInventarioEsperadas
BEGIN
    THROW 52002, 'Validación de 002_Inventario.sql falló: no se encontraron todas las tablas esperadas.', 1;
END;

PRINT N'002_Inventario.sql completado correctamente.';
PRINT N'Tablas de inventario encontradas: ' + CONVERT(NVARCHAR(10), @TablasInventarioEncontradas);
PRINT N'Nota: la FK LotesProducto -> RecepcionCompraDetalle se agregará en 003_Compras.sql.';
GO
