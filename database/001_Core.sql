/*
===============================================================================
 GoDental - 001_Core.sql
 Versión: 0.1
 Base documental: DM-01 v0.2 + DB-01 v0.1
 Motor: Microsoft SQL Server

 Alcance:
   1) Crear base de datos GoDental si no existe.
   2) Crear esquemas funcionales.
   3) Crear núcleo de Seguridad (seg).
   4) Crear núcleo de Sistema (sis).
   5) Crear Catálogos compartidos (cat).

 Fuera de alcance:
   - Inventario (002_Inventario.sql)
   - Compras/importaciones (003_Compras.sql)
   - Clientes/ventas (004_ClientesVentas.sql)
   - Fiscal/DTE (005_Fiscal_Base.sql)
   - Comercial/Online (006_Comercial_Base.sql)
   - Datos iniciales (007_Seed.sql)
   - Vistas y procedimientos (008_ViewsProcedures.sql)

 Decisión de diseño:
   - No se utilizan triggers en esta versión.
   - La lógica crítica se implementará mediante transacciones, procedimientos
     almacenados/servicios de aplicación, restricciones, índices y ROWVERSION.
===============================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================================
   1. BASE DE DATOS
   ============================================================================ */
IF DB_ID(N'GoDental') IS NULL
BEGIN
    PRINT N'Creando base de datos [GoDental]...';
    CREATE DATABASE [GoDental];
END
ELSE
BEGIN
    PRINT N'La base de datos [GoDental] ya existe.';
END;
GO

USE [GoDental];
GO

/* ============================================================================
   2. ESQUEMAS
   ============================================================================ */
IF SCHEMA_ID(N'seg') IS NULL EXEC(N'CREATE SCHEMA [seg] AUTHORIZATION [dbo];');
IF SCHEMA_ID(N'sis') IS NULL EXEC(N'CREATE SCHEMA [sis] AUTHORIZATION [dbo];');
IF SCHEMA_ID(N'cat') IS NULL EXEC(N'CREATE SCHEMA [cat] AUTHORIZATION [dbo];');
IF SCHEMA_ID(N'inv') IS NULL EXEC(N'CREATE SCHEMA [inv] AUTHORIZATION [dbo];');
IF SCHEMA_ID(N'cmp') IS NULL EXEC(N'CREATE SCHEMA [cmp] AUTHORIZATION [dbo];');
IF SCHEMA_ID(N'cli') IS NULL EXEC(N'CREATE SCHEMA [cli] AUTHORIZATION [dbo];');
IF SCHEMA_ID(N'ven') IS NULL EXEC(N'CREATE SCHEMA [ven] AUTHORIZATION [dbo];');
IF SCHEMA_ID(N'fis') IS NULL EXEC(N'CREATE SCHEMA [fis] AUTHORIZATION [dbo];');
IF SCHEMA_ID(N'com') IS NULL EXEC(N'CREATE SCHEMA [com] AUTHORIZATION [dbo];');
GO

/* ============================================================================
   3. SEGURIDAD
   ============================================================================ */

