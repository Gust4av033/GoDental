/* ============================================================================
   GoDental - 007_Seed.sql
   Datos iniciales / catálogos base / permisos / configuración operativa

   Dependencias:
     001_Core.sql
     002_Inventario.sql
     003_Compras.sql
     004_ClientesVentas.sql
     005_Fiscal_Base.sql
     006_Comercial_Base.sql

   Notas:
   - Script idempotente: puede ejecutarse más de una vez sin duplicar semillas.
   - NO crea usuarios con contraseñas por defecto. El primer administrador debe
     crearse desde el bootstrap seguro de la aplicación / procedimiento dedicado.
   - NO registra PCs: los equipos se registrarán con identificador real desde C#.
   - NO carga datos fiscales reales de la empresa; se deja un registro editable.
   - NO inserta productos, proveedores ni clientes reales.
   - NO usa triggers.
   ============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF DB_ID(N'GoDental') IS NULL
    THROW 50700, N'La base de datos GoDental no existe. Ejecute primero 001_Core.sql.', 1;
GO

USE GoDental;
GO

/* ==========================================================================
   0. VALIDACIÓN DE DEPENDENCIAS
   ========================================================================== */

IF OBJECT_ID(N'seg.Roles', N'U') IS NULL OR OBJECT_ID(N'seg.Permisos', N'U') IS NULL
    THROW 50701, N'Falta el esquema de seguridad. Ejecute primero 001_Core.sql.', 1;

IF OBJECT_ID(N'inv.Almacenes', N'U') IS NULL
    THROW 50702, N'Falta Inventario. Ejecute primero 002_Inventario.sql.', 1;

IF OBJECT_ID(N'cmp.Proveedores', N'U') IS NULL
    THROW 50703, N'Falta Compras. Ejecute primero 003_Compras.sql.', 1;

IF OBJECT_ID(N'ven.Cajas', N'U') IS NULL OR OBJECT_ID(N'ven.ListasPrecio', N'U') IS NULL
    THROW 50704, N'Falta Clientes/Ventas. Ejecute primero 004_ClientesVentas.sql.', 1;

IF OBJECT_ID(N'fis.ConfiguracionFiscal', N'U') IS NULL
    THROW 50705, N'Falta Fiscal Base. Ejecute primero 005_Fiscal_Base.sql.', 1;

