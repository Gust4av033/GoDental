/*
===============================================================================
 GoDental - 006_Comercial_Base.sql
 Versión: 0.1
 Base documental: DM-01 v0.2 + DB-01 v0.1
 Motor: Microsoft SQL Server

 Alcance:
   1) Crear cimientos del módulo Comercial / Online.
   2) Registrar zonas y ejecuciones de prospección.
   3) Registrar prospectos y sus fuentes públicas verificables.
   4) Preparar plantillas, campañas y destinatarios.
   5) Mantener lista central de no contactar.

 Fuera de alcance:
   - Scraping / búsqueda web real.
   - Envío automático por WhatsApp, Instagram, Facebook o email.
   - APIs de Meta / proveedores de mensajería.
   - Automatizaciones periódicas (Worker Service futuro).
   - Conversión de prospecto a cliente como proceso automático.
   - Seeds y permisos iniciales (007_Seed.sql).

 Decisión de diseño:
   - No se utilizan triggers en esta versión.
   - Todo envío comercial debe quedar sujeto a aprobación humana.
   - La lista de no contactar es central e independiente de campañas.
===============================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF DB_ID(N'GoDental') IS NULL
BEGIN
    THROW 51000, N'La base de datos GoDental no existe. Ejecute primero 001_Core.sql.', 1;
END;
GO

USE [GoDental];
GO

IF SCHEMA_ID(N'com') IS NULL
BEGIN
    THROW 51001, N'El esquema com no existe. Ejecute primero 001_Core.sql.', 1;
END;
GO

IF OBJECT_ID(N'seg.Usuarios', N'U') IS NULL
    THROW 51002, N'Falta seg.Usuarios. Ejecute primero 001_Core.sql.', 1;
GO

IF OBJECT_ID(N'cli.Clientes', N'U') IS NULL
    THROW 51003, N'Falta cli.Clientes. Ejecute primero 004_ClientesVentas.sql.', 1;
GO

/* ============================================================================
   1. ZONAS DE PROSPECCIÓN
   ============================================================================ */
IF OBJECT_ID(N'com.ZonasProspeccion', N'U') IS NULL
BEGIN
    CREATE TABLE com.ZonasProspeccion
    (
        IdZona          INT IDENTITY(1,1) NOT NULL,
        Departamento    NVARCHAR(100) NOT NULL,
        Municipio       NVARCHAR(100) NULL,
        Prioridad       SMALLINT NOT NULL CONSTRAINT DF_ZonasProspeccion_Prioridad DEFAULT (100),
        UltimaEjecucion DATETIMEOFFSET(0) NULL,
        Activo           BIT NOT NULL CONSTRAINT DF_ZonasProspeccion_Activo DEFAULT (1),

        CONSTRAINT PK_ZonasProspeccion PRIMARY KEY CLUSTERED (IdZona),
        CONSTRAINT CK_ZonasProspeccion_Prioridad CHECK (Prioridad >= 0)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'com.ZonasProspeccion')
      AND name = N'IX_ZonasProspeccion_Activo_Prioridad'
)
BEGIN
    CREATE INDEX IX_ZonasProspeccion_Activo_Prioridad
        ON com.ZonasProspeccion (Activo, Prioridad, Departamento, Municipio);
END;
GO

/* ============================================================================
   2. PROSPECTOS
   ============================================================================ */
IF OBJECT_ID(N'com.Prospectos', N'U') IS NULL
BEGIN
    CREATE TABLE com.Prospectos
    (
        IdProspecto        BIGINT IDENTITY(1,1) NOT NULL,
        Nombre              NVARCHAR(200) NOT NULL,
        TipoProspecto       NVARCHAR(40) NULL,
        TelefonoPublico     NVARCHAR(40) NULL,
        WhatsAppPublico     NVARCHAR(40) NULL,
        EmailPublico        NVARCHAR(254) NULL,
        Instagram           NVARCHAR(200) NULL,
        Facebook            NVARCHAR(200) NULL,
        Departamento        NVARCHAR(100) NULL,
        Municipio           NVARCHAR(100) NULL,
        Estado              NVARCHAR(30) NOT NULL CONSTRAINT DF_Prospectos_Estado DEFAULT (N'NUEVO'),
        IdClienteConvertido BIGINT NULL,
        NoContactar         BIT NOT NULL CONSTRAINT DF_Prospectos_NoContactar DEFAULT (0),

        CONSTRAINT PK_Prospectos PRIMARY KEY CLUSTERED (IdProspecto),
        CONSTRAINT FK_Prospectos_ClienteConvertido
            FOREIGN KEY (IdClienteConvertido) REFERENCES cli.Clientes(IdCliente),
        CONSTRAINT CK_Prospectos_Estado
            CHECK (Estado IN
            (
                N'NUEVO', N'REVISADO', N'CONTACTADO', N'INTERESADO',
                N'CONVERTIDO', N'DESCARTADO'
            ))
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'com.Prospectos')
      AND name = N'IX_Prospectos_Estado_Ubicacion'
)
BEGIN
    CREATE INDEX IX_Prospectos_Estado_Ubicacion
        ON com.Prospectos (Estado, NoContactar, Departamento, Municipio)
        INCLUDE (Nombre, TipoProspecto, IdClienteConvertido);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'com.Prospectos')
      AND name = N'IX_Prospectos_TelefonoPublico'
)
BEGIN
    CREATE INDEX IX_Prospectos_TelefonoPublico
        ON com.Prospectos (TelefonoPublico)
        WHERE TelefonoPublico IS NOT NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'com.Prospectos')
      AND name = N'IX_Prospectos_WhatsAppPublico'
)
BEGIN
    CREATE INDEX IX_Prospectos_WhatsAppPublico
        ON com.Prospectos (WhatsAppPublico)
        WHERE WhatsAppPublico IS NOT NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'com.Prospectos')
      AND name = N'IX_Prospectos_EmailPublico'
)
BEGIN
    CREATE INDEX IX_Prospectos_EmailPublico
        ON com.Prospectos (EmailPublico)
        WHERE EmailPublico IS NOT NULL;
