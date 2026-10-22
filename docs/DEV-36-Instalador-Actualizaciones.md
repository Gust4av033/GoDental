# DEV-36 · Instalación y actualización con conservación de datos

Fecha: 21/10/2026, America/El_Salvador.

## Estado y alcance

Solicitud: preparar una distribución Windows con asistente de instalación, SQL Server sólo motor, sin SSMS, y actualizaciones que conserven los registros capturados por el negocio. Este documento define la propuesta y el siguiente trabajo; **todavía no existe un Setup.exe ni un actualizador de producción implementado**. No se instala software, modifica SQL ni cambia la versión comercial con esta entrega.

Fuentes: DM-01 §§5/18 (una base central, migraciones numeradas, backups diarios), DB-01 (modelo y trazabilidad), ARQ-01 (capas y composición) y SEC-01 (mínimo privilegio, secretos y restauración). La edición/versión SQL definitiva, autenticación de producción y retención de backups continúan pendientes de cierre en esos documentos antes de desplegar. SQL Server Express es la recomendación inicial, sujeta a compatibilidad, capacidad, soporte y licencias del redistribuible elegido.

## Lo que ya existe y lo que falta

| Aspecto | Situación comprobada |
| --- | --- |
| Aplicación | WPF .NET 8; `GoDental.Wpf.csproj` declara **0.3.0**. No se ha publicado una versión 1.3.3G. |
| Base | Scripts 001–017. Las migraciones recientes son aditivas y tienen pruebas de reaplicación/conservación, pero eso no sustituye un historial de ejecución en producción. |
| Herramienta local | `DevelopmentDatabase.InstallAsync` acepta bases de desarrollo/pruebas marcadas. En una existente omite 001–008 y vuelve a ejecutar 009 en adelante; no decide por historial qué migraciones faltan. No dispone de checksum histórico, respaldo ni coordinación de actualización productiva. |
| Script manual | `scripts/install-database.ps1` ejecuta la lista de scripts con sqlcmd; no es el asistente ni el coordinador de upgrades. |
| Configuración | Conexión/tema y preferencias de impresión por equipo. Deben conservarse, junto con el diario local de operaciones pendientes. |
| Producción | Faltan bootstrapper, herramienta de mantenimiento, registro de migraciones, control de compatibilidad, respaldo/restauración y pruebas de instalación/actualización. No reutilizar el instalador de desarrollo cambiándole el nombre o quitando sus protecciones. |

## Instalador propuesto

Un único **GoDental-Setup.exe**, con dos recorridos:

1. **Equipo principal**: detectar motor compatible existente y ofrecer usarlo, o instalar una instancia dedicada de SQL Server Express **sólo Database Engine**. No instalar SSMS, Visual Studio, VS Code, Git ni SDK. Instalar GoDental publicado `win-x64` autocontenido; incluye el runtime necesario. Comprobar espacio, privilegios administrativos, reinicio pendiente, integridad de paquetes y conectividad; informar los pasos que requieren descarga o reinicio. Crear la base sólo si no existe. Una base existente requiere identificar/verificar su procedencia y actualizarla, nunca reemplazarla.
2. **Estación adicional**: instalar sólo GoDental, seleccionar el servidor principal, comprobar conexión y registrar la estación mediante el flujo autorizado. No crear una segunda copia independiente de la BD. La principal debe estar encendida durante la operación.

El asistente muestra licencia/requisitos, tipo de equipo, destino/conexión, progreso y resultado. El primer administrador define sus credenciales; no hay contraseña fija ni usuarios de ejemplo de producción. Elegir o incorporar paquetes del motor requiere fijar versión, arquitectura, hash/firma, origen oficial y condiciones de redistribución. Permitir paquete completo offline para instalaciones sin Internet; no descargar automáticamente una versión SQL distinta en cada equipo.

Recomendación técnica: **WiX Toolset + Burn**, con MSI para GoDental y cadena de prerrequisitos para el motor. Permite detectar/reparar/actualizar dependencias y manejar códigos de salida/reinicio. Fijar versión y condiciones de uso/licencia de la herramienta antes de incorporar la dependencia. Firmar instalador/binarios cuando se disponga del certificado; no presentar un paquete sin firma como firmado.

