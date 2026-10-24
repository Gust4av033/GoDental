/* DEV-47: planificación interna conforme a referencia 221200. Sin transporte externo. */
SET XACT_ABORT ON;
GO
IF COL_LENGTH(N'com.Prospectos',N'IdVentaConversion') IS NULL
 THROW 53601,N'Falta aplicar 025_Online_Interno.sql.',1;
GO
IF COL_LENGTH(N'com.ZonasProspeccion',N'RowVersion') IS NULL
 ALTER TABLE com.ZonasProspeccion ADD RowVersion rowversion NOT NULL;
IF COL_LENGTH(N'com.PlantillasMensaje',N'RowVersion') IS NULL
 ALTER TABLE com.PlantillasMensaje ADD RowVersion rowversion NOT NULL;
IF OBJECT_ID(N'com.SegmentosProspecto',N'U') IS NULL
 CREATE TABLE com.SegmentosProspecto(
  IdSegmento int IDENTITY NOT NULL CONSTRAINT PK_SegmentosProspecto PRIMARY KEY,
  Nombre nvarchar(120) NOT NULL,Departamento nvarchar(100) NULL,Municipio nvarchar(100) NULL,
  TipoProspecto nvarchar(40) NULL,EstadoProspecto nvarchar(30) NULL,TipoFuente nvarchar(40) NULL,
  Canal nvarchar(30) NOT NULL,Activo bit NOT NULL CONSTRAINT DF_SegmentosProspecto_Activo DEFAULT(1),
  RowVersion rowversion NOT NULL,
  CONSTRAINT CK_SegmentosProspecto_Canal CHECK(Canal IN(N'WHATSAPP',N'EMAIL',N'INSTAGRAM',N'FACEBOOK')),
  CONSTRAINT CK_SegmentosProspecto_Estado CHECK(EstadoProspecto IS NULL OR EstadoProspecto IN(N'NUEVO',N'REVISADO',N'CONTACTADO',N'INTERESADO')),
  CONSTRAINT CK_SegmentosProspecto_Fuente CHECK(TipoFuente IS NULL OR TipoFuente IN(N'WEB',N'GOOGLE',N'INSTAGRAM',N'FACEBOOK',N'OTRA')));
GO
IF COL_LENGTH(N'com.Campanias',N'RowVersion') IS NULL
 ALTER TABLE com.Campanias ADD RowVersion rowversion NOT NULL,IdSegmento int NULL,
 CriteriosJson nvarchar(max) NULL,ContenidoSnapshot nvarchar(4000) NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Campanias_SegmentosProspecto')
 ALTER TABLE com.Campanias ADD CONSTRAINT FK_Campanias_SegmentosProspecto FOREIGN KEY(IdSegmento) REFERENCES com.SegmentosProspecto(IdSegmento);
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_Campanias_CriteriosJson')
 ALTER TABLE com.Campanias ADD CONSTRAINT CK_Campanias_CriteriosJson CHECK(CriteriosJson IS NULL OR ISJSON(CriteriosJson)=1);
IF COL_LENGTH(N'com.CampaniaDestinatarios',N'ContactoSnapshot') IS NULL
 ALTER TABLE com.CampaniaDestinatarios ADD ContactoSnapshot nvarchar(300) NULL,VersionProspecto binary(8) NULL;
GO
IF OBJECT_ID(N'com.SeguimientosProspecto',N'U') IS NULL
 CREATE TABLE com.SeguimientosProspecto(
  IdSeguimiento bigint IDENTITY NOT NULL CONSTRAINT PK_SeguimientosProspecto PRIMARY KEY,
  IdProspecto bigint NOT NULL,IdUsuario int NOT NULL,Fecha datetimeoffset(0) NOT NULL,
  ProximaGestion date NULL,Canal nvarchar(30) NOT NULL,Resultado nvarchar(30) NOT NULL,
  Nota nvarchar(1500) NOT NULL,
  CONSTRAINT FK_SeguimientosProspecto_Prospectos FOREIGN KEY(IdProspecto) REFERENCES com.Prospectos(IdProspecto),
  CONSTRAINT FK_SeguimientosProspecto_Usuarios FOREIGN KEY(IdUsuario) REFERENCES seg.Usuarios(IdUsuario),
  CONSTRAINT CK_SeguimientosProspecto_Canal CHECK(Canal IN(N'WHATSAPP',N'EMAIL',N'INSTAGRAM',N'FACEBOOK')),
  CONSTRAINT CK_SeguimientosProspecto_Resultado CHECK(Resultado IN(N'CONTACTADO',N'RESPONDIO',N'INTERESADO',N'SIN_INTERES',N'NOTA')),
  CONSTRAINT CK_SeguimientosProspecto_Fecha CHECK(ProximaGestion IS NULL OR ProximaGestion>=CONVERT(date,SWITCHOFFSET(Fecha,'-06:00'))));
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'com.SeguimientosProspecto') AND name=N'IX_SeguimientosProspecto_Fecha')
 CREATE INDEX IX_SeguimientosProspecto_Fecha ON com.SeguimientosProspecto(IdProspecto,Fecha DESC,IdSeguimiento DESC) INCLUDE(ProximaGestion,Resultado,Canal);
GO