END;
GO

/* ============================================================================
   3. FUENTES DEL PROSPECTO
   ============================================================================ */
IF OBJECT_ID(N'com.FuentesProspecto', N'U') IS NULL
BEGIN
    CREATE TABLE com.FuentesProspecto
    (
        IdFuente       BIGINT IDENTITY(1,1) NOT NULL,
        IdProspecto    BIGINT NOT NULL,
        TipoFuente     NVARCHAR(40) NOT NULL,
        UrlFuente      NVARCHAR(1000) NOT NULL,
        FechaObtencion DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_FuentesProspecto_FechaObtencion DEFAULT (SYSDATETIMEOFFSET()),
        DatoObtenido   NVARCHAR(300) NULL,

        CONSTRAINT PK_FuentesProspecto PRIMARY KEY CLUSTERED (IdFuente),
        CONSTRAINT FK_FuentesProspecto_Prospectos
            FOREIGN KEY (IdProspecto) REFERENCES com.Prospectos(IdProspecto),
        CONSTRAINT CK_FuentesProspecto_TipoFuente
            CHECK (TipoFuente IN (N'WEB', N'GOOGLE', N'INSTAGRAM', N'FACEBOOK', N'OTRA'))
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'com.FuentesProspecto')
      AND name = N'IX_FuentesProspecto_Prospecto_Fecha'
)
BEGIN
    CREATE INDEX IX_FuentesProspecto_Prospecto_Fecha
        ON com.FuentesProspecto (IdProspecto, FechaObtencion DESC)
        INCLUDE (TipoFuente, DatoObtenido);
END;
GO

/* ============================================================================
   4. EJECUCIONES DE PROSPECCIÓN
   ============================================================================ */
IF OBJECT_ID(N'com.EjecucionesProspeccion', N'U') IS NULL
BEGIN
    CREATE TABLE com.EjecucionesProspeccion
    (
        IdEjecucion  BIGINT IDENTITY(1,1) NOT NULL,
        IdZona       INT NOT NULL,
        FechaInicio  DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_EjecucionesProspeccion_FechaInicio DEFAULT (SYSDATETIMEOFFSET()),
        FechaFin     DATETIMEOFFSET(0) NULL,
        Estado       NVARCHAR(20) NOT NULL CONSTRAINT DF_EjecucionesProspeccion_Estado DEFAULT (N'EN_PROCESO'),
        Encontrados  INT NOT NULL CONSTRAINT DF_EjecucionesProspeccion_Encontrados DEFAULT (0),
        Nuevos       INT NOT NULL CONSTRAINT DF_EjecucionesProspeccion_Nuevos DEFAULT (0),
        DetalleError NVARCHAR(1500) NULL,

        CONSTRAINT PK_EjecucionesProspeccion PRIMARY KEY CLUSTERED (IdEjecucion),
        CONSTRAINT FK_EjecucionesProspeccion_Zonas
            FOREIGN KEY (IdZona) REFERENCES com.ZonasProspeccion(IdZona),
        CONSTRAINT CK_EjecucionesProspeccion_Estado
            CHECK (Estado IN (N'EN_PROCESO', N'OK', N'ERROR')),
        CONSTRAINT CK_EjecucionesProspeccion_Encontrados CHECK (Encontrados >= 0),
        CONSTRAINT CK_EjecucionesProspeccion_Nuevos CHECK (Nuevos >= 0 AND Nuevos <= Encontrados),
        CONSTRAINT CK_EjecucionesProspeccion_Fechas CHECK (FechaFin IS NULL OR FechaFin >= FechaInicio)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'com.EjecucionesProspeccion')
      AND name = N'IX_EjecucionesProspeccion_Zona_Fecha'
)
BEGIN
    CREATE INDEX IX_EjecucionesProspeccion_Zona_Fecha
        ON com.EjecucionesProspeccion (IdZona, FechaInicio DESC)
        INCLUDE (Estado, Encontrados, Nuevos, FechaFin);
