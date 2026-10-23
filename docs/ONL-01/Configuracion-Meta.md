# Preparar WhatsApp Business Platform y el servidor

El usuario confirmó que usa la app del teléfono. Tener WhatsApp Business en el teléfono y una cuenta Meta Business permite iniciar la preparación.
Para que GoDental envíe y reciba, la cuenta debe tener habilitada **WhatsApp Business Platform /
Cloud API** y un servicio central con HTTPS. No compartas tokens ni secretos en chats, capturas,
Git, archivos de la aplicación de escritorio o campos de una plantilla.

## Lo que puedes usar ahora

En Online → Zonas, el mapa de departamentos funciona sin Internet. Selecciona departamento y
pulsa Buscar negocios dentales para consultar OpenStreetMap mediante Overpass. La fuente es
comunitaria y puede estar incompleta; la consulta es acotada y espera un minuto entre búsquedas.
Si el servidor falla, DEV-49 muestra el código HTTP y permite seleccionar Private.coffee como
alternativo. La selección es manual y mantiene la espera común entre consultas y Retry-After;
no hay reintentos automáticos. No se garantiza disponibilidad de ninguno de los servidores.
Los resultados no se guardan ni contactan: selecciona uno, pulsa Revisar para registrar y verifica
su municipio/contactos/fuente. Guardar vuelve a comprobar duplicados. La consulta no asigna
WhatsApp a cualquier teléfono ni crea una autorización de publicidad.

El mapa incluye geometrías abiertas de Who's On First / foursquare Quattroshapes (CC-BY 2.0),
con crédito/enlaces visibles y licencias distribuidas. La búsqueda conserva crédito de
OpenStreetMap (ODbL). No requiere comprar mapas ni una cuenta cartográfica.

## Datos y habilitación en Meta

1. En el panel oficial de Meta para desarrolladores, revisar si existe una aplicación del negocio
   con el producto WhatsApp habilitado. Confirmar las condiciones actuales de alta, número,
   permisos y pruebas. El proceso puede variar según la cuenta; no asumir que activar la app
   del teléfono habilitó Cloud API ni que su número puede migrarse sin comprobar compatibilidad.
   Comienza en https://developers.facebook.com/apps/ con la aplicación del negocio y el
   **número de prueba oficial** del producto WhatsApp. Obtén de ese ambiente los identificadores
   del paso 2 y prepara únicamente destinatarios propios de prueba según el panel. Después se
   verifica la elegibilidad del número habitual y su proceso de coexistencia/migración en Meta.
2. Obtener **Business Account ID (WABA)** y **Phone Number ID** del producto WhatsApp. Son
   identificadores, no el teléfono escrito ni la contraseña de Facebook. Mantener el negocio,
   número y ambiente de pruebas/producción identificados antes de configurar el servicio.
3. Configurar un token de servidor con permisos mínimos autorizados para ese negocio y número,
   su rotación/revocación y el App Secret para verificar notificaciones. Guardarlos mediante un
   almacén protegido del servidor. Las laptops no necesitan recibir ese token ni el App Secret.
4. Confirmar la versión Graph API vigente soportada por la cuenta; la librería exige versión
   explícita y no inventa una versión de producción. El SDK Node oficial archivado no se instala.
5. Registrar la plantilla oficial y su idioma/parámetros conforme a Meta. Las plantillas de texto
   internas de GoDental aún no son plantillas aprobadas del proveedor. Antes del primer envío
   debe verificarse la regla de autorización por contacto/canal y la exclusión vigente.

Documentación oficial para comprobar estos pasos:

- https://developers.facebook.com/docs/whatsapp/cloud-api/get-started
- https://developers.facebook.com/docs/whatsapp/cloud-api/webhooks
- https://developers.facebook.com/docs/whatsapp/business-management-api/message-templates

Las cuentas gratuitas de negocio no establecen por sí mismas las tarifas, cupos o disponibilidad
de la API. Comprobarlos en el panel oficial antes de habilitar tráfico; las pruebas de GoDental
no hicieron envíos reales ni generaron cargos del proveedor.

## Qué es el servidor HTTPS

Es el punto público donde Meta entrega notificaciones firmadas y desde donde se realiza el
transporte autorizado. SQL Server queda en la red privada. Las laptops se comunican con el
servicio autenticado; no se publica el puerto de SQL ni se envía el access token/App Secret dentro de la URL.
El parámetro `hub.verify_token` del alta viaja por query por protocolo Meta: el host deberá
redactar query/cabeceras sensibles de sus logs y responder el desafío como texto plano.

El servicio debe estar disponible aunque se cierre WPF, conservar mensajes y deduplicar antes
de responder a Meta. Necesita dominio/URL HTTPS estable, certificado válido, identidad y permisos
SQL mínimos, almacenamiento cifrado, respaldo y monitoreo. Un dominio por sí solo no ejecuta
el servicio. Hosting propio y hosting externo son opciones; la elección/despliegue siguen por
cerrar con el equipo disponible. No presentar una URL de prueba temporal como despliegue final.

El receptor público validará `X-Hub-Signature-256` sobre los bytes originales usando App Secret,
WABA y Phone Number ID configurados; el token de verificación sirve para el alta del webhook,
no sustituye esa firma. No descargar adjuntos ni seguir URLs recibidas automáticamente.

## Estado de esta entrega y siguiente bloque

Se implementan el mapa/descubrimiento de sólo lectura y los componentes probados de transporte
Cloud API y verificación de notificaciones. Los componentes Meta son de servidor, **no están
registrados en WPF ni publicados como un webhook**. No hay todavía un botón de envío habilitado,
un servidor desplegado ni chat bidireccional conectado. Aprobar una campaña sigue sin transmitir.

Antes de activarlos falta cerrar: regla de autorización por canal (respuesta pendiente), acceso
real a Cloud API/plantillas, servicio central autenticado, buzón/outbox durables con reconciliación
y mínimo privilegio (S1 de DEV-45). Si se pierde una respuesta de envío, el transporte devuelve
INCIERTO y no reintenta automáticamente. ACEPTADO identifica aceptación por API; no prueba
entrega ni lectura del destinatario. Sólo un evento verificado y correlacionado podrá hacerlo.
Instagram y Facebook Messenger necesitan sus permisos/contratos específicos; una cuenta de
WhatsApp no los habilita automáticamente. No se simulan sus estados ni conversaciones.

No hay migración nueva para este incremento: localmente siguen pendientes las que falten de
**021 → 022 → 023 → 024 → 025 → 026**, en orden, con respaldo previo. No se modificó tu BD
personal desde el entorno de desarrollo.
