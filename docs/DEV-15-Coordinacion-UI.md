# DEV-15 · Coordinación UI y lógica

Organización vigente: DEV-08. Este chat trabaja en `feature/ui`.

| Necesidad | Estado | Contrato y verificación |
| --- | --- | --- |
| Preparar acceso y detectar primer administrador | conectado | `IIdentityGateway.NeedsAdministratorAsync()`, sin entradas; booleano, sin sesión previa. Revisar conexión, esquema y permisos en Windows; una conexión comprobada no certifica el esquema. |
| Guardar conexión y reintentar acceso | conectado | Ajustes y acceso comparten configuración; el usuario pulsa Reintentar acceso tras cerrar Ajustes. No se reintentan escrituras automáticamente. |
| Conteos, proveedores y clientes | conectado | Contratos documentados en DEV-09 a DEV-13; pendiente validación WPF/SQL en Windows del nuevo estilo. |

No se necesitan contratos nuevos para este refinamiento visual. Si vuelve a fallar la preparación del acceso, recoger el tipo de error del registro local sin credenciales y reproducir contra la base de pruebas antes de atribuirlo a lógica.
