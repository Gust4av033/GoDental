# Seguridad fiscal y cierre S1

**PREPARADO.** [DEV-45](../DEV-45-Seguridad-Hallazgos.md) sigue abierto; este diseño no
acredita mínimo privilegio instalado, pentest ni producción. Fiscal actual no tiene transporte
o secreto MH, por lo que no se debe habilitar captura de credenciales en sus formularios.

Avance B1: [DEV-51](../DEV-51-Servidor-Identidad.md) implementa host, sesión opaca duradera,
permisos actuales y rol SQL de autenticación. Validación y alcance de cierre allí; no convierte
el acceso SQL comercial en confiable ni habilita firma/transmisión/PROD.

## Amenazas y controles de salida

| Riesgo | Control exigido antes de habilitar recorrido | Prueba de cierre |
| --- | --- | --- |
| Operador falsificado por IdUsuario del cliente | Identidad del contexto autenticado, permisos actuales y estación registrada | Anónimo, otro usuario/estación, sesión revocada y permiso retirado no escriben ni revelan DTE |
| Salto de autorización por SQL directo (SEC-01) | Identidad de servicio limitada; clientes sin escritura en perfiles, autorizaciones, snapshots, cola/evidencias fiscales | Login real de cliente no puede modificar ni ejecutar operaciones fiscales de confianza |
| Robo de certificado/token/contraseña (SEC-03) | Almacén del servidor, ACL por identidad/purpose, separación de ambientes, nunca retorno por API | Acceso desde usuario de escritorio/otra identidad/ambiente/purpose falla sin revelar material |
| SSRF, destinos manipulados o MITM | HttpClientFactory, allowlist desde contrato aprobado, TLS validado, redirecciones deshabilitadas salvo decisión oficial explícita | Host/IP/ruta no autorizados y certificado inválido rechazados antes de enviar secretos |
| Repetición y doble emisión | Ledger con hash/operador/alcance, identidad fiscal estable, trabajo durable, fencing y reconciliación | Dos instancias/caída tras envío no generan doble identidad ni POST automático |
| Acceso indebido a PII/artefactos | Autorización por dueño/emisor, mínimo dato en listados/logs, ACL/backup protegidos | Enumeración de IDs, descargas cruzadas, path traversal y enlaces no exponen bytes |
| Aceptación manual/falsa | Resultado interpretado desde respuesta/consulta oficial conservada, no endpoint “marcar aceptado” | Ningún rol ni campo UI permite asignar sello o activar PROD sin evidencias |
| Alteración histórica | Artefactos inmutables/hash, separación de roles, auditoría transaccional y backups | Cambio de Cliente no altera DTE; hash incorrecto bloquea uso y deja evidencia |
| Filtración mediante diagnósticos | Logs estructurados por allowlist: operación, ID interno, fase, clase de error, duración | Captura de logs en fallos de auth/firma/transporte sin cabeceras, cuerpos o secretos |
| Abuso de acceso/emisión | Límites de tamaño, frecuencia y concurrencia; reautenticación para configuración sensible | Límites compartidos, sin saturar SQL/firmador ni saltarlos abriendo otra ventana |

No registrar Authorization, cookies/sesiones, tokens parciales reutilizables, cuerpos completos,
claves, connection strings, PII innecesaria o dumps del firmador. Respuesta original para evidencia
es un artefacto protegido con política propia, **no un log técnico**. Sanitizar también URL/query,
excepciones, tracing automático de HTTP y bundles de soporte.

El servidor fija usuario/estación en parámetros de auditoría; no confía en `SESSION_CONTEXT`
que pueda escribir una cuenta con acceso amplio. Identidad SQL runtime sin db_owner/sysadmin;
principal de migración distinto. Las pruebas de permiso de Application/SQL actuales se conservan,
pero no demuestran aislamiento frente a un cliente con acceso directo privilegiado.

## Permisos y responsabilidades

Existentes, sin ampliación ficticia: `FISCAL.VER`, `FISCAL.CONFIGURAR`,
`FISCAL.REVISAR_INTERNO`. Configurar actualmente prepara una solicitud, no instala certificados
ni cambia ambiente MH. Mantener autorización real de lectura y revisión en Application/SQL.

Sólo al implementar el caso real se añadirán `FISCAL.EMITIR_DTE`, `FISCAL.INVALIDAR_DTE`,
`FISCAL.REINTENTAR_DTE` y permiso de contingencia si corresponde, mediante migración nueva.
El permiso de reintento nunca autoriza POST ciego ni alterar identidad fiscal. Administrar secretos,
mapeos/autorizaciones y activar PROD exige permisos específicos y reautenticación a definir en
fase B. No hardcodear privilegios por nombre de usuario ni dar todos los futuros permisos al rol
actual sin revisar el riesgo. Fallo de configuración mantiene producción bloqueada.

## Fases y evidencia

- **B/S1**: autenticación, sesión/revocación, autorización, SQL mínimo, custodia y aislamiento.
  Se puede construir sin credenciales fiscales. No cerrar SEC-01/03 sólo por crear el host.
- **C–F/S1**: contratos aprobados, validación local, interoperabilidad firmador, destinos/TLS,
  evidencias durables, incertidumbre/reconciliación y prueba de no exposición de secretos.
- **Antes de datos productivos**: backup/restauración coordinados SQL+artefactos, ACL/retención,
  revocación/rotación ensayadas; SEC-04/05 y demás controles aplicables no esperan al piloto.
- **Antes de distribución/piloto**: SEC-02/06, expiración, firma/actualización y dependencia,
  revisión de exposición LAN y pruebas de abuso. Mantener las comprobaciones NuGet de CI.

Cada cierre identifica commit, entorno, principal con que se probó y resultado. La ausencia de
avisos NuGet no certifica seguridad. Ninguna prueba ordinaria de CI utiliza credenciales reales
TEST/PROD ni llama a Hacienda. Los fixtures se etiquetan sintéticos y nunca se muestran como
evidencia de aceptación fiscal real.

Base B3 en [DEV-53](../DEV-53-Custodia-Secretos.md): almacén Windows de candidatos opacos,
sin endpoints, activación ni importación fiscal. Se documenta la diferencia entre persistir
revocación y restaurar un backup antiguo: no existe protección completa contra rollback de
backups ni cierre SEC-03; el futuro preflight necesita reconciliar y aprobar la restauración.

DEV-55 fortalece el despliegue B2: cuenta Windows limitada y SID real, certificado HTTPS
explícito y SQL 027 completo antes de escuchar. Diagnóstico sanitizado sin listener ni secretos.
No cierra estaciones, operaciones/evidencias fiscales, revocación TLS o provisión LAN reales.
