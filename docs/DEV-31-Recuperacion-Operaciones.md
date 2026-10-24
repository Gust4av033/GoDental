# DEV-31 · Recuperación segura de operaciones · etapa 2.0A

21/10/2026. Diseño previo a la implementación, autorizado al continuar el orden de DEV-30. Se consultan DM-01, DB-01, ARQ-01, SEC-01 y DEV-15/17/30. Se conserva `feature/desarrollo` y el límite Venta != DTE. No cambia la política comercial.

## Resolución SQL

015 agrega `EstadoResolucion` a `sis.OperacionesMvp`, con default CONFIRMADA para los registros existentes. CHECK permite CONFIRMADA con EntidadId positivo o RECHAZADA_DEFINITIVA con EntidadId=0; cero no es un recibo comercial. No agrega tablas ni procedimientos (76/8/18). Agrega también HashRecuperacion binary(32) nullable: en autorización administrativa conserva la huella del DTO sin credenciales, mientras HashSolicitud mantiene su formato histórico con Id del autorizante; el reenvío confirmado sigue comparando el hash histórico completo. Es aditiva e idempotente.

`OperationTicket(OperationId, Type, RequestHash)` identifica la solicitud normalizada exactamente como el repositorio; hash SHA256 hexadecimal de 32 bytes. El catálogo cerrado de tipos y su normalización pertenecen a Application. No se cambia la huella de solicitudes históricas válidas.

`ResolveAsync(ticket, rejectIfAbsent)` exige usuario activo autenticado, propiedad y coincidencia de tipo/hash. Usa el mismo `GoDental:MVP:<GUID>` y bloqueo Exclusive/Transaction que las escrituras. Devuelve Confirmed con recibo, InProgress si el lock está ocupado, Unconfirmed si no hay fila, o RejectedDefinitively. Ausencia no equivale a rechazo. La acción explícita de resolver/liberar, si no hay confirmación y obtuvo el bloqueo, registra un rechazo definitivo y bitácora en una transacción. No reejecuta el negocio ni anula una operación confirmada. Un escritor tardío debe observar el rechazo antes de cualquier efecto. Confirmación/rechazo son terminales.

El reenvío consulta el registro después de validar identidad activa y adquirir lock, antes de exigir permisos de negocio: recuperar el recibo propio no vuelve a autorizar una venta. Nuevos efectos conservan todos sus permisos y validaciones. Usuarios desactivados o que deben cambiar clave necesitan recuperar acceso con el administrador antes de reconciliar; no se elude esa política.

## Diario local y reinicio

`IPendingOperationStore` persiste únicamente ticket, fecha, scope SQL, usuario y estación, sin payload comercial, contraseña administrativa ni PAN/CVV. La solicitud completa permanece en memoria para reenvío manual durante la misma sesión. Al reiniciar no se reconstruye ni reenvía: se consulta o cierra definitivamente la solicitud antes de capturar una nueva. La huella normalizada es suficiente para reconciliar y bloquear un envío tardío, sin guardar datos de clientes/pagos.

Infrastructure guarda un diario en LocalApplicationData/GoDental, con reemplazo atómico, flush a disco y exclusión entre procesos. Reservar una operación comprueba y escribe bajo el mismo lock: sólo una solicitud pendiente por base/usuario/equipo en este perfil. Si falla la persistencia no se envía. Error o corrupción del diario bloquea nuevas escrituras MVP y se informa; no se borra ni reemplaza silenciosamente. Scope/usuario/equipo diferentes no se mezclan. No hay una operación que borre pendientes sin comprobar un resultado terminal en SQL.

La eliminación local ocurre sólo tras confirmación o rechazo definitivo serializado en SQL, antes de liberar la captura. Un rechazo conocido del envío se cierra también bajo el bloqueo antes de limpiar el diario. Ante fallo al limpiar se conserva el pendiente para reconciliar. Al cambiar de sesión se limpia la presentación, no el diario anterior. Respuestas tardías no se aplican a otra sesión. El diario productivo es durable; las pruebas pueden usar implementación volátil aislada.

## UI

Una sección global de Operaciones pendientes consulta las solicitudes del usuario/base/equipo actuales al entrar. Permite Consultar y Resolver y liberar, explicando que no anula una venta confirmada y que nunca vuelve a enviar el cobro. Las páginas existentes conservan consultar/reenvío idéntico; el cierre normal puede conservar pendientes durables, pero sigue bloqueado durante escrituras activas. No se libera una captura sólo por timeout o ausencia de recibo.