/* --------------------------------------------------------------------------
   seg.Roles
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'seg.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE seg.Roles
    (
        IdRol       INT IDENTITY(1,1) NOT NULL,
        Nombre      NVARCHAR(60) NOT NULL,
        Descripcion NVARCHAR(250) NULL,
        EsSistema   BIT NOT NULL CONSTRAINT DF_Roles_EsSistema DEFAULT (0),
        Activo      BIT NOT NULL CONSTRAINT DF_Roles_Activo DEFAULT (1),

        CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (IdRol),
        CONSTRAINT UQ_Roles_Nombre UNIQUE (Nombre)
    );
END;
GO

/* --------------------------------------------------------------------------
   seg.Permisos
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'seg.Permisos', N'U') IS NULL
BEGIN
    CREATE TABLE seg.Permisos
    (
        IdPermiso  INT IDENTITY(1,1) NOT NULL,
        Codigo     NVARCHAR(100) NOT NULL,
        Nombre     NVARCHAR(100) NOT NULL,
        Modulo     NVARCHAR(50) NOT NULL,
        Descripcion NVARCHAR(250) NULL,
        Activo     BIT NOT NULL CONSTRAINT DF_Permisos_Activo DEFAULT (1),

        CONSTRAINT PK_Permisos PRIMARY KEY CLUSTERED (IdPermiso),
        CONSTRAINT UQ_Permisos_Codigo UNIQUE (Codigo)
    );
END;
GO

/* --------------------------------------------------------------------------
   seg.RolPermisos
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'seg.RolPermisos', N'U') IS NULL
BEGIN
    CREATE TABLE seg.RolPermisos
    (
        IdRol      INT NOT NULL,
        IdPermiso  INT NOT NULL,
        Concedido  BIT NOT NULL CONSTRAINT DF_RolPermisos_Concedido DEFAULT (1),

        CONSTRAINT PK_RolPermisos PRIMARY KEY CLUSTERED (IdRol, IdPermiso),
        CONSTRAINT FK_RolPermisos_Roles
            FOREIGN KEY (IdRol) REFERENCES seg.Roles(IdRol),
        CONSTRAINT FK_RolPermisos_Permisos
            FOREIGN KEY (IdPermiso) REFERENCES seg.Permisos(IdPermiso)
    );
END;
GO

/* --------------------------------------------------------------------------
   seg.Usuarios
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'seg.Usuarios', N'U') IS NULL
BEGIN
    CREATE TABLE seg.Usuarios
    (
        IdUsuario          INT IDENTITY(1,1) NOT NULL,
        Usuario            NVARCHAR(60) NOT NULL,
        NombreCompleto     NVARCHAR(150) NOT NULL,
        PasswordHash       NVARCHAR(512) NOT NULL,
        Email              NVARCHAR(254) NULL,
        DebeCambiarClave   BIT NOT NULL CONSTRAINT DF_Usuarios_DebeCambiarClave DEFAULT (1),
        IntentosFallidos   SMALLINT NOT NULL CONSTRAINT DF_Usuarios_IntentosFallidos DEFAULT (0),
        BloqueadoHasta     DATETIMEOFFSET(0) NULL,
        UltimoAcceso       DATETIMEOFFSET(0) NULL,
        Activo             BIT NOT NULL CONSTRAINT DF_Usuarios_Activo DEFAULT (1),
        RowVersion         ROWVERSION NOT NULL,

        CONSTRAINT PK_Usuarios PRIMARY KEY CLUSTERED (IdUsuario),
        CONSTRAINT UQ_Usuarios_Usuario UNIQUE (Usuario),
        CONSTRAINT CK_Usuarios_IntentosFallidos CHECK (IntentosFallidos >= 0)
    );
END;
GO

/* --------------------------------------------------------------------------
   seg.UsuarioRoles
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'seg.UsuarioRoles', N'U') IS NULL
BEGIN
    CREATE TABLE seg.UsuarioRoles
    (
        IdUsuario       INT NOT NULL,
        IdRol           INT NOT NULL,
        AsignadoPor     INT NULL,
        FechaAsignacion DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_UsuarioRoles_FechaAsignacion DEFAULT (SYSDATETIMEOFFSET()),

        CONSTRAINT PK_UsuarioRoles PRIMARY KEY CLUSTERED (IdUsuario, IdRol),
        CONSTRAINT FK_UsuarioRoles_Usuarios
            FOREIGN KEY (IdUsuario) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT FK_UsuarioRoles_Roles
            FOREIGN KEY (IdRol) REFERENCES seg.Roles(IdRol),
        CONSTRAINT FK_UsuarioRoles_AsignadoPor
            FOREIGN KEY (AsignadoPor) REFERENCES seg.Usuarios(IdUsuario)
    );
END;
GO

/* ============================================================================
   4. SISTEMA
   ============================================================================ */

