# DEV-10 · Proveedores: base lógica para compras

Incremento del 19/10/2026 de la fase 4 de DM-01, basado en IMP-01, SEC-01 y las tablas existentes de DB-01/003. Se implementan contratos C#, repositorios Dapper y pruebas; las vistas se conectan en el incremento posterior de UI.

## Alcance y reglas

- Consulta paginada por código, nombre o país, incluyendo inactivos cuando se solicite; detalle con contactos y relaciones de productos. Lectura exige `COMPRAS.VER`, escritura además `PROVEEDORES.GESTIONAR`, revalidados en SQL en cada operación. No se añaden permisos nuevos.
- Alta, edición y desactivación/reactivación del proveedor con motivo de auditoría. Código interno obligatorio/único; se conserva si ya existen órdenes. No se eliminan proveedores, contactos ni relaciones comerciales. Desactivar conserva órdenes e historial y no modifica stock ni costos de inventario.
- Una edición de contactos o relaciones avanza la `RowVersion` del proveedor. Toda mutación de su agregado exige esa versión vigente, evitando sobrescribir desde otra PC. Los contactos se editan individualmente; ausencia de un contacto en una petición nunca significa borrarlo. Sólo un contacto activo puede ser principal por proveedor; se rechaza un segundo hasta desmarcar el anterior.
- Relación producto-proveedor: SKU externo, costo último, moneda del costo, MOQ, plazo, preferencia y estado. Costo hasta seis decimales; MOQ positivo hasta cuatro; plazo no negativo. Costo conocido requiere moneda. No modifica precio de venta, promedio, costo histórico ni existencias. La preferencia es por relación; no se impone exclusividad entre proveedores sin regla aprobada.
- Los costos de referencia comercial se devuelven sólo con `PROVEEDORES.GESTIONAR`, comprobado en servidor. Un lector de compras obtiene la relación pero no el costo. No se confunde este costo de referencia con costo puesto en bodega ni utilidad.
- Proveedor inactivo sólo admite reactivación/edición de su ficha; los contactos y relaciones se mantienen consultables. Para activar una relación se exige producto y moneda activos. Se puede desactivar una relación aunque su producto/moneda haya sido desactivado.
- Datos opcionales vacíos se normalizan a null. Email y URL se validan cuando se proporcionen; la URL sólo admite HTTP/HTTPS sin credenciales. Son datos de contacto, no se envían comunicaciones ni se abren enlaces automáticamente.

## Persistencia y contratos para UI

No requiere tablas, columnas o procedimientos nuevos: usa `cmp.Proveedores.RowVersion`, `cmp.ProveedorContactos`, `cmp.ProveedorProductos` y catálogos de 003. Las escrituras y la bitácora se confirman en una transacción; ante conflictos, código duplicado o permiso revocado se devuelve un resultado recuperable. Cada cambio registra motivo y JSON anterior/posterior en bitácora. La lectura del detalle es consistente entre ficha y subregistros.

`SupplierService`: `CanRead`, `CanManage`, `SearchAsync`, `ReadAsync`, `CurrenciesAsync`, `SaveAsync`, `SaveContactAsync`, `SaveProductAsync`. Antes de guardar un contacto/relación, enviar `ExpectedSupplierVersion` de la última lectura; recargar el agregado después. La ficha usa `RowVersion` (vacía sólo para alta). Los contactos usan Id=0 para alta; una relación se identifica por proveedor+producto y se conserva para reactivación.

Las consultas denegadas producen `UnauthorizedAccessException`; las escrituras devuelven `Result<int>` con `FORBIDDEN`, `VALIDATION`, `CONCURRENCY_CONFLICT`, `DUPLICATE_CODE`, `IN_USE`, `INVALID_STATE` o `PRIMARY_CONTACT_EXISTS`. El entero identifica el proveedor, para recargar su detalle. No se implementan aún órdenes, recepciones, prorrateo de importaciones, cuentas por pagar, DHL ni comunicaciones con proveedores.

## Validación

La suite SQL `--run-suppliers` usa una base aislada `GoDental_Tests_<GUID>` y se incorpora a `verify.ps1 -SqlIntegration` y al CI. Debe comprobar permisos revocados, concurrencia del agregado, duplicados, contacto principal, códigos con historial, costos/moneda, conservación de inventario y rollback de auditoría, además de validadores unitarios.

Validación específica ejecutada: 38 comprobaciones SQL de proveedores correctas y 9 casos xUnit nuevos. Incluye dos estaciones editando el mismo contacto, ocultación de costos tras revocar permisos, deactivación/reactivación de relaciones, cuatro/seis decimales, código con órdenes históricas y rollback ante un equipo de auditoría inválido. No se usaron proveedores, correos ni productos reales.
