# DB-01 — Modelo de Datos y Diccionario SQL

**Versión vigente:** v0.1

DB-01 define la estructura formal de datos de GoDental. La transcripción histórica completa se conserva en `../DB-01.md` y el documento Word original en `../referencias/GoDental_DB-01_ModeloDatos_DiccionarioSQL_v0.1(1).docx`.

La baseline ejecutable 001–008 y las migraciones posteriores viven en `../../database/`. Las ampliaciones se documentan antes de implementarlas: [DEV-03](../DEV-03-Inventario-operativo.md) define 010 y [DEV-09](../DEV-09-Conteos.md) define 011 sobre las tablas de conteos existentes. No inventar tablas, columnas o procedimientos desde C# sin una definición versionada.

DEV-33 documenta la ampliación 017 sobre las tablas de compras existentes: estado parcial, snapshots de líneas, versión/cambio real/costo completo de recepción y fecha de fabricación. No agrega tablas ni procedimientos y mantiene la separación entre compra directa e importación pendiente de diseño 2.1B.

DEV-37 define la ampliación 018 de importaciones: cierre/cambio/gastos totales, anulación de gastos, presupuestos por línea de OC y total de gasto recibido. Conserva 76 tablas, 8 vistas y 18 procedimientos; no reconstruye costos históricos.

DEV-38 define 019 antes de su SQL: versión, comprobante, tipo anulación, turno/equipo,
autorización/compensaciones y fecha de aplicación en devoluciones; enlace detalle→Kardex;
raíz central de evidencias en Configuracion y conservación de entrega histórica al cancelar pedidos.
Conserva 76 tablas, 8 vistas y 18 procedimientos. No reconstruye compensaciones históricas.
