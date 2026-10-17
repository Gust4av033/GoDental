# DEV-02 · Seguridad y catálogo operativo

Versión 0.2 · 17/10/2026 · Desarrollo local; no habilita producción.

## Comportamiento disponible

- Crear el primer administrador desde la interfaz, sin cuenta ni contraseña predeterminadas. Sólo puede inicializar un responsable con permisos SQL de instalación; dos intentos simultáneos producen una única cuenta.
- Iniciar y cerrar sesión; cambiar contraseña propia; exigir cambio de claves temporales antes de otorgar permisos. Los PasswordBox se limpian al enviar y ocultar la vista; los ViewModels no almacenan claves como propiedades.
- Consultar usuarios, crear cuentas con rol existente, activar/desactivar y restablecer claves temporales con motivo, rowversion y auditoría. No permite desactivar/restablecer la cuenta propia desde la administración ni eliminar el último administrador activo. No incluye todavía edición de roles/permisos existentes ni altas de nuevos roles.
- Buscar productos por SKU, nombre o código de barras registrado; filtrar activos/inactivos, consultar stock agregado de almacenes activos y paginar en bloques de 30.
- Crear productos y categorías; editar SKU, nombre, descripción, categoría, marca existente, unidad, precio de referencia, mínimos/máximos y controles de lote/vencimiento; desactivar conservando historial. La marca es opcional. No incluye todavía alta de marcas, unidades ni códigos de barra desde la interfaz.
- El catálogo no ingresa ni ajusta existencias. No permite cambiar unidad/controles de trazabilidad de un producto con stock, lotes o movimientos. El control de serie existente se conserva; no se introduce un módulo de serialización nuevo.

## Decisiones de implementación

Mantiene DM-01/DB-01/ARQ-01/SEC-01/UX-01, .NET 8/C# 12, WPF/MVVM, Dapper, Generic Host y SQL Server. No cambia tablas ni procedimientos 001–008. La migración 009 incorpora dos parámetros iniciales de seguridad sin sobrescribir ajustes existentes.

El hash PBKDF2-SHA256 codifica versión, 600 000 iteraciones, salt aleatorio de 16 bytes y subclave de 32 bytes. Usa comparación en tiempo constante; versiones válidas con parámetros anteriores se actualizan al autenticar. Las contraseñas nuevas admiten frases de 10–128 caracteres y rechazan algunos valores triviales. Es una política técnica inicial versionada de SEC-01, no una certificación externa.

Los intentos fallidos se serializan en SQL por cuenta y actualizan contador, bloqueo y bitácora dentro de una transacción. El mensaje de rechazo no distingue usuario inexistente, clave incorrecta o bloqueo. El cambio de contraseña también consume intentos ante una clave actual incorrecta. `sp_getapplock` protege el primer administrador y la administración de usuarios entre procesos/PCs; no se usan bloqueos locales para integridad de datos.

Application deniega por permisos; las mutaciones de catálogo/usuarios vuelven a verificar permisos efectivos en SQL. La administración de este incremento exige `USUARIOS.GESTIONAR` y `SEGURIDAD.ROLES_PERMISOS`; las lecturas usan `USUARIOS.VER`. El vendedor del seed puede consultar inventario, pero carece de acciones de mantenimiento.

Las mutaciones de producto, categoría y cuenta se confirman junto con su bitácora. Un fallo de auditoría revierte todos los cambios. La edición usa rowversion; un conflicto exige recargar, sin sobrescritura ni reintento ciego. El JSON de auditoría de producto se toma del registro persistido, con su Id y rowversion reales. No se registran hashes, contraseñas ni cadenas de conexión.

## Conexión y entorno

Desde 0.2, `WorkstationConnectionFactory` utiliza el perfil local guardado para diagnóstico y operaciones, reemplazando la separación provisional de DEV-01. El contrato anterior basado en `DatabaseOptions` permanece disponible en `SqlConnectionFactory`, pero los módulos de esta aplicación consumen el perfil. Cambiar configuración requiere cerrar sesión; cada sesión se vincula además al servidor/base donde se autenticó para impedir reutilizar Ids en otro destino.

Cada conexión establece `SESSION_CONTEXT` de usuario/equipo y vuelve a comprobar que la cuenta esté activa y no requiera cambio de clave. Las consultas sensibles se limpian al cerrar sesión y descartan resultados pendientes de una sesión anterior. Los permisos de navegación se cachean por sesión; las mutaciones se revalidan en servidor.

Se utiliza identidad Windows para el desarrollo local. La conexión continúa cifrada. La aceptación de certificado autofirmado es optativa y está restringida al equipo local y nombres `GoDental_Development` / `GoDental_Tests_*`; se rechaza en servidores remotos, `GoDental` y otros nombres. La cuenta de aplicación SQL con mínimo privilegio, el certificado de producción y los secretos protegidos siguen pendientes de despliegue. No se guardan contraseñas SQL ni se publica el motor en Internet.

Se creó `GoDental_Development` exclusivamente para este avance, sin usuarios ni productos de muestra. El primer administrador lo elige el usuario. Las pruebas usan otras bases `GoDental_Tests_<GUID>`, marcadas y con datos artificiales; se retienen para inspección. No se leen ni modifican datos de otras bases.

## Uso

1. Abrir GoDental 0.2. El perfil local se dejó preparado si no había una configuración del usuario.
2. Si hace falta, ir a **Conexión y apariencia → Usar base local de desarrollo → Guardar configuración**.
3. Abrir **Acceso de usuarios**; crear nombre, usuario y contraseña del primer administrador.
4. Entrar a **Productos** para registrar catálogo y categorías. **Nuevo producto** lleva al formulario. Para actualizar, seleccionar una fila y pulsar **Editar seleccionado**.
5. En **Administrar usuarios**, crear vendedores con contraseña temporal. Al entrar deberán elegir su propia clave. Para cambiar la propia, usar **Acceso de usuarios → Cambiar mi contraseña**.

## Pruebas reproducibles

```powershell
.\scripts\verify.ps1
.\scripts\verify.ps1 -SqlIntegration
```

La segunda variante requiere SQL Server local y permiso Windows para crear una base aislada. Nunca se conecta a producción. Alternativas:

```powershell
dotnet run --project tests/GoDental.DatabaseTests -- --install-development .
dotnet run --project tests/GoDental.DatabaseTests -- --run-integration .
```

Se probaron instalación completa, inicialización simultánea, hashes, bloqueo/expiración, cambio y restablecimiento de claves, aislamiento entre sesiones/bases, permisos vendedor, revocación antes de guardar, duplicados, concurrencia de edición, desactivación, restricciones de trazabilidad y rollback por fallo deliberado de auditoría.

## Límites y siguiente incremento

No están implementados clientes, entradas/recepciones de inventario, ventas, caja, reportes operativos, PDFs ni DTE. El stock del catálogo sólo se consulta. Faltan expiración por inactividad, administración completa de roles, gestión de claves de producción, preferencias por usuario de GoDental, backups automáticos y pruebas de red entre dos laptops. La ejecución de la baseline no certifica por sí sola los procedimientos de venta/compra; requieren pruebas específicas antes de habilitarlos.
