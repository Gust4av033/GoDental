# DEV-20 · Códigos automáticos y cierre de aplicación

19/10/2026, hora de El Salvador. Incremento solicitado: facilitar altas y terminar el proceso al cerrar la ventana activa.

## Identificadores

Las altas normales de clientes, productos, proveedores y cajas envían el código vacío; SQL asigna `CLI-`, `PRD-`, `PROV-` y `CAJ-`, con diez dígitos, dentro de la misma transacción que el registro y su auditoría. Se reutiliza `sis.SecuenciasDocumento`, sin reinicio anual. La migración 014 agrega el procedimiento específico `sis.sp_SiguienteCodigoMaestro`: sólo admite esos cuatro tipos, exige transacción, evita códigos históricos ya existentes y rechaza el agotamiento del rango sin truncarlo. La secuencia se revierte si falla el alta. No se calculan códigos con MAX+1 ni en WPF.

La persistencia conserva la compatibilidad con códigos explícitos de datos históricos/importaciones. Los formularios normales muestran el código como consulta y explican que se asigna al guardar; las ediciones conservan el existente. No se renumeran clientes/productos/proveedores/cajas existentes. NIT, NRC, referencias de pago, lote del fabricante y SKU del proveedor son referencias externas y se capturan tal como corresponden.

El código de empleado corresponde a las cuentas del personal existentes (`seg.Usuarios`), sin crear un módulo de RRHH: 014 agrega `CodigoEmpleado` calculado y persistido como `EMP-` + IdUsuario a diez dígitos, con unicidad. Incluye al primer administrador y las cuentas anteriores sin modificar nombres de acceso, contraseñas o roles. El listado de usuarios muestra ese código.

El resto de registros ya dispone de ID o correlativo asignado por SQL; no se añade un código redundante a contactos/direcciones ni se reemplazan identificadores externos. 014 conserva 76 tablas y 8 vistas, y eleva los procedimientos a 18.

## Cierre

El coordinador identifica su ventana activa explícitamente, bloquea cambios de ventana durante la salida y detiene el host antes de apagar el dispatcher. La limpieza normal se espera antes de `Application.Shutdown`; `OnExit` conserva una liberación síncrona de respaldo, sin `async void` pendiente durante la destrucción de WPF. Se mantienen las protecciones de operaciones financieras pendientes y se revisan las escrituras de catálogo que también deben impedir el cierre durante su ejecución.

La verificación cubre altas simultáneas, colisiones con códigos históricos, rollback de secuencia/auditoría, códigos de personal, campos automáticos y salida del proceso en Windows. No se instalan migraciones en la base del usuario desde este entorno.
