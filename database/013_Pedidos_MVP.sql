/* DEV-17: pedidos ligeros, snapshots y eventos. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
USE [GoDental];
GO
IF OBJECT_ID(N'ven.PedidosMvp',N'U') IS NULL
CREATE TABLE ven.PedidosMvp(
 IdPedido bigint IDENTITY NOT NULL CONSTRAINT PK_PedidosMvp PRIMARY KEY,
 IdCliente bigint NOT NULL,ClienteSnapshot nvarchar(200) NOT NULL,Contacto nvarchar(150) NOT NULL,Telefono nvarchar(30) NOT NULL,
 Direccion nvarchar(500) NOT NULL,Departamento nvarchar(100) NULL,Municipio nvarchar(100) NULL,Referencia nvarchar(300) NULL,
 FechaPrevista date NULL,Responsable nvarchar(150) NULL,IdResponsable int NULL,CargoEntrega decimal(18,2) NOT NULL,
 IdVenta bigint NULL,Estado nvarchar(20) NOT NULL CONSTRAINT DF_PedidosMvp_Estado DEFAULT(N'RECIBIDO'),
 Fecha datetimeoffset(0) NOT NULL CONSTRAINT DF_PedidosMvp_Fecha DEFAULT SYSDATETIMEOFFSET(),
 FechaEntrega datetimeoffset(0) NULL,EntregadoPor int NULL,RowVersion rowversion NOT NULL,
 CONSTRAINT FK_PedidosMvp_Cliente FOREIGN KEY(IdCliente) REFERENCES cli.Clientes(IdCliente),
 CONSTRAINT FK_PedidosMvp_Venta FOREIGN KEY(IdVenta) REFERENCES ven.Ventas(IdVenta),
 CONSTRAINT FK_PedidosMvp_Responsable FOREIGN KEY(IdResponsable) REFERENCES seg.Usuarios(IdUsuario),
 CONSTRAINT FK_PedidosMvp_Entregado FOREIGN KEY(EntregadoPor) REFERENCES seg.Usuarios(IdUsuario),
 CONSTRAINT CK_PedidosMvp_Cargo CHECK(CargoEntrega>=0),
 CONSTRAINT CK_PedidosMvp_Estado CHECK(Estado IN(N'RECIBIDO',N'PREPARANDO',N'LISTO',N'EN_RUTA',N'ENTREGADO',N'CANCELADO',N'INCIDENCIA')),
 CONSTRAINT CK_PedidosMvp_Entrega CHECK((Estado=N'ENTREGADO' AND FechaEntrega IS NOT NULL AND EntregadoPor IS NOT NULL) OR (Estado<>N'ENTREGADO' AND FechaEntrega IS NULL AND EntregadoPor IS NULL)));
GO
IF OBJECT_ID(N'ven.PedidoLineasMvp',N'U') IS NULL
CREATE TABLE ven.PedidoLineasMvp(
 IdPedido bigint NOT NULL,IdProducto int NOT NULL,SKU nvarchar(60) NOT NULL,Nombre nvarchar(250) NOT NULL,Cantidad decimal(18,4) NOT NULL,
 CONSTRAINT PK_PedidoLineasMvp PRIMARY KEY(IdPedido,IdProducto),
 CONSTRAINT FK_PedidoLineasMvp_Pedido FOREIGN KEY(IdPedido) REFERENCES ven.PedidosMvp(IdPedido),
 CONSTRAINT FK_PedidoLineasMvp_Producto FOREIGN KEY(IdProducto) REFERENCES inv.Productos(IdProducto),
 CONSTRAINT CK_PedidoLineasMvp_Cantidad CHECK(Cantidad>0));
GO
IF OBJECT_ID(N'ven.PedidoEventosMvp',N'U') IS NULL
CREATE TABLE ven.PedidoEventosMvp(
 IdEvento bigint IDENTITY NOT NULL CONSTRAINT PK_PedidoEventosMvp PRIMARY KEY,
 IdPedido bigint NOT NULL,Fecha datetimeoffset(0) NOT NULL CONSTRAINT DF_PedidoEventosMvp_Fecha DEFAULT SYSDATETIMEOFFSET(),
 IdUsuario int NOT NULL,Estado nvarchar(20) NOT NULL,Motivo nvarchar(500) NOT NULL,
 CONSTRAINT FK_PedidoEventosMvp_Pedido FOREIGN KEY(IdPedido) REFERENCES ven.PedidosMvp(IdPedido),
 CONSTRAINT FK_PedidoEventosMvp_Usuario FOREIGN KEY(IdUsuario) REFERENCES seg.Usuarios(IdUsuario));
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ven.PedidosMvp') AND name=N'UX_PedidosMvp_Venta')
 CREATE UNIQUE INDEX UX_PedidosMvp_Venta ON ven.PedidosMvp(IdVenta) WHERE IdVenta IS NOT NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ven.PedidosMvp') AND name=N'IX_PedidosMvp_Estado')
 CREATE INDEX IX_PedidosMvp_Estado ON ven.PedidosMvp(Estado,IdPedido);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ven.PedidoEventosMvp') AND name=N'IX_PedidoEventosMvp_Pedido')
 CREATE INDEX IX_PedidoEventosMvp_Pedido ON ven.PedidoEventosMvp(IdPedido,IdEvento);
GO
