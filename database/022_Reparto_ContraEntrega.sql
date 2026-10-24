/* DEV-41: custodia de reparto, entrega completa y rendición. Sin CxC comercial. */
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'ven.RepartosMvp',N'U') IS NULL
BEGIN
 CREATE TABLE ven.RepartosMvp(
 IdReparto bigint IDENTITY PRIMARY KEY, IdPedido bigint NOT NULL UNIQUE REFERENCES ven.PedidosMvp(IdPedido),
 IdCaja int NOT NULL REFERENCES ven.Cajas(IdCaja), IdAlmacen int NOT NULL REFERENCES inv.Almacenes(IdAlmacen),
 Responsable nvarchar(150) NOT NULL CHECK(LEN(LTRIM(RTRIM(Responsable)))>0),
 Estado nvarchar(30) NOT NULL CHECK(Estado IN(N'EN_RUTA',N'INCIDENCIA',N'PENDIENTE_RENDICION',N'RENDIDO',N'RETORNADO')),
 Total decimal(18,2) NOT NULL CHECK(Total>0), SnapshotCalculo nvarchar(max) NOT NULL CHECK(ISJSON(SnapshotCalculo)=1),
 ClienteSnapshot nvarchar(max) NOT NULL CHECK(ISJSON(ClienteSnapshot)=1),
 IdVenta bigint NULL REFERENCES ven.Ventas(IdVenta), Medio nvarchar(20) NULL CHECK(Medio IN(N'EFECTIVO',N'TRANSFERENCIA')),
 Referencia nvarchar(150) NULL, FechaSalida datetimeoffset(0) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
 IdUsuarioSalida int NOT NULL REFERENCES seg.Usuarios(IdUsuario), IdEquipoSalida int NOT NULL REFERENCES sis.EquiposTrabajo(IdEquipo),
 FechaEntrega datetimeoffset(0) NULL, FechaRetorno datetimeoffset(0) NULL, FechaRendicion datetimeoffset(0) NULL,
 IdUsuarioRendicion int NULL REFERENCES seg.Usuarios(IdUsuario), IdEquipoRendicion int NULL REFERENCES sis.EquiposTrabajo(IdEquipo),
 IdTurno bigint NULL REFERENCES ven.TurnosCaja(IdTurno), RowVersion rowversion,
 CONSTRAINT CK_RepartosMvp_Ciclo CHECK(
 (Estado IN(N'EN_RUTA',N'INCIDENCIA') AND IdVenta IS NULL AND Medio IS NULL AND FechaEntrega IS NULL AND FechaRetorno IS NULL AND FechaRendicion IS NULL) OR
 (Estado=N'RETORNADO' AND IdVenta IS NULL AND Medio IS NULL AND FechaEntrega IS NULL AND FechaRetorno IS NOT NULL AND FechaRendicion IS NULL) OR
 (Estado=N'PENDIENTE_RENDICION' AND IdVenta IS NOT NULL AND Medio IS NOT NULL AND FechaEntrega IS NOT NULL AND FechaRetorno IS NULL AND FechaRendicion IS NULL) OR
 (Estado=N'RENDIDO' AND IdVenta IS NOT NULL AND Medio IS NOT NULL AND FechaEntrega IS NOT NULL AND FechaRetorno IS NULL AND FechaRendicion IS NOT NULL AND IdUsuarioRendicion IS NOT NULL AND IdEquipoRendicion IS NOT NULL AND (Medio<>N'EFECTIVO' OR IdTurno IS NOT NULL)))
 );
 CREATE UNIQUE INDEX UX_RepartosMvp_Venta ON ven.RepartosMvp(IdVenta) WHERE IdVenta IS NOT NULL;
 CREATE INDEX IX_RepartosMvp_Estado ON ven.RepartosMvp(Estado,IdReparto);
END;
GO
IF OBJECT_ID(N'ven.RepartoLineasMvp',N'U') IS NULL
BEGIN
 CREATE TABLE ven.RepartoLineasMvp(
 IdLinea bigint IDENTITY PRIMARY KEY, IdReparto bigint NOT NULL REFERENCES ven.RepartosMvp(IdReparto),
 IdProducto int NOT NULL REFERENCES inv.Productos(IdProducto), IdLote bigint NULL REFERENCES inv.LotesProducto(IdLote),
 Cantidad decimal(18,4) NOT NULL CHECK(Cantidad>0), CostoUnitario decimal(18,6) NOT NULL CHECK(CostoUnitario>=0),
 Calculo nvarchar(max) NOT NULL CHECK(ISJSON(Calculo)=1));
 CREATE INDEX IX_RepartoLineasMvp_Reparto ON ven.RepartoLineasMvp(IdReparto,IdProducto);
END;
GO
INSERT INTO seg.Permisos(Codigo,Nombre,Modulo,Descripcion)
 SELECT v.Codigo,v.Nombre,N'Ventas',v.Nombre FROM(VALUES
 (N'VENTAS.REPARTO_GESTIONAR',N'Despachar y resolver reparto contra entrega'),
 (N'CAJA.REPARTO_RENDIR',N'Recibir y verificar rendiciones de reparto'))v(Codigo,Nombre)
 WHERE NOT EXISTS(SELECT 1 FROM seg.Permisos p WHERE p.Codigo=v.Codigo);
INSERT INTO seg.RolPermisos(IdRol,IdPermiso,Concedido)
 SELECT r.IdRol,p.IdPermiso,1 FROM seg.Roles r CROSS JOIN seg.Permisos p
 WHERE r.Nombre=N'Administrador' AND p.Codigo IN(N'VENTAS.REPARTO_GESTIONAR',N'CAJA.REPARTO_RENDIR')
 AND NOT EXISTS(SELECT 1 FROM seg.RolPermisos rp WHERE rp.IdRol=r.IdRol AND rp.IdPermiso=p.IdPermiso);
IF NOT EXISTS(SELECT 1 FROM cat.MetodosPago WHERE Codigo=N'EFECTIVO_REPARTO')
 INSERT INTO cat.MetodosPago(Codigo,Nombre,AfectaCajaEfectivo,RequiereReferencia,Activo)
 VALUES(N'EFECTIVO_REPARTO',N'Efectivo en reparto (rendición separada)',0,0,1);
GO
