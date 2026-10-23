# DEV-49 — Corrección de búsqueda y mejoras de acceso

23/10/2026, El Salvador. Paréntesis solicitado por el usuario sobre DEV-48; no cambia la
continuación Meta/S1 ni habilita envío/chat. Fuentes: DM-01 §15/16, DB-01 §7.9, ARQ-01,
SEC-01, UX-01 y DEV-48. Diseño previo a los cambios.

## Búsqueda

La captura sólo identifica un HTTP no exitoso, sin su código: no demuestra caída, bloqueo o
consulta rechazada. Separar 400, 403, 429, redirección y 5xx, sin mostrar cuerpos/URLs arbitrarias
ni desactivar TLS. Conservar límites, cancelación, permisos SQL y revisión antes de guardar.
El entorno cloud no autoriza Overpass: verificar contratos con fixtures y dejar comprobación
real en la estación, sin presentar las pruebas simuladas como aceptación del proveedor.

Problema independiente confirmado en el código fuente del proveedor: `out ... tags` usa
ID+TAGS, sin COORDS en los nodos; `center` añade el centro de ways/relations, no coordenadas de
nodes. Usar `out body center 101` conserva coordenadas y contactos, con límite de respuesta.
Referencia: drolbr/Overpass-API, frontend/map_ql_parser.cc y statements/print.cc.

Permitir elegir explícitamente servidor principal o alternativo de la misma fuente pública OSM:
Overpass API (overpass-api.de) y Private.coffee (overpass.private.coffee). Lista fija, HTTPS,
sin redirecciones ni URL libre. Ambos figuran en la configuración de overpass-turbo:
https://github.com/tyrasd/overpass-turbo/blob/cf9252a3042cb5c5f21a9fbc2b2b7251b90ff90b/js/configs.ts
El cambio es manual, sin failover/reintento automático ni evasión de límites: una espera común
mínima de un minuto, ampliada por Retry-After (hasta un día), también al cambiar de servidor.
Cambiar servidor cancela y descarta los resultados anteriores. Mostrar código HTTP y orientación
útil; no asegurar disponibilidad de un servicio comunitario. El botón largo debe ajustar su ancho.

## Acceso

Componente visual reutilizable para mostrar/ocultar contraseña, con PasswordBox enmascarado por
defecto. Texto visible sólo en el control: sin binding del secreto al ViewModel, configuración,
logs o persistencia. Desactivar historial de deshacer/corrector del campo visible; ocultar y
limpiar ese campo al enmascarar o desactivar la ventana. Limpiar ambos campos al enviar,
deshabilitar, cambiar contexto/modo o salir de la vista. No prometer borrado criptográfico de
strings .NET. Aviso de Bloq Mayús, foco/teclado accesible y recursos Light/Dark compartidos.

Validar campos vacíos antes de llamar al gateway (no consumen intentos SQL), conservar el
bloqueo/errores genéricos existentes y permitir volver al acceso desde cambio de contraseña,
sin crear sesión ni omitir el cambio obligatorio. Texto de ayuda para pedir restablecimiento al
administrador; no crear enlaces de recuperación ficticios ni recordar contraseñas.

## Continuidad y actualización

Sin migración nueva; siguen pendientes en la PC las que falten de **021 → 022 → 023 → 024 →
025 → 026**, en orden. Después de este paréntesis continúa el servicio central HTTPS, identidad
confiable/mínimo privilegio e inbox/outbox durables de DEV-48/S1; Cloud API, autorización comercial
y plantillas oficiales antes de transmitir. Registrar pruebas y limitaciones al cerrar.