IF OBJECT_ID(N'com.Prospectos', N'U') IS NULL
    THROW 50706, N'Falta Comercial Base. Ejecute primero 006_Comercial_Base.sql.', 1;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    /* ======================================================================
       1. ROLES BASE
       ====================================================================== */

    UPDATE r
       SET r.Descripcion = v.Descripcion,
           r.EsSistema   = 1,
           r.Activo      = 1
    FROM seg.Roles r
    INNER JOIN
    (
        VALUES
            (N'Administrador', N'Acceso total a la administración y operación de GoDental.'),
            (N'Vendedor',      N'Operación comercial: caja, clientes, cotizaciones y ventas con permisos limitados.')
    ) v(Nombre, Descripcion)
        ON v.Nombre = r.Nombre;

    INSERT INTO seg.Roles (Nombre, Descripcion, EsSistema, Activo)
    SELECT v.Nombre, v.Descripcion, 1, 1
    FROM
    (
        VALUES
            (N'Administrador', N'Acceso total a la administración y operación de GoDental.'),
            (N'Vendedor',      N'Operación comercial: caja, clientes, cotizaciones y ventas con permisos limitados.')
    ) v(Nombre, Descripcion)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM seg.Roles r WHERE r.Nombre = v.Nombre
    );

    /* ======================================================================
       2. PERMISOS BASE
       ====================================================================== */

    DECLARE @Permisos TABLE
    (
        Codigo       NVARCHAR(100) NOT NULL,
        Nombre       NVARCHAR(100) NOT NULL,
        Modulo       NVARCHAR(50)  NOT NULL,
        Descripcion  NVARCHAR(250) NULL
    );

    INSERT INTO @Permisos (Codigo, Nombre, Modulo, Descripcion)
    VALUES
        (N'DASHBOARD.VER',                   N'Ver dashboard',                       N'Dashboard',      N'Consultar indicadores y resumen principal.'),

        (N'VENTAS.VER',                      N'Ver ventas',                          N'Ventas',         N'Consultar ventas y sus detalles.'),
        (N'VENTAS.CREAR',                    N'Crear ventas',                        N'Ventas',         N'Registrar y confirmar ventas.'),
        (N'VENTAS.COTIZAR',                  N'Crear cotizaciones',                  N'Ventas',         N'Crear y gestionar cotizaciones.'),
        (N'VENTAS.ANULAR',                   N'Anular ventas',                       N'Ventas',         N'Anular ventas conforme a las reglas del sistema.'),
        (N'VENTAS.USAR_CREDITO',             N'Vender a crédito',                   N'Ventas',         N'Usar crédito ya autorizado/configurado para el cliente.'),
        (N'VENTAS.DESCUENTO_7',              N'Descuento hasta 7%',                 N'Ventas',         N'Aplicar descuentos de hasta 7% sin autorización especial.'),
        (N'VENTAS.DESCUENTO_MAYOR_7',        N'Descuento mayor a 7%',               N'Ventas',         N'Aplicar descuentos mayores a 7% dejando justificación.'),
        (N'VENTAS.DESCUENTO_MAYOR_35',       N'Descuento mayor a 35%',              N'Ventas',         N'Autorizar descuentos mayores a 35% con credencial administrativa.'),
        (N'VENTAS.DEVOLUCION_SOLICITAR',     N'Solicitar devolución',               N'Ventas',         N'Registrar una solicitud de devolución.'),
        (N'VENTAS.DEVOLUCION_AUTORIZAR',     N'Autorizar devolución',               N'Ventas',         N'Aprobar/rechazar devoluciones y definir reingreso a inventario.'),
        (N'VENTAS.REIMPRIMIR',               N'Reimprimir documentos',              N'Ventas',         N'Reimprimir recibos, comprobantes y documentos permitidos.'),

        (N'CAJA.VER',                        N'Ver caja',                            N'Caja',           N'Consultar caja y turno actual.'),
        (N'CAJA.ABRIR',                      N'Abrir caja',                          N'Caja',           N'Abrir un turno de caja.'),
        (N'CAJA.CERRAR',                     N'Cerrar caja',                         N'Caja',           N'Cerrar y cuadrar un turno de caja.'),
        (N'CAJA.MOVIMIENTO',                 N'Movimientos de caja',                 N'Caja',           N'Registrar ingresos/egresos manuales autorizados.'),

        (N'CLIENTES.VER',                    N'Ver clientes',                        N'Clientes',       N'Consultar clientes y contactos.'),
        (N'CLIENTES.CREAR',                  N'Crear clientes',                      N'Clientes',       N'Registrar nuevos clientes.'),
        (N'CLIENTES.EDITAR',                 N'Editar clientes',                     N'Clientes',       N'Modificar datos comerciales de clientes.'),
        (N'CLIENTES.CREDITO_CONFIGURAR',     N'Configurar crédito',                  N'Clientes',       N'Habilitar, bloquear y modificar límites/plazos de crédito.'),
        (N'CLIENTES.CREDITO_EXCEPCION',      N'Autorizar excepción de crédito',      N'Clientes',       N'Autorizar excepciones por límite o mora.'),

        (N'INVENTARIO.VER',                  N'Ver inventario',                      N'Inventario',     N'Consultar existencias, lotes y vencimientos.'),
        (N'INVENTARIO.PRODUCTOS_GESTIONAR',  N'Gestionar productos',                 N'Inventario',     N'Crear/editar productos, códigos de barras y parámetros.'),
        (N'INVENTARIO.AJUSTAR',              N'Ajustar inventario',                  N'Inventario',     N'Registrar ajustes controlados de existencias.'),
        (N'INVENTARIO.CONTEO',               N'Realizar conteos',                    N'Inventario',     N'Crear y cerrar conteos físicos.'),
        (N'INVENTARIO.COSTOS_VER',           N'Ver costos',                          N'Inventario',     N'Consultar costo promedio y costo puesto en bodega.'),

        (N'COMPRAS.VER',                     N'Ver compras',                         N'Compras',        N'Consultar proveedores, OC, recepciones e importaciones.'),
        (N'COMPRAS.GESTIONAR',               N'Gestionar compras',                   N'Compras',        N'Crear y gestionar órdenes de compra y recepciones.'),
        (N'PROVEEDORES.GESTIONAR',           N'Gestionar proveedores',               N'Compras',        N'Crear y editar proveedores y contactos.'),
        (N'IMPORTACIONES.GESTIONAR',         N'Gestionar importaciones',             N'Compras',        N'Gestionar envíos, DHL, eventos y gastos de importación.'),

        (N'REPORTES.VENTAS',                 N'Reportes de ventas',                  N'Reportes',       N'Consultar reportes comerciales de ventas.'),
        (N'REPORTES.INVENTARIO',             N'Reportes de inventario',              N'Reportes',       N'Consultar reportes de stock, lotes y vencimientos.'),
        (N'REPORTES.FINANCIEROS',            N'Reportes financieros',                N'Reportes',       N'Consultar cobros, crédito y resultados financieros.'),
        (N'REPORTES.COSTOS',                 N'Reportes de costos',                  N'Reportes',       N'Consultar márgenes, costos e importaciones.'),
        (N'REPORTES.COMERCIAL',              N'Reportes comerciales',               N'Reportes',       N'Consultar campañas, prospectos y rendimiento comercial.'),

        (N'FISCAL.VER',                      N'Ver información fiscal',              N'Fiscal',         N'Consultar documentos fiscales/DTE.'),
        (N'FISCAL.CONFIGURAR',               N'Configurar fiscal/DTE',               N'Fiscal',         N'Gestionar configuración fiscal e integración futura.'),

        (N'COMERCIAL.VER',                   N'Ver módulo comercial',                N'Comercial',      N'Consultar prospectos, zonas y campañas.'),
        (N'COMERCIAL.PROSPECTAR',             N'Gestionar prospección',                N'Comercial',      N'Ejecutar/revisar prospección y fuentes públicas.'),
        (N'COMERCIAL.CAMPANIAS_CREAR',        N'Crear campañas',                      N'Comercial',      N'Preparar campañas y mensajes.'),
        (N'COMERCIAL.CAMPANIAS_APROBAR',      N'Aprobar campañas',                   N'Comercial',      N'Aprobar campañas antes del envío.'),

        (N'USUARIOS.VER',                    N'Ver usuarios',                        N'Seguridad',      N'Consultar usuarios y roles.'),
        (N'USUARIOS.GESTIONAR',               N'Gestionar usuarios',                  N'Seguridad',      N'Crear, editar, bloquear y reactivar usuarios.'),
        (N'SEGURIDAD.ROLES_PERMISOS',         N'Gestionar roles y permisos',          N'Seguridad',      N'Administrar roles y asignaciones de permisos.'),

        (N'AJUSTES.VER',                     N'Ver ajustes',                         N'Sistema',        N'Consultar configuración de GoDental.'),
        (N'AJUSTES.EDITAR',                  N'Editar ajustes',                      N'Sistema',        N'Modificar parámetros permitidos del sistema.'),
        (N'BITACORA.VER',                    N'Ver bitácora',                        N'Sistema',        N'Consultar auditoría y eventos relevantes.'),
        (N'BACKUP.EJECUTAR',                 N'Ejecutar respaldo',                   N'Sistema',        N'Crear respaldo manual de la base.'),
        (N'BACKUP.CONFIGURAR',               N'Configurar respaldos',                N'Sistema',        N'Configurar estrategia y destino de respaldos.');

    UPDATE p
       SET p.Nombre      = s.Nombre,
           p.Modulo      = s.Modulo,
           p.Descripcion = s.Descripcion,
           p.Activo      = 1
    FROM seg.Permisos p
    INNER JOIN @Permisos s ON s.Codigo = p.Codigo;

    INSERT INTO seg.Permisos (Codigo, Nombre, Modulo, Descripcion, Activo)
    SELECT s.Codigo, s.Nombre, s.Modulo, s.Descripcion, 1
    FROM @Permisos s
    WHERE NOT EXISTS
    (
        SELECT 1 FROM seg.Permisos p WHERE p.Codigo = s.Codigo
    );

    /* Administrador: todos los permisos activos. */
    DECLARE @IdRolAdmin INT = (SELECT IdRol FROM seg.Roles WHERE Nombre = N'Administrador');
    DECLARE @IdRolVendedor INT = (SELECT IdRol FROM seg.Roles WHERE Nombre = N'Vendedor');

    UPDATE rp
       SET rp.Concedido = 1
    FROM seg.RolPermisos rp
    INNER JOIN seg.Permisos p ON p.IdPermiso = rp.IdPermiso
    WHERE rp.IdRol = @IdRolAdmin
      AND p.Activo = 1;

    INSERT INTO seg.RolPermisos (IdRol, IdPermiso, Concedido)
    SELECT @IdRolAdmin, p.IdPermiso, 1
    FROM seg.Permisos p
    WHERE p.Activo = 1
      AND NOT EXISTS
      (
          SELECT 1
          FROM seg.RolPermisos rp
          WHERE rp.IdRol = @IdRolAdmin
            AND rp.IdPermiso = p.IdPermiso
      );

    /* Vendedor: operación comercial sin administración sensible ni costos. */
    DECLARE @PermisosVendedor TABLE (Codigo NVARCHAR(100) PRIMARY KEY);

    INSERT INTO @PermisosVendedor (Codigo)
    VALUES
        (N'DASHBOARD.VER'),
        (N'VENTAS.VER'),
        (N'VENTAS.CREAR'),
        (N'VENTAS.COTIZAR'),
        (N'VENTAS.USAR_CREDITO'),
        (N'VENTAS.DESCUENTO_7'),
        (N'VENTAS.DESCUENTO_MAYOR_7'),
        (N'VENTAS.DEVOLUCION_SOLICITAR'),
        (N'VENTAS.REIMPRIMIR'),
        (N'CAJA.VER'),
        (N'CAJA.ABRIR'),
        (N'CAJA.CERRAR'),
        (N'CLIENTES.VER'),
        (N'CLIENTES.CREAR'),
        (N'CLIENTES.EDITAR'),
        (N'INVENTARIO.VER'),
        (N'REPORTES.VENTAS');

    INSERT INTO seg.RolPermisos (IdRol, IdPermiso, Concedido)
    SELECT @IdRolVendedor, p.IdPermiso, 1
    FROM seg.Permisos p
    INNER JOIN @PermisosVendedor v ON v.Codigo = p.Codigo
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM seg.RolPermisos rp
        WHERE rp.IdRol = @IdRolVendedor
          AND rp.IdPermiso = p.IdPermiso
    );

    UPDATE rp
       SET rp.Concedido = CASE WHEN v.Codigo IS NULL THEN 0 ELSE 1 END
    FROM seg.RolPermisos rp
    INNER JOIN seg.Permisos p ON p.IdPermiso = rp.IdPermiso
    LEFT JOIN @PermisosVendedor v ON v.Codigo = p.Codigo
    WHERE rp.IdRol = @IdRolVendedor;

    /* ======================================================================
       3. EMPRESA PLACEHOLDER EDITABLE
       ====================================================================== */

    IF NOT EXISTS (SELECT 1 FROM sis.Empresa WHERE Activo = 1)
        INSERT INTO sis.Empresa (Activo) VALUES (1);

    /* ======================================================================
       4. CATÁLOGOS GENERALES
       ====================================================================== */

    /* Categoría genérica inicial; las categorías reales se administran en UI. */
    IF NOT EXISTS
    (
        SELECT 1
        FROM cat.CategoriasProducto
        WHERE IdCategoriaPadre IS NULL AND Nombre = N'General'
    )
        INSERT INTO cat.CategoriasProducto (IdCategoriaPadre, Nombre, Descripcion, Activo)
        VALUES (NULL, N'General', N'Categoría inicial para productos aún no clasificados.', 1);

    /* Unidades de medida frecuentes. */
    DECLARE @Unidades TABLE
    (
        Codigo NVARCHAR(15),
        Nombre NVARCHAR(60),
        PermiteDecimales BIT
    );

    INSERT INTO @Unidades VALUES
        (N'UND',  N'Unidad',      0),
        (N'CAJA', N'Caja',        0),
        (N'KIT',  N'Kit',         0),
        (N'PAQ',  N'Paquete',     0),
        (N'ML',   N'Mililitro',   1),
        (N'G',    N'Gramo',       1);

    UPDATE u
       SET u.Nombre = s.Nombre,
           u.PermiteDecimales = s.PermiteDecimales,
           u.Activo = 1
    FROM cat.UnidadesMedida u
    INNER JOIN @Unidades s ON s.Codigo = u.Codigo;

    INSERT INTO cat.UnidadesMedida (Codigo, Nombre, PermiteDecimales, Activo)
    SELECT s.Codigo, s.Nombre, s.PermiteDecimales, 1
    FROM @Unidades s
    WHERE NOT EXISTS
    (
        SELECT 1 FROM cat.UnidadesMedida u WHERE u.Codigo = s.Codigo
    );

    /* Monedas: USD base y CNY disponible para compras a proveedor chino. */
    UPDATE cat.Monedas SET EsBase = 0 WHERE CodigoISO <> 'USD' AND EsBase = 1;

    IF EXISTS (SELECT 1 FROM cat.Monedas WHERE CodigoISO = 'USD')
        UPDATE cat.Monedas SET Nombre = N'Dólar estadounidense', EsBase = 1, Activo = 1 WHERE CodigoISO = 'USD';
    ELSE
        INSERT INTO cat.Monedas (CodigoISO, Nombre, EsBase, Activo)
        VALUES ('USD', N'Dólar estadounidense', 1, 1);

    IF EXISTS (SELECT 1 FROM cat.Monedas WHERE CodigoISO = 'CNY')
        UPDATE cat.Monedas SET Nombre = N'Yuan renminbi chino', EsBase = 0, Activo = 1 WHERE CodigoISO = 'CNY';
    ELSE
        INSERT INTO cat.Monedas (CodigoISO, Nombre, EsBase, Activo)
        VALUES ('CNY', N'Yuan renminbi chino', 0, 1);

    /* Métodos de pago acordados. Pago mixto = varias filas de VentaPagos. */
    DECLARE @MetodosPago TABLE
    (
        Codigo NVARCHAR(30),
        Nombre NVARCHAR(80),
        AfectaCajaEfectivo BIT,
        RequiereReferencia BIT
    );

    INSERT INTO @MetodosPago VALUES
        (N'EFECTIVO',      N'Efectivo',             1, 0),
        (N'TRANSFERENCIA', N'Transferencia',        0, 1),
        (N'QR',            N'Pago por QR',           0, 1),
        (N'DEPOSITO',      N'Depósito bancario',    0, 1),
        (N'TARJETA',       N'Tarjeta',               0, 1),
        (N'CREDITO',       N'Crédito del cliente',   0, 0);

    UPDATE m
       SET m.Nombre = s.Nombre,
           m.AfectaCajaEfectivo = s.AfectaCajaEfectivo,
           m.RequiereReferencia = s.RequiereReferencia,
           m.Activo = 1
    FROM cat.MetodosPago m
    INNER JOIN @MetodosPago s ON s.Codigo = m.Codigo;

    INSERT INTO cat.MetodosPago (Codigo, Nombre, AfectaCajaEfectivo, RequiereReferencia, Activo)
    SELECT s.Codigo, s.Nombre, s.AfectaCajaEfectivo, s.RequiereReferencia, 1
    FROM @MetodosPago s
    WHERE NOT EXISTS
    (
        SELECT 1 FROM cat.MetodosPago m WHERE m.Codigo = s.Codigo
    );

    /* Tipos de cliente. Sólo clínica, odontólogo y mayorista son elegibles
       para crédito por defecto según la política inicial acordada. */
    DECLARE @TiposCliente TABLE
    (
        Codigo NVARCHAR(30),
        Nombre NVARCHAR(80),
        ElegibleCreditoDefault BIT
    );

    INSERT INTO @TiposCliente VALUES
        (N'NORMAL',       N'Cliente general',           0),
        (N'MAYORISTA',    N'Mayorista',                 1),
        (N'CLINICA',      N'Clínica odontológica',      1),
        (N'ODONTOLOGO',   N'Odontólogo',                1),
        (N'ESTUDIANTE',   N'Estudiante',                0),
        (N'LABORATORIO',  N'Laboratorio dental',        0),
        (N'UNIVERSIDAD',  N'Universidad / institución', 0),
        (N'DISTRIBUIDOR', N'Distribuidor',              0);

    UPDATE t
       SET t.Nombre = s.Nombre,
           t.ElegibleCreditoDefault = s.ElegibleCreditoDefault,
           t.Activo = 1
    FROM cat.TiposCliente t
    INNER JOIN @TiposCliente s ON s.Codigo = t.Codigo;

    INSERT INTO cat.TiposCliente (Codigo, Nombre, ElegibleCreditoDefault, Activo)
    SELECT s.Codigo, s.Nombre, s.ElegibleCreditoDefault, 1
    FROM @TiposCliente s
    WHERE NOT EXISTS
    (
        SELECT 1 FROM cat.TiposCliente t WHERE t.Codigo = s.Codigo
    );

    /* IVA inicial. Debe verificarse antes de puesta fiscal en producción. */
    IF EXISTS (SELECT 1 FROM cat.Impuestos WHERE Codigo = N'IVA')
        UPDATE cat.Impuestos
           SET Nombre = N'IVA', Tasa = 13.0000, IncluidoEnPrecio = 1, Activo = 1
         WHERE Codigo = N'IVA';
    ELSE
        INSERT INTO cat.Impuestos (Codigo, Nombre, Tasa, IncluidoEnPrecio, Activo)
        VALUES (N'IVA', N'IVA', 13.0000, 1, 1);

    /* ======================================================================
       5. ALMACÉN Y CAJA INICIALES
       ====================================================================== */

    IF EXISTS (SELECT 1 FROM inv.Almacenes WHERE Codigo = N'PRINCIPAL')
        UPDATE inv.Almacenes
           SET Nombre = N'Almacén principal', EsPrincipal = 1, Activo = 1
         WHERE Codigo = N'PRINCIPAL';
    ELSE
        INSERT INTO inv.Almacenes (Codigo, Nombre, EsPrincipal, Activo)
        VALUES (N'PRINCIPAL', N'Almacén principal', 1, 1);

    DECLARE @IdAlmacenPrincipal INT =
        (SELECT TOP (1) IdAlmacen FROM inv.Almacenes WHERE Codigo = N'PRINCIPAL');

    IF EXISTS (SELECT 1 FROM ven.Cajas WHERE Codigo = N'CAJA01')
        UPDATE ven.Cajas
           SET Nombre = N'Caja principal', IdAlmacen = @IdAlmacenPrincipal, Activo = 1
         WHERE Codigo = N'CAJA01';
    ELSE
        INSERT INTO ven.Cajas (Codigo, Nombre, IdAlmacen, IdEquipoPreferido, Activo)
        VALUES (N'CAJA01', N'Caja principal', @IdAlmacenPrincipal, NULL, 1);

    /* ======================================================================
       6. LISTAS DE PRECIO BASE
       La precedencia comercial quedó cerrada en VEN-01A v0.1:
       lista/tipo de cliente -> comparar promoción -> mejor precio válido ->
       descuento manual autorizado. No hay acumulación automática.
       ====================================================================== */

    DECLARE @ListasPrecio TABLE
    (
        Codigo NVARCHAR(30),
        Nombre NVARCHAR(100),
        CodigoTipoCliente NVARCHAR(30) NULL,
        MontoMinimoVenta DECIMAL(18,2) NULL,
        Prioridad SMALLINT
    );

    INSERT INTO @ListasPrecio VALUES
        (N'PUBLICO',     N'Precio público',       N'NORMAL',     NULL, 100),
        (N'MAYORISTA',   N'Precio mayorista',     N'MAYORISTA',  NULL, 100),
        (N'CLINICA',     N'Precio clínica',       N'CLINICA',    NULL, 100),
        (N'ODONTOLOGO',  N'Precio odontólogo',    N'ODONTOLOGO', NULL, 100),
        (N'ESTUDIANTE',  N'Precio estudiante',    N'ESTUDIANTE', NULL, 100);

    UPDATE lp
       SET lp.Nombre = s.Nombre,
           lp.IdTipoCliente = tc.IdTipoCliente,
           lp.MontoMinimoVenta = s.MontoMinimoVenta,
           lp.Prioridad = s.Prioridad,
           lp.Activo = 1
    FROM ven.ListasPrecio lp
    INNER JOIN @ListasPrecio s ON s.Codigo = lp.Codigo
    LEFT JOIN cat.TiposCliente tc ON tc.Codigo = s.CodigoTipoCliente;

    INSERT INTO ven.ListasPrecio
    (
        Codigo, Nombre, IdTipoCliente, MontoMinimoVenta,
        Prioridad, FechaInicio, FechaFin, Activo
    )
    SELECT
        s.Codigo, s.Nombre, tc.IdTipoCliente, s.MontoMinimoVenta,
        s.Prioridad, NULL, NULL, 1
    FROM @ListasPrecio s
    LEFT JOIN cat.TiposCliente tc ON tc.Codigo = s.CodigoTipoCliente
    WHERE NOT EXISTS
    (
        SELECT 1 FROM ven.ListasPrecio lp WHERE lp.Codigo = s.Codigo
    );

    /* ======================================================================
       7. CONFIGURACIÓN OPERATIVA BASE
       ====================================================================== */

    DECLARE @Configuracion TABLE
    (
        Clave        NVARCHAR(120),
        Valor        NVARCHAR(1000),
        TipoDato     NVARCHAR(20),
        Descripcion  NVARCHAR(300),
        EditableUI   BIT
    );

    INSERT INTO @Configuracion VALUES
        (N'Negocio.MonedaBase',                    N'USD',               N'STRING',  N'Moneda base de operación.',                                                0),
        (N'Inventario.MetodoValuacion',            N'PROMEDIO_PONDERADO',N'STRING',  N'Método de valuación definido para GoDental.',                              0),
        (N'Inventario.PermitirStockNegativo',      N'false',             N'BOOL',    N'No permite confirmar operaciones que dejen stock negativo.',                1),
        (N'Inventario.AlertaVencimientoDias',      N'90',                N'INT',     N'Días de anticipación para alertas generales de vencimiento.',               1),
        (N'Inventario.AlertaVencimientoUrgenteDias',N'60',               N'INT',     N'Días de anticipación para alerta urgente de vencimiento.',                  1),

        (N'Descuento.VendedorMaximo',              N'7.00',              N'DECIMAL', N'Descuento máximo ordinario permitido al rol Vendedor.',                    1),
        (N'Descuento.RequiereJustificacionSobre',  N'7.00',              N'DECIMAL', N'Por encima de este porcentaje debe registrarse motivo.',                  1),
        (N'Descuento.RequiereAutorizacionSobre',   N'35.00',             N'DECIMAL', N'Por encima de este porcentaje se exige autorización administrativa.',     1),

        (N'Devolucion.PlazoHoras',                 N'24',                N'INT',     N'Plazo inicial máximo para solicitar devolución desde la compra.',           1),
        (N'Devolucion.RequiereComprobante',        N'true',              N'BOOL',    N'Requiere factura o constancia de compra.',                                  1),
        (N'Devolucion.RequiereEvidenciaAbierto',   N'true',              N'BOOL',    N'Producto abierto requiere evidencia/justificación, incluida foto.',         1),
        (N'Devolucion.SoloAdministradorAutoriza',  N'true',              N'BOOL',    N'Sólo Administrador puede autorizar devoluciones.',                          0),

        (N'Credito.DiasDefault',                   N'15',                N'INT',     N'Plazo inicial de crédito; puede modificarse por cliente.',                  1),
        (N'Credito.IntervaloAvisoDias',             N'5',                 N'INT',     N'Intervalo inicial entre avisos de saldo/crédito.',                           1),
        (N'Credito.LimiteDefault',                 N'0.00',              N'DECIMAL', N'Límite por defecto; el administrador debe asignar monto por cliente.',     1),

        (N'Precios.MontoReferenciaEspecial',       N'50.00',             N'DECIMAL', N'Monto de referencia acordado para considerar reglas especiales de precio.',1),
        (N'Precios.PrecedenciaDefinida',           N'true',              N'BOOL',    N'Precedencia cerrada por VEN-01A: cliente/lista vs promoción, mejor precio; descuento manual al final.', 0),

        (N'Documentos.TamanoDefault',              N'CARTA',             N'STRING',  N'Tamaño inicial para documentos impresos.',                                  1),
        (N'Documentos.PermitirTicketPos',           N'true',              N'BOOL',    N'Permite generar representación compacta POS cuando se implemente.',         1),

        (N'Backup.FrecuenciaHoras',                N'24',                N'INT',     N'Frecuencia objetivo de respaldo FULL.',                                     1),
        (N'Backup.CopiarOneDrive',                 N'true',              N'BOOL',    N'Copiar archivos .bak terminados a ubicación sincronizada con OneDrive.',    1),

        (N'Online.IntervaloProspeccionDias',       N'15',                N'INT',     N'Intervalo objetivo para revisar nuevas zonas/prospectos.',                  1),
        (N'Online.RequiereAprobacionHumana',       N'true',              N'BOOL',    N'Las campañas requieren aprobación humana previa al envío.',                 0),

        (N'UI.TemaDefault',                        N'SYSTEM',             N'STRING',  N'Tema inicial; cada usuario puede preferir LIGHT/DARK/SYSTEM.',              1);

    UPDATE c
       SET c.Valor = s.Valor,
           c.TipoDato = s.TipoDato,
           c.Descripcion = s.Descripcion,
           c.EditableUI = s.EditableUI,
           c.ModificadoPor = NULL,
           c.FechaModificacion = SYSDATETIMEOFFSET()
    FROM sis.Configuracion c
    INNER JOIN @Configuracion s ON s.Clave = c.Clave;

    INSERT INTO sis.Configuracion
    (
        Clave, Valor, TipoDato, Descripcion,
        EditableUI, ModificadoPor, FechaModificacion
    )
    SELECT
        s.Clave, s.Valor, s.TipoDato, s.Descripcion,
        s.EditableUI, NULL, SYSDATETIMEOFFSET()
    FROM @Configuracion s
    WHERE NOT EXISTS
    (
        SELECT 1 FROM sis.Configuracion c WHERE c.Clave = s.Clave
    );

    /* ======================================================================
       8. CORRELATIVOS / SECUENCIAS DOCUMENTALES
       ====================================================================== */

    DECLARE @Secuencias TABLE
    (
        TipoDocumento NVARCHAR(40),
        Prefijo NVARCHAR(20),
        Longitud TINYINT,
        ReinicioAnual BIT
    );

    INSERT INTO @Secuencias VALUES
        (N'CLIENTE',          N'CLI-',  8, 0),
        (N'PROVEEDOR',        N'PRO-',  8, 0),
        (N'COTIZACION',       N'COT-',  8, 1),
        (N'VENTA',            N'VEN-',  8, 1),
        (N'RECIBO',           N'REC-',  8, 1),
        (N'DEVOLUCION',       N'DEV-',  8, 1),
        (N'ORDEN_COMPRA',     N'OC-',   8, 1),
        (N'RECEPCION_COMPRA', N'RC-',   8, 1);

    UPDATE d
       SET d.Prefijo = s.Prefijo,
           d.Longitud = s.Longitud,
           d.ReinicioAnual = s.ReinicioAnual
    FROM sis.SecuenciasDocumento d
    INNER JOIN @Secuencias s ON s.TipoDocumento = d.TipoDocumento;

    INSERT INTO sis.SecuenciasDocumento
    (
        TipoDocumento, Prefijo, UltimoNumero,
        Longitud, ReinicioAnual, AnioActual
    )
    SELECT
        s.TipoDocumento, s.Prefijo, 0,
        s.Longitud, s.ReinicioAnual,
        CASE WHEN s.ReinicioAnual = 1 THEN YEAR(GETDATE()) ELSE NULL END
    FROM @Secuencias s
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM sis.SecuenciasDocumento d
        WHERE d.TipoDocumento = s.TipoDocumento
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO

