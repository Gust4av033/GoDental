# Custodia, instalación de certificado y rotación

**PREPARADO — procedimiento objetivo; no existe todavía una pantalla/comando seguro de
instalación fiscal.** Certificados/API TEST/PROD están PENDIENTE_DE_CREDENCIALES. Formato,
requisitos de instalación y protocolo de firma: PENDIENTE_DE_CONTRATO_OFICIAL (T05/T06).
No solicitar material privado por chat, GitHub, correo de soporte o documentación.

F05 pp.7–14 distingue acreditamiento, certificado y contraseña API por ambiente. Contraseña
GoDental, contraseña DGII, credencial API y protección del material de firma tienen propósitos
distintos. No copiar requisitos de una contraseña a otra ni asumir formatos X.509/PFX.

## Estrategia IFiscalSecretStore

Resolver referencias mediante una clave interna tipada `(emisor, ambiente, propósito, versión)`.
Propósitos separados: material de firma, protección del material de firma y credencial API;
token MH es temporal y nunca configurable en WPF. El servidor determina el ambiente desde
el perfil/autorización aprobados; no admite que el cliente pase una ruta de archivo/alias libre.
Cada resultado está ligado a esa clave; no hay fallback de TEST hacia PROD ni entre emisores.

Diseño para Windows/on-premise:

- Identidad de servicio dedicada con perfil/ACL propios; directorios privados fuera de repo,
  carpetas compartidas, OneDrive, directorio de instalación público y perfil del operador.
- Si el formato oficial requiere archivos/bytes opacos, evaluar DPAPI **ligado a la identidad
  de servicio** más ACL. DPAPI de equipo sin restricciones no satisface aislamiento por sí solo.
- Windows Certificate Store sólo si T05/T06 confirman compatibilidad del formato y firmador;
  no convertir un archivo oficial a PFX por suposición. Preferir clave no exportable si la
  solución oficial lo permite, verificando interoperabilidad y recuperación.
- Separación física/lógica de repositorios TEST/PROD, ACL y referencias. Verificación de emisor,
  ambiente, propósito, versión y vigencia antes de cada uso. La misma etiqueta no prueba
  que una credencial pertenezca al ambiente correcto; comprobarlo por mecanismo oficial.
- SQL sólo guarda datos públicos/metadatos/referencias opacas, nunca secretos en texto plano.
  `ValorProtegido` existente no se usará como almacén fiscal por el nombre de la columna.
- Lectura mínima y acotada del secreto por adaptador, sin exponerlo al modelo general o
  controladores. Borrar buffers cuando proceda y evitar copias/stringificaciones innecesarias.
  Proteger también temporales, crash dumps, procesos del firmador y archivos de intercambio.

La elección definitiva de backend y su recuperación se valida en Windows bajo identidad real,
no sólo con una implementación en memoria. No introducir hoy un store ficticio que reporte
“certificado válido” ni certificados reales en tests.

## Instalación local del certificado (diseño)

1. Con T05/T06 aprobados, documentar formatos exactos admitidos, integridad del paquete y
   compatibilidad con versión del firmador. Hasta entonces **no hay comando de importación**.
2. Administrador autorizado se autentica localmente en el host del servidor y se reautentica
   para aprovisionar. Selecciona emisor/ambiente/purpose explícitos; PROD permanece deshabilitado.
3. El futuro asistente local seguro recibe material sólo en ese equipo; no en argumentos de
   CLI/historial, connection strings, appsettings o WPF. Validar propiedad, formato/vigencia y
   vínculo fiscal conforme al contrato, sin enviar material a otro destino.
4. Instalar en store privado, verificar ACL como servicio y denegación como usuario de escritorio.
   Registrar metadatos públicos, versión y huella del artefacto si es apropiado; no registrar clave.
5. Ejecutar diagnóstico sin revelar material: disponibilidad, ambiente, pertenencia y estado.
   Probar firma según protocolo oficial con datos de ensayo, fuera de emisión productiva.
6. Guardar evidencia de aprovisionamiento y limpiar temporales. Eliminar el archivo temporal
   no garantiza borrado seguro del disco; evitar crearlo cuando el proveedor lo permita.

No pedir al usuario que pegue ahora un certificado. Cuando el asistente exista, documentar su
ruta exacta y verificar su operación antes de pedir configuración local. Permiso de Windows
para instalar un servicio no equivale al permiso de operación fiscal en GoDental.

## Rotación, revocación y recuperación (diseño)

1. Crear versión candidata con mismo emisor/ambiente/purpose; conservar la activa mientras
   se verifica la nueva según contrato. No sobrescribir material activo en una llamada parcial.
2. Validar acceso, pertenencia, vigencia e interoperabilidad sin datos productivos en CI.
3. Activación atómica del puntero de versión, auditoría de aprobador/fecha/motivo y revalidación
   del preflight. Invalidar tokens/cache de autenticación cuando corresponda a T01.
4. Las operaciones en curso conservan la versión usada y sus artefactos firmados. Un trabajo
   incierto no se vuelve a firmar con la nueva versión ni cambia identidad como parte de rotación.
5. Retirar/revocar la anterior conforme a procedimiento oficial y retención de evidencia. Ante
   compromiso, bloquear nuevos usos de inmediato, notificar por recorrido oficial y reconciliar
   trabajos en curso; no hacer rollback automático a material comprometido/vencido.
6. Restauración ensayada en entorno separado: identidad de servicio/DPAPI, secretos, referencias
   SQL y artefactos. Un `.bak` SQL no respalda el store. Si el proveedor prohíbe exportación o
   una clave no es recuperable, definir reaprovisionamiento oficial antes de activar producción.

Alertas de expiración/caducidad sin revelar valores sensibles. No inventar vigencia del token o
del certificado; obtenerla de T01/T05/T06 y metadatos verificables.

## Base ejecutable de custodia · DEV-53

[DEV-53](../DEV-53-Custodia-Secretos.md) implementa `IFiscalSecretCandidateStore` para candidatos
opacos **no verificados**: backend Windows DPAPI CurrentUser + ACL, partición fija por
emisor/ambiente/SID y referencia por propósito/versión. No sustituye `IFiscalSecretStore` de firma
ni confirma pertenencia/vigencia. No hay versión activa, importador de archivos ni API/WPF para
capturarlos. Comandos locales sólo provisionan raíz vacía y ensayan bytes sintéticos;
[configuración](../Servidor-Configuracion.md#5-raíz-privada-del-servidor--dev-53-candidatos-sin-importación-fiscal).
Rotación genérica conserva versiones y revoca por tombstone; aprobación/auditoría fiscal,
reautenticación y recuperación DPAPI en otro host siguen pendientes. Formatos T05/T06 pendientes.
