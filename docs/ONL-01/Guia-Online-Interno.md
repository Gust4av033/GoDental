# Usar Comercial / Online

Online organiza la búsqueda de nuevos negocios y la preparación de campañas. La versión
interna permite trabajar con datos registrados manualmente; no busca contactos en Internet
ni envía WhatsApp, mensajes de redes o correos. La referencia visual es `Imagenes/221200`.

1. **Zonas de prospección:** crea una zona con departamento, municipio opcional y prioridad.
   Menor número aparece primero. El conteo corresponde a prospectos guardados en ese territorio.
2. **Prospectos:** registra el nombre del negocio, contactos públicos, ubicación y fuente con
   fecha. Usa Comparar coincidencias para revisar si ya existe como prospecto o cliente.
   Para campañas de WhatsApp, registra su campo WhatsApp explícitamente: un teléfono publicado
   no se interpreta automáticamente como cuenta de WhatsApp autorizada.
3. **Ficha y origen:** selecciona el prospecto y pulsa Consultar ficha. Puedes editar sus datos,
   añadir fuentes, revisar coincidencias/historial o registrar una petición de no contactar.
4. **Segmentación:** crea un grupo por territorio, tipo de negocio, estado, fuente y canal.
   Los filtros se combinan; los opcionales pueden quedar sin especificar. Selecciona el segmento
   y pulsa Vista previa. No incluye convertidos, descartados ni contactos excluidos.
5. **Plantillas de mensaje:** crea el texto del canal correspondiente. `{nombre}` es el único
   marcador disponible. La vista previa usa un negocio de ejemplo; conserva el contenido como texto.
6. **Campañas:** con un segmento y una plantilla seleccionados del mismo canal, pulsa Nueva
   campaña y asigna nombre. El borrador copia sus criterios y mensaje. Cambiar después el segmento
   o la plantilla no cambia esta copia: para otro contenido, crea otra campaña.
7. **Aprobación:** selecciona la campaña en Campañas y pulsa Preparar para aprobación. En
   Aprobación, consulta destinatarios y revisa el mensaje. Una persona con permiso de aprobar
   puede aprobar la preparación, volver a borrador para renovar destinatarios o cancelarla.
   Máximo 1000 candidatos por campaña; acota el segmento si lo supera. Aprobar no envía mensajes.
   Si una ficha cambia o se excluye, aparece marcada para revisión incluso después de aprobar.
8. **Respuestas / seguimiento:** consulta primero la ficha y pulsa Registrar gestión. Guarda una
   nota, contacto o respuesta que ya ocurrió fuera de GoDental, su fecha y próxima gestión opcional.
   INTERESADO actualiza el estado; SIN_INTERES descarta; NOTA conserva el estado. No se genera
   un chat remoto. Las respuestas entrantes y notas conservan cualquier exclusión existente.
9. **Conversión a cliente:** registra primero al cliente desde Clientes si no existe y confirma
   su compra en POS. Busca esa venta confirmada, revisa que corresponde al mismo negocio y vincula.
   Se conservan fuentes/exclusiones; no crea otra venta, crédito ni cuenta de usuario.
10. **No contactar:** registra las exclusiones por canal o bloquea la ficha. Reingresar un contacto
    o comprar no levanta la exclusión. Esta versión no ofrece una acción para quitarla.

Los listados se consultan con Buscar/Consultar y tienen paginación. Si dos personas modifican
el mismo registro, vuelve a consultarlo antes de reintentar. Si queda una operación pendiente,
resuélvela desde Operaciones pendientes antes de crear otra equivalente.

La imagen también muestra mapa, bandeja remota, programación y envíos. Esas funciones necesitan
fuentes/proveedores oficiales y reglas de consentimiento; siguen pendientes. La preparación
interna no acredita consentimiento comercial. El detalle de alcance y continuación está en
[DEV-47](../DEV-47-Online-Referencia.md).