/* ==========================================================================
   9. VALIDACIÓN FINAL
   ========================================================================== */

DECLARE @Errores TABLE (Detalle NVARCHAR(300));

IF NOT EXISTS (SELECT 1 FROM seg.Roles WHERE Nombre = N'Administrador' AND Activo = 1)
    INSERT INTO @Errores VALUES (N'No existe el rol Administrador activo.');

IF NOT EXISTS (SELECT 1 FROM seg.Roles WHERE Nombre = N'Vendedor' AND Activo = 1)
    INSERT INTO @Errores VALUES (N'No existe el rol Vendedor activo.');

IF NOT EXISTS (SELECT 1 FROM cat.Monedas WHERE CodigoISO = 'USD' AND EsBase = 1 AND Activo = 1)
    INSERT INTO @Errores VALUES (N'USD no quedó configurada como moneda base.');

IF NOT EXISTS (SELECT 1 FROM inv.Almacenes WHERE Codigo = N'PRINCIPAL' AND Activo = 1)
    INSERT INTO @Errores VALUES (N'No existe el almacén PRINCIPAL.');

IF NOT EXISTS (SELECT 1 FROM ven.Cajas WHERE Codigo = N'CAJA01' AND Activo = 1)
    INSERT INTO @Errores VALUES (N'No existe CAJA01.');