/* --------------------------------------------------------------------------
   sis.Empresa
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'sis.Empresa', N'U') IS NULL
BEGIN
    CREATE TABLE sis.Empresa
    (
        IdEmpresa       INT IDENTITY(1,1) NOT NULL,
        RazonSocial     NVARCHAR(200) NULL,
        NombreComercial NVARCHAR(200) NULL,
        NIT             NVARCHAR(30) NULL,
        NRC             NVARCHAR(30) NULL,
        Giro            NVARCHAR(250) NULL,
        Direccion       NVARCHAR(500) NULL,
        Departamento    NVARCHAR(100) NULL,
        Municipio       NVARCHAR(100) NULL,
        Telefono        NVARCHAR(30) NULL,
        WhatsApp        NVARCHAR(30) NULL,
        Email           NVARCHAR(254) NULL,
        LogoRuta        NVARCHAR(500) NULL,
        Activo          BIT NOT NULL CONSTRAINT DF_Empresa_Activo DEFAULT (1),

        CONSTRAINT PK_Empresa PRIMARY KEY CLUSTERED (IdEmpresa)
    );
END;
GO

/* --------------------------------------------------------------------------
   sis.Configuracion
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'sis.Configuracion', N'U') IS NULL
BEGIN
    CREATE TABLE sis.Configuracion
    (
        Clave             NVARCHAR(120) NOT NULL,
        Valor             NVARCHAR(1000) NOT NULL,
        TipoDato          NVARCHAR(20) NOT NULL,
        Descripcion       NVARCHAR(300) NULL,
        EditableUI        BIT NOT NULL CONSTRAINT DF_Configuracion_EditableUI DEFAULT (0),
        ModificadoPor     INT NULL,
        FechaModificacion DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_Configuracion_FechaModificacion DEFAULT (SYSDATETIMEOFFSET()),

        CONSTRAINT PK_Configuracion PRIMARY KEY CLUSTERED (Clave),
        CONSTRAINT FK_Configuracion_Usuarios
            FOREIGN KEY (ModificadoPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_Configuracion_TipoDato
            CHECK (TipoDato IN (N'INT', N'DECIMAL', N'BOOL', N'STRING', N'JSON_CONTROLADO'))
    );
END;
GO

/* --------------------------------------------------------------------------
   sis.EquiposTrabajo
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'sis.EquiposTrabajo', N'U') IS NULL
BEGIN
    CREATE TABLE sis.EquiposTrabajo
    (
        IdEquipo             INT IDENTITY(1,1) NOT NULL,
        IdentificadorEquipo  NVARCHAR(120) NOT NULL,
        NombreEquipo         NVARCHAR(120) NOT NULL,
        Descripcion          NVARCHAR(200) NULL,
        UltimaIp             NVARCHAR(64) NULL,
        UltimaConexion       DATETIMEOFFSET(0) NULL,
        Activo               BIT NOT NULL CONSTRAINT DF_EquiposTrabajo_Activo DEFAULT (1),
        RowVersion           ROWVERSION NOT NULL,

        CONSTRAINT PK_EquiposTrabajo PRIMARY KEY CLUSTERED (IdEquipo),
        CONSTRAINT UQ_EquiposTrabajo_Identificador UNIQUE (IdentificadorEquipo)
    );
END;
GO

/* --------------------------------------------------------------------------
   sis.PreferenciasUsuario
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'sis.PreferenciasUsuario', N'U') IS NULL
BEGIN
    CREATE TABLE sis.PreferenciasUsuario
    (
        IdUsuario                 INT NOT NULL,
        Tema                      NVARCHAR(20) NOT NULL CONSTRAINT DF_PreferenciasUsuario_Tema DEFAULT (N'SYSTEM'),
        SidebarCompacta           BIT NOT NULL CONSTRAINT DF_PreferenciasUsuario_SidebarCompacta DEFAULT (0),
        PaginaInicio              NVARCHAR(80) NULL,
        TamanoDocumentoPreferido  NVARCHAR(40) NULL,

        CONSTRAINT PK_PreferenciasUsuario PRIMARY KEY CLUSTERED (IdUsuario),
        CONSTRAINT FK_PreferenciasUsuario_Usuarios
            FOREIGN KEY (IdUsuario) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_PreferenciasUsuario_Tema
            CHECK (Tema IN (N'LIGHT', N'DARK', N'SYSTEM'))
    );
END;
GO

/* --------------------------------------------------------------------------
   sis.SecuenciasDocumento
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'sis.SecuenciasDocumento', N'U') IS NULL
BEGIN
    CREATE TABLE sis.SecuenciasDocumento
    (
        IdSecuencia    INT IDENTITY(1,1) NOT NULL,
        TipoDocumento  NVARCHAR(40) NOT NULL,
        Prefijo        NVARCHAR(20) NULL,
        UltimoNumero   BIGINT NOT NULL CONSTRAINT DF_SecuenciasDocumento_UltimoNumero DEFAULT (0),
        Longitud       TINYINT NOT NULL CONSTRAINT DF_SecuenciasDocumento_Longitud DEFAULT (8),
        ReinicioAnual  BIT NOT NULL CONSTRAINT DF_SecuenciasDocumento_ReinicioAnual DEFAULT (0),
        AnioActual     SMALLINT NULL,
        RowVersion     ROWVERSION NOT NULL,

        CONSTRAINT PK_SecuenciasDocumento PRIMARY KEY CLUSTERED (IdSecuencia),
        CONSTRAINT UQ_SecuenciasDocumento_Tipo UNIQUE (TipoDocumento),
        CONSTRAINT CK_SecuenciasDocumento_UltimoNumero CHECK (UltimoNumero >= 0),
        CONSTRAINT CK_SecuenciasDocumento_Longitud CHECK (Longitud BETWEEN 1 AND 20)
    );
END;
GO

/* --------------------------------------------------------------------------
   sis.AutorizacionesOperacion
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'sis.AutorizacionesOperacion', N'U') IS NULL
BEGIN
    CREATE TABLE sis.AutorizacionesOperacion
    (
        IdAutorizacion    BIGINT IDENTITY(1,1) NOT NULL,
        TipoOperacion     NVARCHAR(60) NOT NULL,
        EntidadTipo       NVARCHAR(60) NOT NULL,
        EntidadId         BIGINT NULL,
        SolicitadoPor     INT NOT NULL,
        AutorizadoPor     INT NULL,
        PorcentajeValor   DECIMAL(9,4) NULL,
        Motivo            NVARCHAR(1000) NOT NULL,
        FechaSolicitud    DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_Autorizaciones_FechaSolicitud DEFAULT (SYSDATETIMEOFFSET()),
        FechaAutorizacion DATETIMEOFFSET(0) NULL,
        Resultado         NVARCHAR(20) NOT NULL
            CONSTRAINT DF_Autorizaciones_Resultado DEFAULT (N'PENDIENTE'),

        CONSTRAINT PK_AutorizacionesOperacion PRIMARY KEY CLUSTERED (IdAutorizacion),
        CONSTRAINT FK_Autorizaciones_SolicitadoPor
            FOREIGN KEY (SolicitadoPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT FK_Autorizaciones_AutorizadoPor
            FOREIGN KEY (AutorizadoPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_Autorizaciones_Resultado
            CHECK (Resultado IN (N'PENDIENTE', N'APROBADA', N'RECHAZADA')),
        CONSTRAINT CK_Autorizaciones_Porcentaje
            CHECK (PorcentajeValor IS NULL OR (PorcentajeValor >= 0 AND PorcentajeValor <= 100)),
        CONSTRAINT CK_Autorizaciones_Fechas
            CHECK (FechaAutorizacion IS NULL OR FechaAutorizacion >= FechaSolicitud)
    );
END;
GO

/* --------------------------------------------------------------------------
   sis.Bitacora
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'sis.Bitacora', N'U') IS NULL
BEGIN
    CREATE TABLE sis.Bitacora
    (
        IdBitacora        BIGINT IDENTITY(1,1) NOT NULL,
        Fecha              DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_Bitacora_Fecha DEFAULT (SYSDATETIMEOFFSET()),
        IdUsuario          INT NULL,
        IdEquipo           INT NULL,
        Modulo             NVARCHAR(50) NOT NULL,
        Accion             NVARCHAR(80) NOT NULL,
        Entidad            NVARCHAR(80) NULL,
        EntidadId          BIGINT NULL,
        Descripcion        NVARCHAR(500) NOT NULL,
        DatosAntesJson     NVARCHAR(MAX) NULL,
        DatosDespuesJson   NVARCHAR(MAX) NULL,

        CONSTRAINT PK_Bitacora PRIMARY KEY CLUSTERED (IdBitacora),
        CONSTRAINT FK_Bitacora_Usuarios
            FOREIGN KEY (IdUsuario) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT FK_Bitacora_EquiposTrabajo
            FOREIGN KEY (IdEquipo) REFERENCES sis.EquiposTrabajo(IdEquipo),
        CONSTRAINT CK_Bitacora_DatosAntesJson
            CHECK (DatosAntesJson IS NULL OR ISJSON(DatosAntesJson) = 1),
        CONSTRAINT CK_Bitacora_DatosDespuesJson
            CHECK (DatosDespuesJson IS NULL OR ISJSON(DatosDespuesJson) = 1)
    );
END;
GO

/* --------------------------------------------------------------------------
   sis.HistorialBackups
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'sis.HistorialBackups', N'U') IS NULL
BEGIN
    CREATE TABLE sis.HistorialBackups
    (
        IdBackup        BIGINT IDENTITY(1,1) NOT NULL,
        FechaInicio     DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_HistorialBackups_FechaInicio DEFAULT (SYSDATETIMEOFFSET()),
        FechaFin        DATETIMEOFFSET(0) NULL,
        Tipo            NVARCHAR(20) NOT NULL CONSTRAINT DF_HistorialBackups_Tipo DEFAULT (N'FULL'),
        RutaBak         NVARCHAR(500) NOT NULL,
        TamanoBytes     BIGINT NULL,
        Estado          NVARCHAR(20) NOT NULL CONSTRAINT DF_HistorialBackups_Estado DEFAULT (N'EN_PROCESO'),
        DetalleError    NVARCHAR(1500) NULL,
        CopiadoOneDrive BIT NOT NULL CONSTRAINT DF_HistorialBackups_CopiadoOneDrive DEFAULT (0),

        CONSTRAINT PK_HistorialBackups PRIMARY KEY CLUSTERED (IdBackup),
        CONSTRAINT CK_HistorialBackups_Tipo CHECK (Tipo IN (N'FULL')),
        CONSTRAINT CK_HistorialBackups_Estado CHECK (Estado IN (N'EN_PROCESO', N'OK', N'ERROR')),
        CONSTRAINT CK_HistorialBackups_Tamano CHECK (TamanoBytes IS NULL OR TamanoBytes >= 0),
        CONSTRAINT CK_HistorialBackups_Fechas CHECK (FechaFin IS NULL OR FechaFin >= FechaInicio)
    );
END;
GO

/* ============================================================================
   5. CATÁLOGOS
   ============================================================================ */

