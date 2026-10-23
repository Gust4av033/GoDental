/* DEV-38: posventa autorizada. Aditiva e idempotente; no altera ventas/devoluciones históricas. */
SET XACT_ABORT ON;
GO
IF COL_LENGTH(N'ven.DevolucionesVenta',N'EsAnulacion') IS NULL
BEGIN
 ALTER TABLE ven.DevolucionesVenta ADD
  EsAnulacion bit NOT NULL CONSTRAINT DF_DevolucionesVenta_EsAnulacion DEFAULT(0),
  Comprobante nvarchar(250) NULL,
  ReduccionDeuda decimal(18,2) NOT NULL CONSTRAINT DF_DevolucionesVenta_Deuda DEFAULT(0),
  ReintegroEfectivo decimal(18,2) NOT NULL CONSTRAINT DF_DevolucionesVenta_Efectivo DEFAULT(0),
  FechaAplicacion datetimeoffset(0) NULL,
  MotivoResolucion nvarchar(500) NULL,
  IdTurno bigint NULL,
  IdEquipo int NULL,
  RowVersion rowversion NOT NULL;
END;
GO
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DevolucionesVenta_Turno')
 ALTER TABLE ven.DevolucionesVenta ADD CONSTRAINT FK_DevolucionesVenta_Turno FOREIGN KEY(IdTurno) REFERENCES ven.TurnosCaja(IdTurno);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DevolucionesVenta_Equipo')
 ALTER TABLE ven.DevolucionesVenta ADD CONSTRAINT FK_DevolucionesVenta_Equipo FOREIGN KEY(IdEquipo) REFERENCES sis.EquiposTrabajo(IdEquipo);
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_DevolucionesVenta_Compensacion')
 ALTER TABLE ven.DevolucionesVenta ADD CONSTRAINT CK_DevolucionesVenta_Compensacion CHECK(
  ReduccionDeuda>=0 AND ReintegroEfectivo>=0 AND
  (FechaAplicacion IS NULL OR (Estado=N'APLICADA' AND FechaAutorizacion IS NOT NULL AND FechaAplicacion>=FechaAutorizacion
    AND ReduccionDeuda+ReintegroEfectivo=MontoReintegro AND (ReintegroEfectivo=0 OR IdTurno IS NOT NULL))));
GO
IF COL_LENGTH(N'ven.DevolucionDetalle',N'IdMovimiento') IS NULL
 ALTER TABLE ven.DevolucionDetalle ADD IdMovimiento bigint NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DevolucionDetalle_Movimiento')
 ALTER TABLE ven.DevolucionDetalle ADD CONSTRAINT FK_DevolucionDetalle_Movimiento FOREIGN KEY(IdMovimiento) REFERENCES inv.MovimientosInventario(IdMovimiento);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ven.DevolucionDetalle') AND name=N'IX_DevolucionDetalle_Venta')
 CREATE INDEX IX_DevolucionDetalle_Venta ON ven.DevolucionDetalle(IdVentaDetalle,IdDevolucion) INCLUDE(Cantidad,MontoReintegrado);
GO

IF NOT EXISTS(SELECT 1 FROM sis.Configuracion WHERE Clave=N'Archivos.RaizEvidencias')
 INSERT INTO sis.Configuracion(Clave,Valor,TipoDato,Descripcion,EditableUI)
 VALUES(N'Archivos.RaizEvidencias',N'',N'STRING',N'Carpeta central gestionada para evidencias; misma ruta UNC en todas las laptops. Respaldar junto con la BD.',1);
GO

-- Conservar el hecho histórico de entrega al anular después de ENTREGADO.
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_PedidosMvp_Entrega' AND definition LIKE N'%INCIDENCIA%')
BEGIN
 ALTER TABLE ven.PedidosMvp DROP CONSTRAINT CK_PedidosMvp_Entrega;
 ALTER TABLE ven.PedidosMvp ADD CONSTRAINT CK_PedidosMvp_Entrega CHECK(
  (FechaEntrega IS NULL AND EntregadoPor IS NULL AND Estado<>N'ENTREGADO') OR
  (FechaEntrega IS NOT NULL AND EntregadoPor IS NOT NULL AND Estado IN(N'ENTREGADO',N'CANCELADO',N'INCIDENCIA')));
END;
GO
