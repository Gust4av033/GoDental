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

SQL: confirmada aunque se revocó permiso, hash/propietario incorrectos, ausencia no terminal, lock ocupado, resolución contra envío concurrente/tardío, rechazo persistente y rollback de auditoría, reaplicar 015. Local/UI: fallo de disco antes de enviar, reconstruir servicio con diario existente, separación de identidades, limpieza fallida, respuesta tardía/sesión y recuperación sin segundo cobro. CI Windows ejecutará SQL y render de UI; Linux compila WPF y ejecuta pruebas unitarias/diario real con archivos temporales. No se modifica la base del usuario desde este entorno.