END;
GO

/* ============================================================================
   5. PLANTILLAS DE MENSAJE
   ============================================================================ */
IF OBJECT_ID(N'com.PlantillasMensaje', N'U') IS NULL
BEGIN
    CREATE TABLE com.PlantillasMensaje
    (
        IdPlantilla INT IDENTITY(1,1) NOT NULL,
        Nombre      NVARCHAR(120) NOT NULL,
        Canal       NVARCHAR(30) NOT NULL,
        Contenido   NVARCHAR(MAX) NOT NULL,
        Activo      BIT NOT NULL CONSTRAINT DF_PlantillasMensaje_Activo DEFAULT (1),

        CONSTRAINT PK_PlantillasMensaje PRIMARY KEY CLUSTERED (IdPlantilla),
        CONSTRAINT CK_PlantillasMensaje_Canal
            CHECK (Canal IN (N'WHATSAPP', N'INSTAGRAM', N'FACEBOOK', N'EMAIL'))
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'com.PlantillasMensaje')
      AND name = N'IX_PlantillasMensaje_Canal_Activo'
)
BEGIN
    CREATE INDEX IX_PlantillasMensaje_Canal_Activo
        ON com.PlantillasMensaje (Canal, Activo, Nombre);
END;
GO

/* ============================================================================
   6. CAMPAÑAS
   ============================================================================ */
IF OBJECT_ID(N'com.Campanias', N'U') IS NULL
BEGIN
    CREATE TABLE com.Campanias
    (
        IdCampania      BIGINT IDENTITY(1,1) NOT NULL,
        Nombre           NVARCHAR(150) NOT NULL,
        Canal            NVARCHAR(30) NOT NULL,
        IdPlantilla      INT NULL,
        FechaCreacion    DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_Campanias_FechaCreacion DEFAULT (SYSDATETIMEOFFSET()),
        Estado           NVARCHAR(30) NOT NULL CONSTRAINT DF_Campanias_Estado DEFAULT (N'BORRADOR'),
        AprobadaPor      INT NULL,
        FechaAprobacion  DATETIMEOFFSET(0) NULL,

        CONSTRAINT PK_Campanias PRIMARY KEY CLUSTERED (IdCampania),
        CONSTRAINT FK_Campanias_Plantillas
            FOREIGN KEY (IdPlantilla) REFERENCES com.PlantillasMensaje(IdPlantilla),
        CONSTRAINT FK_Campanias_AprobadaPor
            FOREIGN KEY (AprobadaPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_Campanias_Canal
            CHECK (Canal IN (N'WHATSAPP', N'INSTAGRAM', N'FACEBOOK', N'EMAIL')),
        CONSTRAINT CK_Campanias_Estado
            CHECK (Estado IN
            (
                N'BORRADOR', N'PENDIENTE_APROBACION', N'APROBADA',
                N'EN_EJECUCION', N'CERRADA', N'CANCELADA'
            )),
        CONSTRAINT CK_Campanias_Aprobacion
            CHECK
            (
                (AprobadaPor IS NULL AND FechaAprobacion IS NULL)
                OR
                (AprobadaPor IS NOT NULL AND FechaAprobacion IS NOT NULL)
            )
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'com.Campanias')
      AND name = N'IX_Campanias_Estado_Fecha'
)
BEGIN
    CREATE INDEX IX_Campanias_Estado_Fecha
        ON com.Campanias (Estado, FechaCreacion DESC)
        INCLUDE (Nombre, Canal, IdPlantilla, AprobadaPor, FechaAprobacion);
END;
GO

/* ============================================================================
   7. DESTINATARIOS DE CAMPAÑA
   ============================================================================ */
