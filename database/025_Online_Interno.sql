/* DEV-46: gestión manual interna, sin comunicaciones externas. */
SET XACT_ABORT ON;
GO
IF COL_LENGTH(N'com.Prospectos',N'RowVersion') IS NULL
 ALTER TABLE com.Prospectos ADD RowVersion rowversion NOT NULL,Distrito nvarchar(100) NULL,
 SitioWebPublico nvarchar(1000) NULL,DominioWeb nvarchar(253) NULL,FechaRegistro datetimeoffset(0) NULL,IdVentaConversion bigint NULL;
IF COL_LENGTH(N'com.ListaNoContactar',N'RowVersion') IS NULL
 ALTER TABLE com.ListaNoContactar ADD RowVersion rowversion NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Prospecto_VentaConversion')
 ALTER TABLE com.Prospectos ADD CONSTRAINT FK_Prospecto_VentaConversion FOREIGN KEY(IdVentaConversion) REFERENCES ven.Ventas(IdVenta);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'com.Prospectos') AND name=N'IX_Prospectos_DominioWeb')
 CREATE INDEX IX_Prospectos_DominioWeb ON com.Prospectos(DominioWeb) WHERE DominioWeb IS NOT NULL;
GO
CREATE OR ALTER FUNCTION com.fn_ClaveOnline(@valor nvarchar(1000),@tipo nvarchar(20)) RETURNS nvarchar(1000) AS
BEGIN
 DECLARE @s nvarchar(1000)=LOWER(LTRIM(RTRIM(@valor)));
 IF @s IS NULL OR @s=N'' RETURN NULL;
 IF @tipo=N'TELEFONO'
 BEGIN
  DECLARE @internacional bit=CASE WHEN LEFT(@s,1)=N'+' THEN 1 ELSE 0 END;
  SET @s=REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@s,N' ',N''),N'-',N''),N'(',N''),N')',N''),N'+',N''),NCHAR(160),N'');
  IF @s LIKE N'%[^0-9]%' COLLATE Latin1_General_100_BIN2 OR LEN(@s)<7 OR LEN(@s)>15 RETURN NULL;
  IF LEN(@s)=8 AND @internacional=0 SET @s=N'503'+@s;
  RETURN N'+'+@s;
 END;
 IF @tipo IN(N'INSTAGRAM',N'FACEBOOK')
 BEGIN
  IF LEFT(@s,8)=N'https://' SET @s=SUBSTRING(@s,9,1000);
  ELSE IF LEFT(@s,7)=N'http://' SET @s=SUBSTRING(@s,8,1000);
  IF LEFT(@s,4)=N'www.' SET @s=SUBSTRING(@s,5,1000);
  IF @tipo=N'FACEBOOK' AND LEFT(@s,2)=N'm.' SET @s=SUBSTRING(@s,3,1000);
  WHILE RIGHT(@s,1)=N'/' SET @s=LEFT(@s,LEN(@s)-1);
  IF LEFT(@s,1)=N'@' SET @s=SUBSTRING(@s,2,1000);
  IF CHARINDEX(N'/',@s)=0 SET @s=LOWER(@tipo)+N'.com/'+@s;
 END;
 IF @tipo=N'TEXTO'
 BEGIN
  SET @s=REPLACE(REPLACE(REPLACE(@s,NCHAR(160),N' '),NCHAR(9),N' '),NCHAR(10),N' ');
  WHILE CHARINDEX(N'  ',@s)>0 SET @s=REPLACE(@s,N'  ',N' ');
 END;
 RETURN NULLIF(@s,N'');
END;
GO
IF NOT EXISTS(SELECT 1 FROM seg.Permisos WHERE Codigo=N'COMERCIAL.EXCLUSIONES')
 INSERT INTO seg.Permisos(Codigo,Nombre,Modulo,Descripcion) VALUES(N'COMERCIAL.EXCLUSIONES',N'Registrar exclusiones comerciales',N'Comercial',N'Bloquear prospectos y registrar/reactivar no contactar; no permite levantar exclusiones.');
INSERT INTO seg.RolPermisos(IdRol,IdPermiso,Concedido)
 SELECT r.IdRol,p.IdPermiso,1 FROM seg.Roles r CROSS JOIN seg.Permisos p WHERE r.Nombre=N'Administrador' AND p.Codigo=N'COMERCIAL.EXCLUSIONES'
 AND NOT EXISTS(SELECT 1 FROM seg.RolPermisos rp WHERE rp.IdRol=r.IdRol AND rp.IdPermiso=p.IdPermiso);
GO
