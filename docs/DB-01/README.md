# DB-01 — Modelo de Datos y Diccionario SQL

**Versión vigente:** v0.1

Ampliación versionada de servidor: [DEV-51](../DEV-51-Servidor-Identidad.md) define antes de
implementar **027_Servidor_Identidad.sql**: seg.SesionesServidor, cuatro procedimientos de
autenticación/sesiones y rol EXECUTE limitado. Previsto 82 tablas / 8 vistas / 22 procedimientos.
Es identidad interna GoDental, no el modelo fiscal externo pendiente de contratos.

Propuesta fiscal posterior a 026: [FIS-01 v0.2, modelo y migraciones](../FIS-01/Modelo-y-Migraciones.md).
Modelo fiscal PREPARADO, sin DDL de emisión aprobado; 027 es identidad interna de DEV-51.
Preservar solicitudes/revisión y tablas históricas;
nuevos snapshots/artefactos/intentos/eventos se definirán con contratos oficiales vigentes antes
de implementar. No reinterpretar ANULADO interno como invalidación MH ni guardar secretos en
ValorProtegido. El diseño no cambia las 26 migraciones existentes.

DB-01 define la estructura formal de datos de GoDental. La transcripción histórica completa se conserva en `../DB-01.md` y el documento Word original en `../referencias/GoDental_DB-01_ModeloDatos_DiccionarioSQL_v0.1(1).docx`.

La baseline ejecutable 001–008 y las migraciones posteriores viven en `../../database/`. Las ampliaciones se documentan antes de implementarlas: [DEV-03](../DEV-03-Inventario-operativo.md) define 010 y [DEV-09](../DEV-09-Conteos.md) define 011 sobre las tablas de conteos existentes. No inventar tablas, columnas o procedimientos desde C# sin una definición versionada.

DEV-33 documenta la ampliación 017 sobre las tablas de compras existentes: estado parcial, snapshots de líneas, versión/cambio real/costo completo de recepción y fecha de fabricación. No agrega tablas ni procedimientos y mantiene la separación entre compra directa e importación pendiente de diseño 2.1B.

DEV-37 define la ampliación 018 de importaciones: cierre/cambio/gastos totales, anulación de gastos, presupuestos por línea de OC y total de gasto recibido. Conserva 76 tablas, 8 vistas y 18 procedimientos; no reconstruye costos históricos.

DEV-38 define 019 antes de su SQL: versión, comprobante, tipo anulación, turno/equipo,
autorización/compensaciones y fecha de aplicación en devoluciones; enlace detalle→Kardex;
raíz central de evidencias en Configuracion y conservación de entrega histórica al cancelar pedidos.
Conserva 76 tablas, 8 vistas y 18 procedimientos. No reconstruye compensaciones históricas.


DEV-46 define 025 antes de implementar: extensión de Prospectos con versión, distrito, sitio/dominio,
fecha nullable para nuevas altas y venta de conversión; versión de ListaNoContactar y función de
comparación de identificadores heredados sin reescribirlos. Conserva 79 tablas / 8 vistas / 18
procedimientos; agrega una función, no un procedimiento. Sin reconstruir conversiones/fechas históricas.

DEV-47 define 026 antes de implementar: SegmentosProspecto y SeguimientosProspecto; versiones
de zonas/plantillas/campañas y snapshots de criterios, contenido y destinatarios. Estado final:
81 tablas / 8 vistas / 18 procedimientos, más la función de 025. No reescribe datos históricos.
