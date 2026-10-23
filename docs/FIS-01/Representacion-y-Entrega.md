# Representación gráfica, entrega y consulta pública

**PREPARADO — infraestructura PDF comercial disponible; RG/QR fiscal sin implementar.**
F08 §11–12, pp.23–27, anexo I §4 p.42 y columnas de representación de anexos II/III;
F03 para consulta pública. Contrato exacto de archivo, campos/leyendas, QR/URL/parámetros,
datos por tipo y condiciones de entrega: PENDIENTE_DE_CONTRATO_OFICIAL (T01–T04).

## Representación y reimpresión

Reutilizar QuestPDF/formatos comerciales configurables como infraestructura de renderizado,
no como prueba de que el comprobante actual sea una RG conforme. Producir desde snapshot y
resultado fiscal inmutables, sin consultas a datos actuales de Clientes. Conservar versión de
plantilla, hash y artefactos; toda reimpresión recupera la misma evidencia sin generar otro DTE.

F08 §11.3 requiere correspondencia/uniformidad por tipo y advierte que PDF/imágenes no son
DTE. Una plantilla de 58/80 mm no se habilita para uso fiscal sólo porque cabe el texto: validar
campos, legibilidad, leyendas y QR según norma. TEST requiere advertencia visible de ensayo sin
valor legal. No mostrar sello ausente ni “ACEPTADO” por revisión interna. Una corrección de RG
debe conservar fidelidad y versión; no modifica ni regenera el DTE original.

Validar presencia/condiciones de Código de Generación, Número de Control, sello y demás
datos con fuentes vigentes. QR fiscal usa exclusivamente mecanismo oficial verificado. Un QR
propio de descarga se identifica como tal y no sustituye al oficial. No se define hoy ninguna URL.

## Entrega desacoplada

Preparar obtención del archivo DTE oficial y RG, impresión, acceso autorizado y correo si existe,
además de canales futuros legal/técnicamente habilitados. El entregable oficial no se arma
inventando un wrapper entre JSON, firma y sello: T01/T04 deben especificarlo.

No exigir correo/teléfono para completar FE cuando la norma permite omitirlos. La entrega y
evidencia deben cubrir también a consumidor final sin cuenta/cliente creado, con acceso acotado
y sin URL pública enumerable. Retención/expiración de enlaces propios se definirá por seguridad
sin borrar el artefacto fiscal que debe conservarse. No filtrar PII ni incluir secretos en URLs.

Registrar destinatario/canal autorizado, artefactos, fecha y resultado. Una falla de correo o
impresora no vuelve a emitir el DTE. No prometer entrega por WhatsApp por tener la app del
teléfono: requiere proveedor oficial, habilitación y consentimiento conforme al módulo Online.

## Consulta pública y reconciliación

Acción futura “Verificar en Hacienda” sólo tras verificar el mecanismo oficial vigente. F03
describe una consulta humana y sus estados/errores; no habilita scraping ni inventa una API.
No simular el resultado consultando la propia SQL. Mostrar resultado/ambiente y distinguirlo de
reconciliación técnica; una consulta pública sin resultado no demuestra que se puede reenviar.

Pruebas: fidelidad a artefacto, campos/leyendas por tipo, QR oficial verificable, formatos
legibles, reimpresión sin efectos, receptor sin correo, autorización/descarga cruzada, entrega
fallida sin duplicación, consulta oficial y separación de enlace propio/fiscal. Contratos antes de
fixtures; hardware real y reglas fiscales se validan por separado.
