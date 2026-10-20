# DEV-13 · Clientes y ficha comercial

Incremento del 19/10/2026, posterior a DEV-12. El usuario autoriza continuar con Clientes mientras la validación visual/SQL de Windows queda pendiente. No se da esa validación por ejecutada ni se integra en `main`.

## Contrato y decisiones del incremento

Se reutilizan `cli.Clientes`, `cli.ClienteContactos`, `cli.ClienteDirecciones`, `cat.TiposCliente`, `ven.ListasPrecio`, `ven.Ventas`, `ven.CuentasPorCobrar` y `ven.AbonosCliente` de DB-01/004. No se crean tablas ni se modifican migraciones históricas.

- `CLIENTES.VER` permite consultar ficha, contactos, direcciones y configuración comercial/de crédito. Crear exige además `CLIENTES.CREAR`; modificar ficha/contactos/direcciones exige `CLIENTES.EDITAR`. Configurar crédito exige `CLIENTES.CREDITO_CONFIGURAR`, independientemente de la edición de ficha.
- La ficha general no acepta campos de crédito. El alta conserva crédito deshabilitado; habilitación, límite, plazo, intervalo, bloqueo y motivo se guardan mediante un contrato separado. No se activan bloqueos automáticos por mora ni excepciones comerciales pendientes en VEN-01A.
- El historial de ventas, estado de cuenta, abonos y resumen de saldos requieren además `VENTAS.VER`. Se consultan mediante páginas acotadas y sin exponer costos. Son consultas reales de las tablas existentes; no se registran ventas ni abonos en esta fase.
- El saldo utilizado para la consulta de crédito sigue `sp_ConfirmarVenta`: saldos positivos en cuentas PENDIENTE/PARCIAL/VENCIDA. Límite null equivale a cero según ese procedimiento. La disponibilidad mostrada es informativa; la autorización definitiva de una venta permanece en su transacción. El vencimiento se compara contra la fecha del servidor.
- Contactos y direcciones se guardan individualmente, sin reemplazo masivo. No se borra ningún registro; activo/inactivo preserva el historial. Las direcciones conservan FISCAL/ENTREGA/OTRA. Los indicadores de principal se conservan según el modelo, sin inventar exclusividad global que la baseline no define.
- Cada edición, incluido crédito o subregistros, exige `RowVersion` del cliente y avanza esa versión. Se bloquea la cabecera durante la transacción, se revalidan permisos reales y se auditan valores anteriores/posteriores y motivo. Un contacto o dirección ajenos se rechazan. Los cambios de estado no borran ni alteran ventas/CxC históricas.
- Una ficha inactiva admite corrección/reactivación; sus contactos/direcciones se consultan y requieren reactivación del cliente para modificarlos. El código se conserva si hay ventas/cotizaciones/CxC vinculadas. Los catálogos se validan en servidor; un valor histórico inactivo puede conservarse, pero no asignarse de nuevo al cambiarlo.
- Vacíos opcionales se normalizan a null. Se validan longitudes del esquema, correo y precisión/rangos de crédito, sin inventar formatos oficiales de NIT/NRC/DUI ni aceptación fiscal/DTE. Código, NIT y NRC duplicados devuelven un error recuperable.

La ficha WPF usa pestañas de información general, contactos, direcciones, datos fiscales, condiciones comerciales, crédito, historial de ventas y estado de cuenta. Los formularios editan copias aisladas, solicitan motivo/confirmación y recargan el agregado. No se reintenta una escritura tras conflicto o respuesta perdida.

## Entrega y comprobaciones

Implementados `CustomerService`, `ICustomerRepository`, validadores y `SqlCustomerRepository`, registrados en DI. WPF recibe el servicio por constructor y presenta los ocho apartados de la ficha. Se reutilizan el contenedor de formularios, los recursos Light/Dark y la gestión de sesión de DEV-12. Las lecturas se cancelan/ignoran al cambiar la sesión; los guardados exitosos recargan su versión; un conflicto o respuesta perdida bloquea repetir desde ese formulario.

El resumen y cada historial se consultan explícitamente desde la ficha; no se precargan listas ilimitadas. Ventas, cuentas y abonos tienen paginación independiente. No se exponen costos ni se incluyen botones para registrar cobros/ventas que aún no están implementados. La baja lógica se hace editando el estado con motivo y confirmación.

Pruebas añadidas:

- 16 casos xUnit de permisos separados, normalización, direcciones FISCAL/ENTREGA/OTRA, validación, versiones, descarte, respuesta perdida, cambios de sesión y consulta/paginación de historiales. Se ejecutan también en Linux enlazando los ViewModels independientes de WPF.
- Suite SQL `--run-customers` para alta, duplicados, concurrencia de dos estaciones, rollback de auditoría, datos ajenos, baja/reactivación, independencia del crédito, historial/abonos/saldos y revocación de permisos reales. Se incorpora a `verify.ps1 -SqlIntegration` y al CI de Windows. Los fixtures sólo se crean en `GoDental_Tests_<GUID>`; no usan la base del negocio.
- Smoke WPF ampliado para ocho pestañas, cuatro formularios, ambas paletas, permisos y tamaño lógico de 1093×614 además de 1366×768.

La validación de este incremento no puede darse por aprobada en Windows desde el entorno Linux. Las suites SQL/visuales están preparadas y deben ejecutarse allí; el permiso del usuario para continuar después de DEV-12 no se registra como resultado de pruebas. Los pasos de VS Code están en [DEV-14](DEV-14-VSCode-Windows.md). El siguiente bloque funcional es Ventas/POS; no se inicia dentro de este incremento de Clientes.

Resultados ejecutados en el entorno cloud Linux: `dotnet build GoDental.sln -c Release -p:EnableWindowsTargeting=true` correcto, sin advertencias; `dotnet test GoDental.sln -c Release` con 72 casos correctos. Smoke WPF no pudo iniciarse por ausencia de `Microsoft.WindowsDesktop.App`; `--run-customers --localdb` devolvió `PlatformNotSupportedException`. El verificador PowerShell tampoco está disponible aquí. No se modificaron migraciones SQL ni bases de usuario.
