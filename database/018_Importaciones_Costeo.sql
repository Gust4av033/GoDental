/* DEV-37 · 2.1B. Aditiva: no reconstruye costos históricos ni genera stock. */
SET XACT_ABORT ON;
GO
IF COL_LENGTH(N'cmp.RecepcionesCompra',N'TipoCambioReal') IS NULL
    THROW 53801,N'Aplica primero 017.',1;
GO
IF COL_LENGTH(N'cmp.Importaciones',N'CostosCerrados') IS NULL
    ALTER TABLE cmp.Importaciones ADD CostosCerrados bit NOT NULL CONSTRAINT DF_Importaciones_CostosCerrados DEFAULT(0),
        TipoCambioReal decimal(18,8) NULL, TotalGastosUSD decimal(18,2) NULL,
        FechaCierre datetimeoffset(0) NULL, CerradoPor int NULL REFERENCES seg.Usuarios(IdUsuario);
GO
IF COL_LENGTH(N'cmp.GastosImportacion',N'Anulado') IS NULL
    ALTER TABLE cmp.GastosImportacion ADD Anulado bit NOT NULL CONSTRAINT DF_GastosImportacion_Anulado DEFAULT(0);
GO
IF COL_LENGTH(N'cmp.OrdenCompraDetalle',N'CostoImportacionUSD') IS NULL
    ALTER TABLE cmp.OrdenCompraDetalle ADD CostoImportacionUSD decimal(18,6) NULL, GastoImportacionUSD decimal(18,2) NULL;
GO
IF COL_LENGTH(N'cmp.RecepcionCompraDetalle',N'GastoImportacionTotalUSD') IS NULL
    ALTER TABLE cmp.RecepcionCompraDetalle ADD GastoImportacionTotalUSD decimal(18,2) NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_Importaciones_Cierre_018')
    ALTER TABLE cmp.Importaciones WITH CHECK ADD CONSTRAINT CK_Importaciones_Cierre_018 CHECK
        (CostosCerrados=0 OR (TipoCambioReal IS NOT NULL AND TotalGastosUSD IS NOT NULL AND TipoCambioReal>0 AND TotalGastosUSD>=0 AND FechaCierre IS NOT NULL AND CerradoPor IS NOT NULL));
GO
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_OrdenCompraDetalle_Importacion_018')
    ALTER TABLE cmp.OrdenCompraDetalle WITH CHECK ADD CONSTRAINT CK_OrdenCompraDetalle_Importacion_018 CHECK
        ((CostoImportacionUSD IS NULL AND GastoImportacionUSD IS NULL) OR (CostoImportacionUSD IS NOT NULL AND GastoImportacionUSD IS NOT NULL AND CostoImportacionUSD>=0 AND GastoImportacionUSD>=0));
GO
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_RecepcionCompraDetalle_Gasto_018')
    ALTER TABLE cmp.RecepcionCompraDetalle WITH CHECK ADD CONSTRAINT CK_RecepcionCompraDetalle_Gasto_018 CHECK (GastoImportacionTotalUSD IS NULL OR GastoImportacionTotalUSD>=0);
GO

CREATE OR ALTER VIEW cmp.vwImportacionesSeguimiento
AS
SELECT
    i.IdImportacion,
    i.IdOrdenCompra,
    oc.NumeroOrden,
    pr.Nombre AS Proveedor,
    i.Courier,
    i.NumeroGuia,
    i.Estado,
    i.FechaPedido,
    i.FechaDespacho,
    i.FechaEstimada,
    i.FechaRecepcion,
    CASE
        WHEN i.FechaRecepcion IS NOT NULL THEN 0
        WHEN i.FechaEstimada IS NULL THEN NULL
        ELSE DATEDIFF(DAY, CONVERT(date, SYSDATETIMEOFFSET()), i.FechaEstimada)
    END AS DiasHastaEstimado,
    ISNULL(g.TotalGastosUSD, 0) AS GastosImportacionUSD,
    ev.FechaEvento AS FechaUltimoEvento,
    ev.TipoEvento AS UltimoEvento,
    ev.Descripcion AS UltimoEventoDescripcion
FROM cmp.Importaciones i
INNER JOIN cmp.OrdenesCompra oc ON oc.IdOrdenCompra = i.IdOrdenCompra
INNER JOIN cmp.Proveedores pr ON pr.IdProveedor = oc.IdProveedor
OUTER APPLY
(
    SELECT SUM(gi.MontoUSD) AS TotalGastosUSD
    FROM cmp.GastosImportacion gi
    WHERE gi.IdImportacion = i.IdImportacion AND gi.Anulado=0
) g
OUTER APPLY
(
    SELECT TOP (1)
        e.FechaEvento,
        e.TipoEvento,
        e.Descripcion
    FROM cmp.EventosImportacion e
    WHERE e.IdImportacion = i.IdImportacion
    ORDER BY e.FechaEvento DESC, e.IdEvento DESC
) ev;
GO