Separar rutas: binarios en Program Files; datos compartidos de mantenimiento/backups y logs con ACL en ProgramData; perfil del equipo en LocalAppData. Los MDF/LDF permanecen en las rutas administradas por SQL Server. Desinstalar GoDental conserva base, backups, configuración y diario de pendientes; tampoco desinstala un motor SQL compartido. La eliminación de datos sería una operación administrativa distinta y explícita.

## Versiones: aplicación y BD son independientes

`1.3.3G` es un **ejemplo del usuario**, no una versión adoptada. Puede mostrarse como etiqueta comercial, con versión numérica de instalador `1.3.3` y una identificación de compilación/canal acordada. No asignar significado a la letra G sin definirlo. El paquete debe identificar versión de aplicación, commit, canal y rango de esquema realmente compatible.

Ejemplo hipotético, suponiendo que el siguiente cambio SQL disponible sea 018:

| Paquete | Esquema | Efecto |
| --- | --- | --- |
| 1.3.3G | 017 | La jefa registra productos, clientes y ventas. |
| 1.3.4G | 017 | Cambio sólo de UI: reemplazar binarios, sin migrar la base. |
| 1.4.0G | 018 | Aplicar la nueva migración sobre la misma BD y actualizar todas las estaciones; conservar registros y relaciones. |

No usar la versión del programa como número de migración, ni deducir el estado de una base contando archivos/carpetas/tablas. Asignar los próximos números SQL al implementar, coordinados con los módulos activos.

## Ejecutor de migraciones de producción

Crear una herramienta C# de mantenimiento fuera de `tests/`, distribuida con el instalador y basada en contratos de Application e implementaciones Infrastructure. WPF no ejecuta scripts SQL. El instalador invoca la herramienta; la cuenta habitual de venta no recibe permisos DDL ni de administrador de SQL.

Antes de introducir DDL, versionar en DB-01 el **historial de migraciones** y el control de mantenimiento/compatibilidad. El diseño debe contemplar identificador/nombre de migración, checksum SHA-256, fecha UTC, versión de aplicación y estado/resultado; los fallos tienen registro de diagnóstico sin secretos. No hay una nueva tabla ni migración creada por este documento.

Reglas del ejecutor:

- Manifest de scripts explícito, ordenado e incluido en el paquete. Verificar firma/integridad del paquete y checksum de cada script. El historial de una migración ya aplicada no se modifica ni se marca aplicada sólo porque su archivo exista.
- Ejecutar solamente migraciones pendientes. Una corrección nueva se publica como otra migración; no editar el pasado después de distribuirlo. Para bases de desarrollo históricas donde sí cambió un script, crear un procedimiento de adopción verificado, sin inventar hashes retrospectivos ni confiar en el marcador de desarrollo.
- Migración y marca de éxito en la misma transacción cuando SQL lo permita; tratar explícitamente comandos no transaccionales. Si falla, detener y no anunciar actualización correcta. Una secuencia larga puede quedar en su última migración confirmada: el reintento debe reconocer ese estado, sin repetir escrituras de negocio.
- Exclusión mediante bloqueo en SQL para impedir dos migradores simultáneos; no sirve sólo un archivo/lock de una laptop. Diseñar también una barrera de mantenimiento que todas las escrituras de negocio respeten. Un applock entre migradores por sí solo no impide ventas concurrentes.
- Comprobar esquema y compatibilidad al conectar y antes de operar; una estación antigua o una BD más nueva de lo admitido debe recibir una indicación de actualización y quedar bloqueada para escritura. No habilitar versiones mixtas hasta probar explícitamente su compatibilidad.
- Inicialización separada de actualización: datos mínimos de catálogos/permisos idempotentes, sin resembrar productos, saldos, ventas, cuentas, contraseñas o correlativos reales. No recrear la BD al actualizar.

La adopción de una BD `GoDental_Development` con registros reales requiere backup, diagnóstico del esquema/datos, ensayo de restauración y plan de transición a una base operativa identificada. No renombrar ni quitar marcadores para forzar herramientas de desarrollo a trabajar sobre producción.

## Recorrido de actualización

