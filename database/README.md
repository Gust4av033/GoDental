# Evolución de SQL Server

DB-01 v0.1 es el modelo de referencia, todavía en revisión. No hay scripts ejecutables en este directorio.

Plan de DB-01, sección 11:

| Script previsto | Contenido |
| --- | --- |
| 001_Core.sql | Base, esquemas, seguridad, sistema, catálogos |
| 002_Inventario.sql | Productos, lotes, saldos, kardex, conteos |
| 003_Compras.sql | Proveedores, OC, importación, recepción, costeo |
| 004_ClientesVentas.sql | Clientes, caja, precios, ventas, crédito, devoluciones |
| 005_Fiscal_Base.sql | Cimientos fiscales desacoplados |
| 006_Comercial_Base.sql | Cimientos de prospectos y campañas |
| 007_Seed.sql | Roles, permisos, catálogos y parámetros |
| 008_ViewsProcedures.sql | Operaciones críticas y reportes validados |

Crear los scripts después de revisar el diseño. No generar ni incluir backups, datos reales o credenciales. Antes de producción se requieren pruebas de concurrencia, rollback, correlativos, integridad de pagos y restauración de backups.