/* --------------------------------------------------------------------------
   cat.CategoriasProducto
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cat.CategoriasProducto', N'U') IS NULL
BEGIN
    CREATE TABLE cat.CategoriasProducto
    (
        IdCategoria      INT IDENTITY(1,1) NOT NULL,
        IdCategoriaPadre INT NULL,
        Nombre           NVARCHAR(120) NOT NULL,
        Descripcion      NVARCHAR(300) NULL,
        Activo           BIT NOT NULL CONSTRAINT DF_CategoriasProducto_Activo DEFAULT (1),

        CONSTRAINT PK_CategoriasProducto PRIMARY KEY CLUSTERED (IdCategoria),
        CONSTRAINT FK_CategoriasProducto_Padre
            FOREIGN KEY (IdCategoriaPadre) REFERENCES cat.CategoriasProducto(IdCategoria),
        CONSTRAINT UQ_CategoriasProducto_PadreNombre UNIQUE (IdCategoriaPadre, Nombre),
        CONSTRAINT CK_CategoriasProducto_NoAutoPadre
            CHECK (IdCategoriaPadre IS NULL OR IdCategoriaPadre <> IdCategoria)
    );
END;
GO

/* --------------------------------------------------------------------------
   cat.Marcas
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cat.Marcas', N'U') IS NULL
BEGIN
    CREATE TABLE cat.Marcas
    (
        IdMarca     INT IDENTITY(1,1) NOT NULL,
        Nombre      NVARCHAR(120) NOT NULL,
        PaisOrigen  NVARCHAR(80) NULL,
        Activo      BIT NOT NULL CONSTRAINT DF_Marcas_Activo DEFAULT (1),

        CONSTRAINT PK_Marcas PRIMARY KEY CLUSTERED (IdMarca),
        CONSTRAINT UQ_Marcas_Nombre UNIQUE (Nombre)
    );
END;
GO

/* --------------------------------------------------------------------------
   cat.UnidadesMedida
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cat.UnidadesMedida', N'U') IS NULL
BEGIN
    CREATE TABLE cat.UnidadesMedida
    (
        IdUnidadMedida   SMALLINT IDENTITY(1,1) NOT NULL,
        Codigo           NVARCHAR(15) NOT NULL,
        Nombre           NVARCHAR(60) NOT NULL,
        PermiteDecimales BIT NOT NULL CONSTRAINT DF_UnidadesMedida_PermiteDecimales DEFAULT (0),
        Activo           BIT NOT NULL CONSTRAINT DF_UnidadesMedida_Activo DEFAULT (1),

        CONSTRAINT PK_UnidadesMedida PRIMARY KEY CLUSTERED (IdUnidadMedida),
        CONSTRAINT UQ_UnidadesMedida_Codigo UNIQUE (Codigo)
    );
END;
GO

/* --------------------------------------------------------------------------
   cat.Monedas
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cat.Monedas', N'U') IS NULL
BEGIN
    CREATE TABLE cat.Monedas
    (
        IdMoneda SMALLINT IDENTITY(1,1) NOT NULL,
        CodigoISO CHAR(3) NOT NULL,
        Nombre   NVARCHAR(60) NOT NULL,
        EsBase   BIT NOT NULL CONSTRAINT DF_Monedas_EsBase DEFAULT (0),
        Activo   BIT NOT NULL CONSTRAINT DF_Monedas_Activo DEFAULT (1),

        CONSTRAINT PK_Monedas PRIMARY KEY CLUSTERED (IdMoneda),
        CONSTRAINT UQ_Monedas_CodigoISO UNIQUE (CodigoISO)
    );
END;
GO

/* --------------------------------------------------------------------------
   cat.MetodosPago
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cat.MetodosPago', N'U') IS NULL
BEGIN
    CREATE TABLE cat.MetodosPago
    (
        IdMetodoPago       SMALLINT IDENTITY(1,1) NOT NULL,
        Codigo             NVARCHAR(30) NOT NULL,
        Nombre             NVARCHAR(80) NOT NULL,
        AfectaCajaEfectivo BIT NOT NULL CONSTRAINT DF_MetodosPago_AfectaCaja DEFAULT (0),
        RequiereReferencia BIT NOT NULL CONSTRAINT DF_MetodosPago_RequiereReferencia DEFAULT (0),
        Activo             BIT NOT NULL CONSTRAINT DF_MetodosPago_Activo DEFAULT (1),

        CONSTRAINT PK_MetodosPago PRIMARY KEY CLUSTERED (IdMetodoPago),
        CONSTRAINT UQ_MetodosPago_Codigo UNIQUE (Codigo)
    );
END;
GO

/* --------------------------------------------------------------------------
   cat.TiposCliente
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cat.TiposCliente', N'U') IS NULL
BEGIN
    CREATE TABLE cat.TiposCliente
    (
        IdTipoCliente         SMALLINT IDENTITY(1,1) NOT NULL,
        Codigo                NVARCHAR(30) NOT NULL,
        Nombre                NVARCHAR(80) NOT NULL,
        ElegibleCreditoDefault BIT NOT NULL CONSTRAINT DF_TiposCliente_Credito DEFAULT (0),
        Activo                BIT NOT NULL CONSTRAINT DF_TiposCliente_Activo DEFAULT (1),

        CONSTRAINT PK_TiposCliente PRIMARY KEY CLUSTERED (IdTipoCliente),
        CONSTRAINT UQ_TiposCliente_Codigo UNIQUE (Codigo)
    );
END;
GO

/* --------------------------------------------------------------------------
   cat.Impuestos
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'cat.Impuestos', N'U') IS NULL
BEGIN
    CREATE TABLE cat.Impuestos
    (
        IdImpuesto      SMALLINT IDENTITY(1,1) NOT NULL,
        Codigo          NVARCHAR(20) NOT NULL,
        Nombre          NVARCHAR(80) NOT NULL,
        Tasa            DECIMAL(9,4) NOT NULL,
        IncluidoEnPrecio BIT NOT NULL CONSTRAINT DF_Impuestos_IncluidoEnPrecio DEFAULT (0),
        Activo          BIT NOT NULL CONSTRAINT DF_Impuestos_Activo DEFAULT (1),

        CONSTRAINT PK_Impuestos PRIMARY KEY CLUSTERED (IdImpuesto),
        CONSTRAINT UQ_Impuestos_Codigo UNIQUE (Codigo),
        CONSTRAINT CK_Impuestos_Tasa CHECK (Tasa >= 0 AND Tasa <= 100)
    );
END;
GO

/* ============================================================================
   6. ÍNDICES
   ============================================================================ */