1. Presentar versión disponible y notas. Para el primer piloto usar **actualizador asistido/manual** desde un paquete de release validado; no depender de `git pull` ni actualizar durante una venta. La descarga automática puede incorporarse después con manifest y paquetes firmados.
2. Identificar instancia y base reales de esa instalación; comprobar versión, permisos, disco y conectividad. Reconciliar operaciones inciertas mediante el mecanismo existente y conservar sus diarios/OperationId; actualizar no equivale a cancelar ni reenviar cobros.
3. Coordinar mantenimiento de **todas** las estaciones, impedir nuevas escrituras en el servidor y esperar/validar la terminación de transacciones activas. Cerrar sólo la ventana de la principal no garantiza exclusión.
4. Crear backup consistente `.bak` de SQL Server antes de cambiar esquema, con checksum y verificación; si falla, detener la actualización. Usar COPY_ONLY en respaldos previos al upgrade cuando proceda, sin alterar la estrategia diaria. `RESTORE VERIFYONLY` es una comprobación adicional, no sustituye ensayar una restauración real en una base aislada.
5. Aplicar migraciones pendientes y validaciones, actualizar aplicación y estaciones, y comprobar compatibilidad antes de levantar mantenimiento. Conservar configuración y diarios locales. Si una migración falla, mantener el bloqueo de operación y presentar diagnóstico/reintento controlado.
6. Validar acceso, totales/relaciones, correlativos y recorridos clave. Registrar resultado y permitir trabajar sólo cuando la instalación sea coherente.

Un fallo del MSI no revierte automáticamente cambios ya confirmados en SQL. Volver sólo al ejecutable anterior puede ser incompatible con el esquema nuevo. No aplicar migraciones inversas destructivas ni restaurar una BD automáticamente. Un plan de recuperación debe coordinar binarios y base, impedir escrituras durante la ventana y advertir que restaurar un backup descarta cambios posteriores; toda restauración real requiere una acción administrativa explícita.

## Backups y red

Antes de recibir registros productivos, implementar backup diario que funcione aunque GoDental esté cerrado. Para SQL Express, programar mantenimiento con el Programador de tareas de Windows; no asumir SQL Server Agent. Producir primero el `.bak` local terminado y después copiarlo a OneDrive; no sincronizar MDF/LDF activos. Definir retención, destino secundario y prueba periódica de restauración antes del piloto, respetando SEC-01.

La autenticación de red sigue pendiente: el modo actual usa Windows y no garantiza acceso entre laptops con cuentas locales distintas. Elegir y probar la estrategia para el entorno real, separar credenciales de instalación/mantenimiento de las de operación, proteger secretos y no usar `sa`/`db_owner` para ventas. Configurar sólo la conectividad LAN necesaria, puerto y firewall acotados; no exponer SQL a Internet ni modificar una instancia ajena silenciosamente.

## Secuencia y criterios de aceptación

Este frente de **despliegue para piloto** tiene que resolverse antes de usar registros de negocio como única copia, aunque los módulos continúen con **2.1B** según DEV-30. No declara terminados importaciones, DTE, Online ni el piloto.

1. Cerrar versión/edición SQL y autenticación/rutas de producción; diseñar y versionar en DB-01 historial y mantenimiento/compatibilidad.
2. Implementar migrador, adopción de esquema existente, backup/restauración y pruebas de fallo/reintento/concurrencia. Reutilizar librerías estables, no las puertas de entrada exclusivas de tests.
3. Publicar app autocontenida y asistente principal/estación adicional; verificar detección de SQL, instalación del motor sin SSMS y reinicio/recuperación del asistente.
4. Probar en Windows limpio, instalación sin SDK/SSMS, instancia existente, dos PCs LAN, actualización con registros ficticios representativos (ventas, pagos, crédito, inventario, lotes, compras y tickets pendientes), preservación de usuarios/configuración, incompatibilidad, fallo parcial, falta de espacio/permisos, reintento y desinstalación sin borrar datos.
5. Ensayar restauración aislada, etiquetar release validada y hacer piloto controlado con copia independiente de respaldo. Publicar un instalador verificable; sólo entonces llamarlo listo para uso productivo.

Esta entrega es documental: no requiere migración, compilación ni reinstalación. La cuota restante de Codex no se puede estimar desde el repositorio; el documento permite continuar por bloques sin perder estas decisiones.
