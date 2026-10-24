/* DEV-42: fecha real de aplicación para nuevas recepciones; no reconstruye historia. */
SET XACT_ABORT ON;
GO
IF COL_LENGTH(N'cmp.RecepcionesCompra',N'FechaAplicacionMvp') IS NULL
 ALTER TABLE cmp.RecepcionesCompra ADD FechaAplicacionMvp datetimeoffset(0) NULL;
GO