-- seg.Roles
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'seg.Roles') AND name = N'IX_Roles_Activo')
    CREATE INDEX IX_Roles_Activo ON seg.Roles(Activo);
GO

-- seg.Permisos
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'seg.Permisos') AND name = N'IX_Permisos_Modulo_Activo')
    CREATE INDEX IX_Permisos_Modulo_Activo ON seg.Permisos(Modulo, Activo);
GO

-- seg.Usuarios
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'seg.Usuarios') AND name = N'IX_Usuarios_Activo')
    CREATE INDEX IX_Usuarios_Activo ON seg.Usuarios(Activo);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'seg.Usuarios') AND name = N'IX_Usuarios_Email')
    CREATE INDEX IX_Usuarios_Email ON seg.Usuarios(Email) WHERE Email IS NOT NULL;
GO

-- sis.EquiposTrabajo
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'sis.EquiposTrabajo') AND name = N'IX_EquiposTrabajo_Nombre')
    CREATE INDEX IX_EquiposTrabajo_Nombre ON sis.EquiposTrabajo(NombreEquipo);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'sis.EquiposTrabajo') AND name = N'IX_EquiposTrabajo_Activo')
    CREATE INDEX IX_EquiposTrabajo_Activo ON sis.EquiposTrabajo(Activo);