IF (SELECT COUNT(*) FROM cat.MetodosPago WHERE Codigo IN
    (N'EFECTIVO', N'TRANSFERENCIA', N'QR', N'DEPOSITO', N'TARJETA', N'CREDITO') AND Activo = 1) <> 6
    INSERT INTO @Errores VALUES (N'No quedaron completos los 6 métodos de pago base.');

IF NOT EXISTS (SELECT 1 FROM sis.Configuracion WHERE Clave = N'Descuento.VendedorMaximo' AND Valor = N'7.00')
    INSERT INTO @Errores VALUES (N'Falta configuración de descuento máximo del vendedor.');

IF NOT EXISTS (SELECT 1 FROM sis.Configuracion WHERE Clave = N'Devolucion.PlazoHoras' AND Valor = N'24')
    INSERT INTO @Errores VALUES (N'Falta configuración de devolución a 24 horas.');

IF NOT EXISTS (SELECT 1 FROM sis.Configuracion WHERE Clave = N'Credito.DiasDefault' AND Valor = N'15')
    INSERT INTO @Errores VALUES (N'Falta configuración de crédito a 15 días.');

IF NOT EXISTS (SELECT 1 FROM sis.Configuracion WHERE Clave = N'Precios.PrecedenciaDefinida' AND Valor = N'true')
    INSERT INTO @Errores VALUES (N'La precedencia de precios definida en VEN-01A no quedó activa.');

IF EXISTS (SELECT 1 FROM @Errores)
BEGIN
    SELECT Detalle AS ErrorSeed FROM @Errores;
    THROW 50799, N'007_Seed.sql finalizó con validaciones pendientes.', 1;
END;

SELECT
    N'007_Seed.sql OK' AS Resultado,
    (SELECT COUNT(*) FROM seg.Roles WHERE Activo = 1) AS RolesActivos,
    (SELECT COUNT(*) FROM seg.Permisos WHERE Activo = 1) AS PermisosActivos,
    (SELECT COUNT(*) FROM cat.MetodosPago WHERE Activo = 1) AS MetodosPagoActivos,
    (SELECT COUNT(*) FROM cat.TiposCliente WHERE Activo = 1) AS TiposClienteActivos,
    (SELECT COUNT(*) FROM sis.Configuracion) AS Configuraciones,
    (SELECT COUNT(*) FROM sis.SecuenciasDocumento) AS Secuencias;
GO

/* ============================================================================
   FIN 007_Seed.sql

   El primer usuario Administrador NO se siembra aquí para evitar una contraseña
   por defecto o hash inseguro. Se creará mediante bootstrap controlado desde C#.
   ============================================================================ */
