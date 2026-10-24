/* DEV-44: preparación interna, sin emisión/transmisión fiscal. */
SET XACT_ABORT ON;
GO
IF COL_LENGTH(N'fis.DocumentosFiscales',N'RevisionInterna') IS NULL
 ALTER TABLE fis.DocumentosFiscales ADD RowVersion rowversion NOT NULL,
 RevisionInterna nvarchar(20) NOT NULL CONSTRAINT DF_Fiscal_Revision DEFAULT N'SIN_REVISAR',
 ObservacionRevision nvarchar(500) NULL,FechaRevision datetimeoffset(0) NULL,
 RevisadoPor int NULL,HuellaRevision char(64) NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Fiscal_Revision_Usuario')
 ALTER TABLE fis.DocumentosFiscales ADD CONSTRAINT FK_Fiscal_Revision_Usuario FOREIGN KEY(RevisadoPor) REFERENCES seg.Usuarios(IdUsuario);
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_Fiscal_Revision_Interna')
 ALTER TABLE fis.DocumentosFiscales ADD CONSTRAINT CK_Fiscal_Revision_Interna CHECK(
 (RevisionInterna=N'SIN_REVISAR' AND FechaRevision IS NULL AND RevisadoPor IS NULL AND HuellaRevision IS NULL AND ObservacionRevision IS NULL) OR
 (RevisionInterna IN(N'REVISADO',N'OBSERVADO') AND FechaRevision IS NOT NULL AND RevisadoPor IS NOT NULL AND HuellaRevision IS NOT NULL AND LEN(HuellaRevision)=64 AND HuellaRevision NOT LIKE '%[^0-9A-F]%' AND ObservacionRevision IS NOT NULL AND LEN(LTRIM(RTRIM(ObservacionRevision)))>0));
GO
IF NOT EXISTS(SELECT 1 FROM seg.Permisos WHERE Codigo=N'FISCAL.REVISAR_INTERNO')
 INSERT INTO seg.Permisos(Codigo,Nombre,Modulo,Descripcion) VALUES(N'FISCAL.REVISAR_INTERNO',N'Revisar preparación fiscal interna',N'Fiscal',N'Registrar revisión interna sin emitir, firmar ni transmitir DTE.');
INSERT INTO seg.RolPermisos(IdRol,IdPermiso,Concedido)
 SELECT r.IdRol,p.IdPermiso,1 FROM seg.Roles r CROSS JOIN seg.Permisos p
 WHERE r.Nombre=N'Administrador' AND p.Codigo=N'FISCAL.REVISAR_INTERNO'
 AND NOT EXISTS(SELECT 1 FROM seg.RolPermisos rp WHERE rp.IdRol=r.IdRol AND rp.IdPermiso=p.IdPermiso);
GO