GO

-- sis.AutorizacionesOperacion
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'sis.AutorizacionesOperacion') AND name = N'IX_Autorizaciones_Tipo_Fecha')
    CREATE INDEX IX_Autorizaciones_Tipo_Fecha
        ON sis.AutorizacionesOperacion(TipoOperacion, FechaSolicitud);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'sis.AutorizacionesOperacion') AND name = N'IX_Autorizaciones_Entidad')
    CREATE INDEX IX_Autorizaciones_Entidad
        ON sis.AutorizacionesOperacion(EntidadTipo, EntidadId);
GO

-- sis.Bitacora
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'sis.Bitacora') AND name = N'IX_Bitacora_Fecha')
    CREATE INDEX IX_Bitacora_Fecha ON sis.Bitacora(Fecha DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'sis.Bitacora') AND name = N'IX_Bitacora_Usuario_Fecha')
    CREATE INDEX IX_Bitacora_Usuario_Fecha ON sis.Bitacora(IdUsuario, Fecha DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'sis.Bitacora') AND name = N'IX_Bitacora_Entidad')
    CREATE INDEX IX_Bitacora_Entidad ON sis.Bitacora(Entidad, EntidadId);
GO

-- sis.HistorialBackups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'sis.HistorialBackups') AND name = N'IX_HistorialBackups_FechaInicio')
    CREATE INDEX IX_HistorialBackups_FechaInicio ON sis.HistorialBackups(FechaInicio DESC);
