/* ============================================================================
   GoDental - 005_Fiscal_Base.sql
   Módulo: Fiscal / DTE (cimientos)
   Versión de modelo: DB-01 v0.1

   OBJETIVO
   -------
   Crear la estructura base para asociar ventas de GoDental con documentos
   fiscales y dejar preparada una futura integración DTE, sin acoplar el
   núcleo comercial a una versión concreta de la plataforma de Hacienda.

   DEPENDENCIAS
   ------------
   001_Core.sql
   004_ClientesVentas.sql

   PRINCIPIOS
   ----------
   - Venta y Documento Fiscal son conceptos separados.
   - Los datos técnicos DTE viven únicamente en el esquema fis.
   - Credenciales/secretos no deben almacenarse en texto plano.
   - Este script NO implementa envío real a Hacienda.
   - Este script NO crea triggers.
   ============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ==========================================================================
   0. PREVALIDACIONES
   ========================================================================== */

IF DB_ID(N'GoDental') IS NULL
BEGIN
    THROW 51000, 'La base de datos GoDental no existe. Ejecute primero 001_Core.sql.', 1;
END;
GO

USE GoDental;
GO

IF SCHEMA_ID(N'fis') IS NULL
BEGIN
    THROW 51001, 'El esquema fis no existe. Ejecute primero 001_Core.sql.', 1;
END;
GO

IF OBJECT_ID(N'ven.Ventas', N'U') IS NULL
BEGIN
    THROW 51002, 'La tabla ven.Ventas no existe. Ejecute primero 004_ClientesVentas.sql.', 1;
END;
GO

/* ==========================================================================
   1. DOCUMENTOS FISCALES
   ========================================================================== */

IF OBJECT_ID(N'fis.DocumentosFiscales', N'U') IS NULL
BEGIN
    CREATE TABLE fis.DocumentosFiscales
    (
        IdDocumentoFiscal BIGINT IDENTITY(1,1) NOT NULL,
        IdVenta           BIGINT NOT NULL,
        TipoDocumento     NVARCHAR(30) NOT NULL,
        NumeroInterno     NVARCHAR(60) NULL,
        Estado            NVARCHAR(30) NOT NULL
            CONSTRAINT DF_DocumentosFiscales_Estado DEFAULT (N'PENDIENTE'),
        Fecha             DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_DocumentosFiscales_Fecha DEFAULT (SYSDATETIMEOFFSET()),

        CONSTRAINT PK_DocumentosFiscales
            PRIMARY KEY CLUSTERED (IdDocumentoFiscal),

        CONSTRAINT FK_DocumentosFiscales_Ventas
            FOREIGN KEY (IdVenta) REFERENCES ven.Ventas(IdVenta),

        CONSTRAINT CK_DocumentosFiscales_Estado
            CHECK (Estado IN (N'PENDIENTE', N'GENERADO', N'ANULADO', N'RECHAZADO'))
    );
END;
GO

/* ==========================================================================
   2. DOCUMENTOS DTE
   ========================================================================== */

IF OBJECT_ID(N'fis.DocumentosDte', N'U') IS NULL
BEGIN
    CREATE TABLE fis.DocumentosDte
    (
        IdDte              BIGINT IDENTITY(1,1) NOT NULL,
        IdDocumentoFiscal  BIGINT NOT NULL,
        TipoDte            NVARCHAR(10) NOT NULL,
        CodigoGeneracion   NVARCHAR(80) NULL,
        NumeroControl      NVARCHAR(80) NULL,
        SelloRecepcion     NVARCHAR(200) NULL,
        Estado             NVARCHAR(30) NOT NULL,
        JsonGenerado       NVARCHAR(MAX) NULL,
        JsonRespuesta      NVARCHAR(MAX) NULL,

        CONSTRAINT PK_DocumentosDte
            PRIMARY KEY CLUSTERED (IdDte),

        CONSTRAINT FK_DocumentosDte_DocumentosFiscales
            FOREIGN KEY (IdDocumentoFiscal)
            REFERENCES fis.DocumentosFiscales(IdDocumentoFiscal)
    );
END;
GO

/* ==========================================================================
   3. HISTORIAL TÉCNICO DTE
   ========================================================================== */

IF OBJECT_ID(N'fis.EventosDte', N'U') IS NULL
BEGIN
    CREATE TABLE fis.EventosDte
    (
        IdEventoDte     BIGINT IDENTITY(1,1) NOT NULL,
        IdDte           BIGINT NOT NULL,
        Fecha           DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_EventosDte_Fecha DEFAULT (SYSDATETIMEOFFSET()),
        TipoEvento      NVARCHAR(40) NOT NULL,
        CodigoRespuesta NVARCHAR(80) NULL,
        Detalle         NVARCHAR(1500) NULL,

        CONSTRAINT PK_EventosDte
            PRIMARY KEY CLUSTERED (IdEventoDte),

        CONSTRAINT FK_EventosDte_DocumentosDte
            FOREIGN KEY (IdDte) REFERENCES fis.DocumentosDte(IdDte),

        CONSTRAINT CK_EventosDte_TipoEvento
            CHECK (TipoEvento IN
            (
                N'GENERADO',
                N'ENVIADO',
                N'RECIBIDO',
                N'RECHAZADO',
                N'ANULADO',
                N'CONTINGENCIA'
            ))
    );
