/* DEV-31: estados terminales del ledger; aplicar después de 014. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
USE [GoDental];
GO
IF OBJECT_ID(N'sis.OperacionesMvp',N'U') IS NULL
 THROW 53601,N'Instala primero las migraciones anteriores del MVP.',1;
GO
IF COL_LENGTH(N'sis.OperacionesMvp',N'EstadoResolucion') IS NULL
 ALTER TABLE sis.OperacionesMvp ADD EstadoResolucion nvarchar(24) NOT NULL
 CONSTRAINT DF_OperacionesMvp_EstadoResolucion DEFAULT N'CONFIRMADA' WITH VALUES;
GO
IF COL_LENGTH(N'sis.OperacionesMvp',N'HashRecuperacion') IS NULL
 ALTER TABLE sis.OperacionesMvp ADD HashRecuperacion binary(32) NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'sis.OperacionesMvp') AND name=N'CK_OperacionesMvp_Resolucion')
 ALTER TABLE sis.OperacionesMvp WITH CHECK ADD CONSTRAINT CK_OperacionesMvp_Resolucion CHECK
 ((EstadoResolucion=N'CONFIRMADA' AND EntidadId>0) OR (EstadoResolucion=N'RECHAZADA_DEFINITIVA' AND EntidadId=0));
GO
