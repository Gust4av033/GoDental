/* DEV-39: distrito opcional y snapshot de entrega. Aditiva e idempotente.
   Sin reasignar departamentos/municipios históricos ni modificar solicitudes recuperables. */
SET XACT_ABORT ON;
GO
IF COL_LENGTH(N'cli.ClienteDirecciones',N'Distrito') IS NULL
 ALTER TABLE cli.ClienteDirecciones ADD Distrito nvarchar(100) NULL;
GO
IF COL_LENGTH(N'ven.PedidosMvp',N'Distrito') IS NULL
 ALTER TABLE ven.PedidosMvp ADD Distrito nvarchar(100) NULL;
GO