IF OBJECT_ID(N'com.CampaniaDestinatarios', N'U') IS NULL
BEGIN
    CREATE TABLE com.CampaniaDestinatarios
    (
        IdDestinatario BIGINT IDENTITY(1,1) NOT NULL,
        IdCampania     BIGINT NOT NULL,
        IdProspecto    BIGINT NOT NULL,
        Estado         NVARCHAR(30) NOT NULL CONSTRAINT DF_CampaniaDestinatarios_Estado DEFAULT (N'PENDIENTE'),
        FechaEnvio     DATETIMEOFFSET(0) NULL,
        FechaRespuesta DATETIMEOFFSET(0) NULL,
        Resultado      NVARCHAR(500) NULL,

        CONSTRAINT PK_CampaniaDestinatarios PRIMARY KEY CLUSTERED (IdDestinatario),
        CONSTRAINT UQ_CampaniaDestinatarios_CampaniaProspecto UNIQUE (IdCampania, IdProspecto),
        CONSTRAINT FK_CampaniaDestinatarios_Campanias
            FOREIGN KEY (IdCampania) REFERENCES com.Campanias(IdCampania),
        CONSTRAINT FK_CampaniaDestinatarios_Prospectos
            FOREIGN KEY (IdProspecto) REFERENCES com.Prospectos(IdProspecto),
        CONSTRAINT CK_CampaniaDestinatarios_Estado
            CHECK (Estado IN (N'PENDIENTE', N'ENVIADO', N'RESPONDIO', N'NO_CONTACTAR', N'ERROR')),
        CONSTRAINT CK_CampaniaDestinatarios_Fechas
            CHECK (FechaRespuesta IS NULL OR FechaEnvio IS NULL OR FechaRespuesta >= FechaEnvio)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'com.CampaniaDestinatarios')
      AND name = N'IX_CampaniaDestinatarios_Estado'
)
BEGIN
    CREATE INDEX IX_CampaniaDestinatarios_Estado
        ON com.CampaniaDestinatarios (IdCampania, Estado)
        INCLUDE (IdProspecto, FechaEnvio, FechaRespuesta);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'com.CampaniaDestinatarios')
      AND name = N'IX_CampaniaDestinatarios_Prospecto'
)
BEGIN
    CREATE INDEX IX_CampaniaDestinatarios_Prospecto
        ON com.CampaniaDestinatarios (IdProspecto, IdCampania)
        INCLUDE (Estado, FechaEnvio, FechaRespuesta, Resultado);
END;
GO

/* ============================================================================
   8. LISTA CENTRAL DE NO CONTACTAR
   ============================================================================ */
IF OBJECT_ID(N'com.ListaNoContactar', N'U') IS NULL
BEGIN
    CREATE TABLE com.ListaNoContactar
    (
        IdNoContactar  BIGINT IDENTITY(1,1) NOT NULL,
        Canal          NVARCHAR(30) NOT NULL,
        Identificador  NVARCHAR(300) NOT NULL,
        Motivo         NVARCHAR(500) NULL,
        Fecha          DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_ListaNoContactar_Fecha DEFAULT (SYSDATETIMEOFFSET()),
        RegistradoPor  INT NULL,
        Activo         BIT NOT NULL CONSTRAINT DF_ListaNoContactar_Activo DEFAULT (1),

        CONSTRAINT PK_ListaNoContactar PRIMARY KEY CLUSTERED (IdNoContactar),
        CONSTRAINT UQ_ListaNoContactar_CanalIdentificador UNIQUE (Canal, Identificador),
        CONSTRAINT FK_ListaNoContactar_RegistradoPor
            FOREIGN KEY (RegistradoPor) REFERENCES seg.Usuarios(IdUsuario),
        CONSTRAINT CK_ListaNoContactar_Canal
            CHECK (Canal IN (N'WHATSAPP', N'INSTAGRAM', N'FACEBOOK', N'EMAIL'))
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'com.ListaNoContactar')
      AND name = N'IX_ListaNoContactar_Activo_Canal'
)
BEGIN
    CREATE INDEX IX_ListaNoContactar_Activo_Canal
        ON com.ListaNoContactar (Activo, Canal, Identificador)
        INCLUDE (Fecha, Motivo, RegistradoPor);
END;
GO

/* ============================================================================
   9. VALIDACIÓN FINAL
   ============================================================================ */
DECLARE @Esperadas INT = 8;
DECLARE @Creadas INT =
(
    SELECT COUNT(*)
    FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'com'
      AND t.name IN
      (
          N'ZonasProspeccion',
          N'Prospectos',
          N'FuentesProspecto',
          N'EjecucionesProspeccion',
          N'PlantillasMensaje',
          N'Campanias',
          N'CampaniaDestinatarios',
          N'ListaNoContactar'
      )
);

IF @Creadas <> @Esperadas
BEGIN
    THROW 51010, N'006_Comercial_Base.sql no creó/encontró todas las tablas esperadas.', 1;
END;

PRINT N'006_Comercial_Base.sql completado correctamente. Tablas Comercial/Online: 8.';
PRINT N'Nota: este script sólo crea cimientos; no realiza búsquedas ni envíos automáticos.';
GO