END;
GO

/* ==========================================================================
   4. CONFIGURACIÓN FISCAL
   ========================================================================== */

IF OBJECT_ID(N'fis.ConfiguracionFiscal', N'U') IS NULL
BEGIN
    CREATE TABLE fis.ConfiguracionFiscal
    (
        IdConfiguracionFiscal INT IDENTITY(1,1) NOT NULL,
        Ambiente              NVARCHAR(20) NOT NULL,
        Clave                 NVARCHAR(100) NOT NULL,
        ValorProtegido        NVARCHAR(MAX) NULL,
        Activo                BIT NOT NULL
            CONSTRAINT DF_ConfiguracionFiscal_Activo DEFAULT (1),

        CONSTRAINT PK_ConfiguracionFiscal
            PRIMARY KEY CLUSTERED (IdConfiguracionFiscal),

        CONSTRAINT UQ_ConfiguracionFiscal_Ambiente_Clave
            UNIQUE (Ambiente, Clave),

        CONSTRAINT CK_ConfiguracionFiscal_Ambiente
            CHECK (Ambiente IN (N'PRUEBAS', N'PRODUCCION'))
    );
END;
GO

/* ==========================================================================
   5. ÍNDICES
   ========================================================================== */

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'fis.DocumentosFiscales')
      AND name = N'IX_DocumentosFiscales_IdVenta'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_DocumentosFiscales_IdVenta
        ON fis.DocumentosFiscales (IdVenta);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'fis.DocumentosFiscales')
      AND name = N'IX_DocumentosFiscales_Fecha_Estado'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_DocumentosFiscales_Fecha_Estado
        ON fis.DocumentosFiscales (Fecha DESC, Estado)
        INCLUDE (IdVenta, TipoDocumento, NumeroInterno);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'fis.DocumentosDte')
      AND name = N'IX_DocumentosDte_IdDocumentoFiscal'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_DocumentosDte_IdDocumentoFiscal
        ON fis.DocumentosDte (IdDocumentoFiscal);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'fis.DocumentosDte')
      AND name = N'IX_DocumentosDte_Estado_TipoDte'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_DocumentosDte_Estado_TipoDte
        ON fis.DocumentosDte (Estado, TipoDte)
        INCLUDE (CodigoGeneracion, NumeroControl, SelloRecepcion);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'fis.DocumentosDte')
      AND name = N'UX_DocumentosDte_CodigoGeneracion'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_DocumentosDte_CodigoGeneracion
        ON fis.DocumentosDte (CodigoGeneracion)
        WHERE CodigoGeneracion IS NOT NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'fis.DocumentosDte')
      AND name = N'UX_DocumentosDte_NumeroControl'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_DocumentosDte_NumeroControl
        ON fis.DocumentosDte (NumeroControl)
        WHERE NumeroControl IS NOT NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'fis.EventosDte')
      AND name = N'IX_EventosDte_IdDte_Fecha'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_EventosDte_IdDte_Fecha
        ON fis.EventosDte (IdDte, Fecha DESC)
        INCLUDE (TipoEvento, CodigoRespuesta);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'fis.ConfiguracionFiscal')
      AND name = N'IX_ConfiguracionFiscal_Ambiente_Activo'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_ConfiguracionFiscal_Ambiente_Activo
        ON fis.ConfiguracionFiscal (Ambiente, Activo)
        INCLUDE (Clave);
END;
GO

/* ==========================================================================
   6. VALIDACIÓN FINAL
   ========================================================================== */

DECLARE @TablasEsperadas TABLE (Nombre SYSNAME NOT NULL PRIMARY KEY);

INSERT INTO @TablasEsperadas (Nombre)
VALUES
    (N'DocumentosFiscales'),
    (N'DocumentosDte'),
    (N'EventosDte'),
    (N'ConfiguracionFiscal');

DECLARE @Faltantes INT =
(
    SELECT COUNT(*)
    FROM @TablasEsperadas e
    WHERE OBJECT_ID(N'fis.' + e.Nombre, N'U') IS NULL
);

IF @Faltantes > 0
BEGIN
    SELECT e.Nombre AS TablaFaltante
    FROM @TablasEsperadas e
    WHERE OBJECT_ID(N'fis.' + e.Nombre, N'U') IS NULL;

    THROW 51003, '005_Fiscal_Base.sql terminó con tablas fiscales faltantes.', 1;
END;

PRINT N'============================================================';
PRINT N'GoDental - 005_Fiscal_Base.sql ejecutado correctamente.';
PRINT N'Tablas fiscales esperadas: 4.';
PRINT N'Integración DTE real: pendiente para FIS-01 / fase fiscal.';
PRINT N'============================================================';
GO
