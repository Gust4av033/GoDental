# DEV-48 — Online externo: mapa, descubrimiento y preparación Meta

23/10/2026. Continúa DEV-47 por solicitud expresa del usuario. Fuentes: DM-01 §15, DB-01 §7.9,
ARQ-01, ONL-01, SEC-01 y DEV-45. Diseño anterior a contratos/implementación de este incremento.
El usuario dispone de cuentas WhatsApp Business/Meta Business, no de proveedor cartográfico ni
configuración conocida de servidor HTTPS. El usuario confirmó después que usa la app del teléfono; Cloud API aún no está habilitada/verificada.
La pregunta de autorización comercial por canal sigue pendiente; ningún envío se habilita por inferencia.

## Mapa local gratuito

Mapa vectorial de los 14 departamentos con geometría real de Who's On First / foursquare Quattroshapes (CC-BY 2.0),
referencia cartográfica, no certificación de límites legales. Datos fijados a revisión y blobs
verificables en docs/licenses/ElSalvador-Map-Sources.json; licencia completa distribuida y
atribución/enlace visibles. Se extraen geometría, nombre, ID y posición de rótulo, sin datos personales.
No usa tiles, cuentas ni llamadas de red. Clic/teclado selecciona departamento; selector textual
alternativo y catálogo de 44 municipios vigente permanecen. No inventa límites municipales.
Zonas guardadas conservan su versión y sólo se modifican mediante sus operaciones existentes.

## Descubrimiento gratuito con revisión

Se define OpenStreetMap mediante la API pública Overpass como fuente inicial, de sólo lectura:
consulta acotada de negocios etiquetados amenity=dentist o healthcare=dentist, dentro de la
extensión del departamento y filtrados por geometría local. No Google Places, scraping de redes,
listados privados ni extracción arbitraria de páginas. Punto representativo de way/relation requiere
verificar ubicación; no es dirección fiscal ni prueba de residencia. Municipio se elige por el usuario
con el catálogo actual, sin geocodificación masiva ni inferir distritos.

Una consulta explícita automatiza la obtención; máximo 100 resultados y espera/tiempo/tamaño
acotados, sin reintentos automáticos, scheduler ni descarga global. Rate limit, cancelación, errores
sanitizados, TLS y host/ruta fijos; sin redirecciones, cookies, credenciales ni URLs del resultado
solicitadas. Identificación GoDental con URL pública del repositorio. Respeta indisponibilidad/429;
servicio comunitario sin garantía y sujeto a política de uso. En despliegue de mayor volumen,
configurar proveedor/instancia con contrato; no abusar de la instancia pública.

Resultado en memoria: nombre, contactos explícitamente publicados, coordenada y enlace al
objeto OSM. No persiste ni contacta automáticamente; revisar uno abre el formulario existente
con fuente/fecha y duplicados comprobados nuevamente al guardar. No atribuye WhatsApp a un
simple teléfono, no inventa datos faltantes y conserva atribución de OpenStreetMap/ODbL.
La búsqueda exige permiso COMERCIAL.PROSPECTAR revalidado en SQL y descarta resultados
si cambian sesión/departamento. No es Worker con la aplicación cerrada. No agrega tablas.

## Meta y seguridad antes del transporte

Preparar contrato oficial WhatsApp Cloud API y validador de webhook: firma HMAC-SHA256 sobre
bytes originales, comparación constante, límites de tamaño/profundidad, WABA/PhoneNumberId
esperados y deduplicación antes de efectos. Secretos sólo del servidor mediante referencias;
ningún bearer/App Secret en WPF/BD/configuración versionada, URLs o logs. El hub.verify_token
del alta viaja por query según el protocolo Meta: redaccionar query y cabeceras en el host. No usar WhatsApp Web ni SDK
archivado como dependencia; especificación vigente/versiones/permisos se verifican con la cuenta
Meta antes de activar. El SDK oficial archivado sólo sirve como referencia histórica del formato.

El servidor HTTPS recibe mensajes sin exponer SQL Server. La arquitectura y despliegue de ese
servicio central, identidad SQL mínima y API/autenticación se cierran en S1 (SEC-01), junto con
reglas de consentimiento, plantillas oficiales y reconciliación de envíos inciertos. No usar el
IdUsuario proporcionado por una laptop como identidad confiable del remitente. APROBADA interna
sigue sin equivaler a enviar. No declarar servicio publicado ni chat bidireccional operativo sin
configurar/verificar esos requisitos. La respuesta pendiente del usuario condiciona el envío,
no el mapa ni la búsqueda pública.

## Actualización y continuidad

Este bloque independiente no requiere migración; siguen pendientes localmente **021 → 022 →
023 → 024 → 025 → 026**. Registrar aquí cualquier esquema posterior antes de SQL.
No se aplica la base personal desde cloud. Después del mapa/descubrimiento, cerrar configuración
Meta y S1 antes de transmisiones reales; documentar límites exactos y pruebas sin mensajes reales.

## Contratos Meta implementables sin activar el servicio

`MetaCloudTemplateTransport` usa exclusivamente POST al host fijo graph.facebook.com, ruta
con versión/PhoneNumberId validados y bearer en cabecera; texto de plantilla oficial/nombre,
idioma y parámetros explícitos, nunca texto interno tomado por aprobado. No sigue redirecciones,
no hace reintentos y limita tiempo/bytes. ACEPTADO exige producto, destinatario y wamid verificados;
4xx con error estructurado sólo devuelve código; timeout/red/5xx/respuesta incompleta son
INCIERTO, no habilitan un reenvío. El invocador futuro debe conservar permiso/outbox antes del IO.

`MetaWebhookVerifier` valida tamaño/firma antes de parsear, cuenta/número, profundidad,
propiedades duplicadas, identificadores/fechas y texto acotado. Deduplica dentro del lote y rechaza
conflictos; no descarga adjuntos. La deduplicación durable, almacenamiento y respuesta HTTP son
responsabilidad del siguiente servicio central, no se declaran implementados por este parser.
Los contratos son de servidor y no se registran en DI de WPF. No se implementa una aprobación
falsa que permita usarlos saltando consentimiento ni se introducen secretos en ajustes locales.

La búsqueda pública sin credenciales no envía datos de Clientes ni privilegios a terceros;
revalida COMERCIAL.PROSPECTAR en SQL antes/después. No equivale a cerrar S1 para acciones de
la cuenta Meta. S1 sigue bloqueando su activación, no la consulta pública con revisión humana.
Guía concreta de alta/cuentas/HTTPS y límites: [Configuración Meta](ONL-01/Configuracion-Meta.md).

El usuario confirmó que utiliza la app del teléfono. La configuración guiada propone primero
el número de prueba oficial de Meta; no migrar el número operativo ni simular coexistencia
antes de verificar el proceso elegible en la cuenta. La autorización por canal sigue pendiente
de respuesta; las transmisiones reales permanecen deshabilitadas.

## Verificación de este incremento

Release local de la solución, incluyendo compilación WPF para Windows: 0 advertencias/errores.
Pruebas unitarias de geometría real, huecos/islas, permisos, cancelación, cambios de sesión,
respuesta pública acotada y firma/transporte Meta con respuestas sintéticas. La consulta real
Overpass y aceptación Meta no se verificaron en vivo en este entorno; no se enviaron mensajes.
La validación Windows/SQL/WPF del código se registra al finalizar CI; no confundir sus fixtures
sin red con aceptación del proveedor o despliegue en las PCs.
