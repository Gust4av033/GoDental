/* DEV-20: códigos de maestros y cuentas del personal. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
USE [GoDental];
GO
IF COL_LENGTH(N'seg.Usuarios',N'CodigoEmpleado') IS NULL
 ALTER TABLE seg.Usuarios ADD CodigoEmpleado AS
 (CONVERT(nvarchar(14),N'EMP-'+RIGHT(N'0000000000'+CONVERT(nvarchar(10),IdUsuario),10))) PERSISTED;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'seg.Usuarios') AND name=N'UX_Usuarios_CodigoEmpleado')
 CREATE UNIQUE INDEX UX_Usuarios_CodigoEmpleado ON seg.Usuarios(CodigoEmpleado);
GO
INSERT INTO sis.SecuenciasDocumento(TipoDocumento,Prefijo,UltimoNumero,Longitud,ReinicioAnual)
 SELECT Tipo,Prefijo,0,10,0 FROM (VALUES
 (N'MAESTRO_CLIENTE',N'CLI-'),(N'MAESTRO_PRODUCTO',N'PRD-'),
 (N'MAESTRO_PROVEEDOR',N'PROV-'),(N'MAESTRO_CAJA',N'CAJ-')) s(Tipo,Prefijo)
 WHERE NOT EXISTS(SELECT 1 FROM sis.SecuenciasDocumento WHERE TipoDocumento=s.Tipo);
GO
CREATE OR ALTER PROCEDURE sis.sp_SiguienteCodigoMaestro
 @Tipo nvarchar(40), @Codigo nvarchar(60) OUTPUT
AS
BEGIN
 SET NOCOUNT ON;
 SET XACT_ABORT ON;
 IF @@TRANCOUNT=0 THROW 53501,N'El código se asigna dentro de la transacción de alta.',1;
 IF @Tipo NOT IN(N'MAESTRO_CLIENTE',N'MAESTRO_PRODUCTO',N'MAESTRO_PROVEEDOR',N'MAESTRO_CAJA') OR @Tipo IS NULL
  THROW 53501,N'Tipo de código no permitido.',1;
 DECLARE @Disponible bit=0;
 WHILE @Disponible=0
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM sis.SecuenciasDocumento WITH(UPDLOCK,HOLDLOCK)
    WHERE TipoDocumento=@Tipo AND Longitud=10 AND ReinicioAnual=0 AND UltimoNumero<9999999999
      AND Prefijo=CASE @Tipo WHEN N'MAESTRO_CLIENTE' THEN N'CLI-' WHEN N'MAESTRO_PRODUCTO' THEN N'PRD-' WHEN N'MAESTRO_PROVEEDOR' THEN N'PROV-' ELSE N'CAJ-' END)
   THROW 53502,N'La secuencia de códigos está agotada o requiere revisar su configuración.',1;
  EXEC sis.sp_SiguienteCorrelativo @TipoDocumento=@Tipo,@NumeroDocumento=@Codigo OUTPUT;
  IF @Tipo=N'MAESTRO_CLIENTE' AND NOT EXISTS(SELECT 1 FROM cli.Clientes WITH(UPDLOCK,HOLDLOCK) WHERE Codigo=@Codigo) SET @Disponible=1;
  IF @Tipo=N'MAESTRO_PRODUCTO' AND NOT EXISTS(SELECT 1 FROM inv.Productos WITH(UPDLOCK,HOLDLOCK) WHERE SKU=@Codigo) SET @Disponible=1;
  IF @Tipo=N'MAESTRO_PROVEEDOR' AND NOT EXISTS(SELECT 1 FROM cmp.Proveedores WITH(UPDLOCK,HOLDLOCK) WHERE Codigo=@Codigo) SET @Disponible=1;
  IF @Tipo=N'MAESTRO_CAJA' AND NOT EXISTS(SELECT 1 FROM ven.Cajas WITH(UPDLOCK,HOLDLOCK) WHERE Codigo=@Codigo) SET @Disponible=1;
 END;
END;
GO
