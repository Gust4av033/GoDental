/* DEV-32: versión optimista de los parámetros existentes. Aditiva e idempotente. */
SET XACT_ABORT ON;
GO
IF COL_LENGTH(N'sis.OperacionesMvp', N'EstadoResolucion') IS NULL
    THROW 53601, N'Aplica primero la migración 015.', 1;
GO
IF COL_LENGTH(N'sis.Configuracion', N'RowVersion') IS NULL
    ALTER TABLE sis.Configuracion ADD RowVersion ROWVERSION NOT NULL;
GO

/* Los códigos históricos se conservan; sus nombres describen los tramos vigentes. */
UPDATE seg.Permisos SET Nombre=N'Descuento básico', Descripcion=N'Aplicar descuentos hasta el umbral vigente de justificación.' WHERE Codigo=N'VENTAS.DESCUENTO_7';
UPDATE seg.Permisos SET Nombre=N'Descuento con motivo', Descripcion=N'Aplicar descuentos sobre el umbral vigente dejando justificación.' WHERE Codigo=N'VENTAS.DESCUENTO_MAYOR_7';
UPDATE seg.Permisos SET Nombre=N'Autorización de descuento', Descripcion=N'Autorizar descuentos sobre el umbral administrativo vigente con credencial administrativa.' WHERE Codigo=N'VENTAS.DESCUENTO_MAYOR_35';
GO