## Verificación requerida

SQL: confirmada aunque se revocó permiso, hash/propietario incorrectos, ausencia no terminal, lock ocupado, resolución contra envío concurrente/tardío, rechazo persistente y rollback de auditoría, reaplicar 015. Local/UI: fallo de disco antes de enviar, reconstruir servicio con diario existente, separación de identidades, limpieza fallida, respuesta tardía/sesión y recuperación sin segundo cobro. CI Windows ejecuta SQL y render de UI; Linux compila WPF y ejecuta pruebas unitarias/diario real con archivos temporales. No se modifica la base del usuario desde este entorno.

## Uso y actualización

1. Cerrar GoDental en todas las estaciones, actualizar `feature/desarrollo` y aplicar 015 después de 014 en la base elegida. No mantener una versión anterior operando contra el ledger actualizado.
2. Al entrar, el aviso **Revisar pendientes** lleva al diario de la cuenta/base/estación actuales. **Consultar estado** no vuelve a registrar la operación.
3. Si sigue en curso, conservar el pendiente y consultar más tarde. Si ya confirmó, se recupera el mismo registro; abrir su módulo y actualizar permite revisar el detalle.
4. Si no hay confirmación, **Resolver y liberar** impide que ese GUID se confirme posteriormente. Si la confirmación ganó la carrera, se devuelve su recibo; nunca se anula esa operación. Después se revisan los datos antes de capturar una nueva solicitud.
5. Durante la sesión sigue disponible el reenvío manual de la solicitud exacta. Tras reiniciar sólo se consulta o resuelve; no se reconstruyen carritos ni se conservan credenciales administrativas.

El diario se conserva en `%LOCALAPPDATA%\GoDental\pending-operations.json`. No borrarlo para desbloquear la pantalla: un error de lectura/limpieza se diagnostica conservando el archivo. No contiene payload de venta, datos de tarjeta ni contraseñas.

La prueba de salida de WPF incluye acceso, espacio de trabajo normal y espacio de trabajo con un pendiente durable; esta última exige que el proceso termine y el diario conserve su registro. Otra prueba de carga diferida verifica que el aviso no pierda una notificación recibida mientras se consultaba el diario.

## Resultado verificado

[CI Windows #106 correcto](https://github.com/Gust4av033/GoDental/actions/runs/39644476282), código **`c3d3445cde02c1f24c83d3ada8b454e24147504a`**: compilación Release con **0 advertencias/errores**, **187 xUnit**, **399 comprobaciones SQL** y **575 WPF**, sin errores de binding. SQL: 40 inventario, 51 seguridad/catálogo, 67 conteos, 38 proveedores, 33 clientes, 128 MVP, 18 códigos y 24 recuperación. Se consultaron el resultado y los logs del job `119376291704`.

Las 24 comprobaciones nuevas cubren confirmación/ausencia/cierre, propietario y huella, lock ocupado, carreras con el envío, rechazo de un envío posterior, rollback de auditoría, permiso comercial revocado y reaplicación. Dos procesos hijos se terminan después de preparar el diario: uno antes del envío y otro después de que el servidor confirme, antes de recibir el resultado. Reconstruir el servicio recupera el estado y limpia sin enviar otra operación comercial.

WPF comprueba la pantalla y su acción de resolución en Light/Dark y tamaño compacto, más tres procesos que terminan por sí mismos al cerrar acceso, espacio de trabajo y espacio de trabajo con pendiente durable. El último verifica que el diario conserva una solicitud después de detener/liberar el host. Las capturas son automáticas y no certifican monitor/DPI físico ni impresora del usuario.

Localmente: Release sin advertencias/errores, 187 xUnit y proceso aislado terminado conservando su ticket en disco. El ajuste final del arnés usa una comprobación específica de la página de recuperación, que no tiene pestañas comerciales. Las modificaciones posteriores a `c3d3445` de esta entrega sólo registran documentación.

El bloque **2.0A queda implementado y validado automáticamente**. No se aplicó 015 en la base del usuario ni se certificó el piloto de dos estaciones. La siguiente entrega es **2.0B: parámetros comerciales consumidos, mantenimiento de códigos de barras y registro manual de tarjeta**, antes de compras/recepciones/importaciones.

La entrega posterior 2.0B se implementa en [DEV-32](DEV-32-Complementos-MVP.md), con migración 016. La evidencia de 2.0A anterior se conserva como referencia histórica.