GO

-- cat.Monedas: sólo puede existir una moneda base.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'cat.Monedas') AND name = N'UX_Monedas_UnaBase')
    CREATE UNIQUE INDEX UX_Monedas_UnaBase ON cat.Monedas(EsBase) WHERE EsBase = 1;
GO

/* ============================================================================
   7. VALIDACIÓN BÁSICA
   ============================================================================ */
DECLARE @TablasCoreEsperadas INT = 20;
DECLARE @TablasCoreEncontradas INT;

SELECT @TablasCoreEncontradas = COUNT(*)
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE (s.name = N'seg' AND t.name IN (N'Roles', N'Permisos', N'RolPermisos', N'Usuarios', N'UsuarioRoles'))
   OR (s.name = N'sis' AND t.name IN (N'Empresa', N'Configuracion', N'EquiposTrabajo', N'PreferenciasUsuario', N'SecuenciasDocumento', N'AutorizacionesOperacion', N'Bitacora', N'HistorialBackups'))
   OR (s.name = N'cat' AND t.name IN (N'CategoriasProducto', N'Marcas', N'UnidadesMedida', N'Monedas', N'MetodosPago', N'TiposCliente', N'Impuestos'));

-- 5 + 8 + 7 = 20 tablas.

IF @TablasCoreEncontradas <> @TablasCoreEsperadas
BEGIN
    THROW 51000, 'Validación de 001_Core.sql falló: no se encontraron todas las tablas esperadas.', 1;
END;

PRINT N'001_Core.sql completado correctamente.';
PRINT N'Tablas Core encontradas: ' + CONVERT(NVARCHAR(10), @TablasCoreEncontradas);
GO
