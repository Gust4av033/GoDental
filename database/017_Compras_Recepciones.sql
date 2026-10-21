/* DEV-33 · Compras directas y recepciones con costo completo. Aditiva e idempotente. */
SET XACT_ABORT ON;
GO
IF COL_LENGTH(N'sis.Configuracion',N'RowVersion') IS NULL
    THROW 53701,N'Aplica primero la migración 016.',1;
GO
IF COL_LENGTH(N'cmp.OrdenCompraDetalle',N'SKU') IS NULL
    ALTER TABLE cmp.OrdenCompraDetalle ADD SKU nvarchar(60) NULL, Nombre nvarchar(200) NULL;
GO
UPDATE d SET SKU=ISNULL(d.SKU,p.SKU),Nombre=ISNULL(d.Nombre,p.Nombre)
FROM cmp.OrdenCompraDetalle d JOIN inv.Productos p ON p.IdProducto=d.IdProducto
WHERE d.SKU IS NULL OR d.Nombre IS NULL;
GO
IF COL_LENGTH(N'cmp.RecepcionesCompra',N'RowVersion') IS NULL
    ALTER TABLE cmp.RecepcionesCompra ADD RowVersion rowversion NOT NULL;
GO
IF COL_LENGTH(N'cmp.RecepcionesCompra',N'TipoCambioReal') IS NULL
    ALTER TABLE cmp.RecepcionesCompra ADD TipoCambioReal decimal(18,8) NULL,
        CostosCompletos bit NOT NULL CONSTRAINT DF_RecepcionesCompra_CostosCompletos DEFAULT(0);
GO
IF COL_LENGTH(N'cmp.RecepcionCompraDetalle',N'FechaFabricacion') IS NULL
    ALTER TABLE cmp.RecepcionCompraDetalle ADD FechaFabricacion date NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'cmp.OrdenesCompra') AND name=N'CK_OrdenesCompra_Estado_017')
BEGIN
    ALTER TABLE cmp.OrdenesCompra DROP CONSTRAINT CK_OrdenesCompra_Estado;
    ALTER TABLE cmp.OrdenesCompra WITH CHECK ADD CONSTRAINT CK_OrdenesCompra_Estado_017 CHECK
        (Estado IN(N'BORRADOR',N'ENVIADA',N'CONFIRMADA',N'EN_PRODUCCION',N'DESPACHADA',N'RECIBIDA_PARCIAL',N'RECIBIDA',N'CANCELADA'));
END;
GO

/* Derivar estado de OC históricas con cantidades recibidas sin reescribirlas. */
UPDATE o SET Estado=CASE WHEN EXISTS(SELECT 1 FROM cmp.OrdenCompraDetalle d WHERE d.IdOrdenCompra=o.IdOrdenCompra AND d.CantidadRecibida<d.Cantidad)
    THEN N'RECIBIDA_PARCIAL' ELSE N'RECIBIDA' END
FROM cmp.OrdenesCompra o
WHERE o.Estado<>N'CANCELADA' AND EXISTS(SELECT 1 FROM cmp.OrdenCompraDetalle d WHERE d.IdOrdenCompra=o.IdOrdenCompra AND d.CantidadRecibida>0)
  AND o.Estado<>CASE WHEN EXISTS(SELECT 1 FROM cmp.OrdenCompraDetalle d WHERE d.IdOrdenCompra=o.IdOrdenCompra AND d.CantidadRecibida<d.Cantidad)
    THEN N'RECIBIDA_PARCIAL' ELSE N'RECIBIDA' END;
GO
