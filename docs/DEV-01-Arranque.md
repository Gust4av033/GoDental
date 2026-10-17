# DEV-01 · Arranque de escritorio

Versión 0.1 · 17/10/2026 · Implementado para revisión, sin habilitación de producción.

## Alcance

Evoluciona el arranque preliminar de la baseline `0344200` con Shell/MVVM, navegación interna, menú de 228/72 px, temas Light/Dark y configuración inicial. Conserva Generic Host, DI, .NET 8/C# 12, Dapper, QuestPDF y la configuración de infraestructura existente.

La pantalla de inicio describe los módulos futuros; no presenta indicadores ni operaciones simuladas. El estado «Conexión comprobada» describe únicamente la última prueba manual del perfil mostrado. No es monitoreo continuo, autenticación de negocio ni certificación del esquema.

## Fuentes y decisiones

- DM-01 v0.2 y DB-01 v0.1 originales, ARQ-01, UX-01 y SEC-01 v0.1: capas, MVVM, conexión, temas y seguridad.
- La baseline SQL 001–008 ya está integrada y aprobada estáticamente en `main`, según `database/README.md` y `docs/VALIDACION.md`. Este avance no cambia su aprobación ni ejecuta esos scripts.
- VEN-01A prevalece sobre los pendientes comerciales históricos del Maestro; no se implementan reglas de precios en este avance.
- Los contratos del perfil y del diagnóstico viven en Application; SQL y archivos en Infrastructure; la interfaz consume esos contratos mediante DI.
- La cuenta Windows se utiliza sólo para el diagnóstico inicial. La cuenta de aplicación SQL de SEC-01, mínimo privilegio y secretos protegidos sigue pendiente junto con autenticación. No se almacenan contraseñas en el perfil.
- La prueba ejecuta exclusivamente `SELECT 1`, con conexión cifrada y certificado validado. Apertura y consulta tienen timeout de 5 segundos cada una, sin reintento automático. Admite cancelación y bloquea acciones solapadas en esa pantalla. Cambiar servidor/base invalida la prueba anterior.
- `Database:ConnectionString` conserva su contrato para infraestructura. El diagnóstico construye una conexión independiente a partir de servidor/base; guardar ese perfil no reemplaza la configuración operativa. Evita confundir un diagnóstico con una instalación lista para vender.
- Se usa Segoe MDL2 Assets de Windows para los iconos básicos, sin añadir otra dependencia visual. La paleta es provisional y queda centralizada en `Colors.xaml`.
- Tema y perfil se guardan por equipo/usuario Windows hasta que exista sesión de aplicación; no se escribe todavía `sis.PreferenciasUsuario`. El tema se aplica inmediatamente y se conserva al guardar un perfil válido.
- El registro técnico local rota a aproximadamente 1 MiB, conservando un archivo anterior. No sustituye bitácora, políticas definitivas de retención ni auditoría de negocio. Registra tipos/códigos de fallos sin cadenas de conexión ni mensajes SQL completos.
- Se fija explícitamente `System.Security.Cryptography.Pkcs` 10.0.13 para evitar las versiones transitivas afectadas por [CVE-2026-84008](https://github.com/advisories/GHSA-846f-f764-76mx) y [CVE-2026-70162](https://github.com/advisories/GHSA-2fmv-rv2c-59x7). Las demás versiones centrales de la baseline se conservan.

## Uso

1. Abrir la aplicación sin necesidad de conexión SQL.
2. Entrar en **Conexión y apariencia**, escribir servidor y una base existente.
3. Elegir tema y guardar. El archivo queda fuera del repositorio: `%LOCALAPPDATA%\GoDental\workstation.json`.
4. Comprobar conexión. Requiere que Windows tenga acceso a SQL y que su certificado sea válido. No omitir la validación del certificado para forzar una prueba exitosa.

Logs: `%LOCALAPPDATA%\GoDental\logs\application.log`.

## Validación reproducible

`scripts/verify.ps1` ejecuta restore, build Release, pruebas existentes con cobertura y el ejecutable de smoke WPF. Este último prueba configuración inválida/dañada, guardado, cancelación, estados, navegación y temas, y genera tres PNG bajo `TestResults/UiPreviews/`.

La comprobación funcional exitosa usa una implementación controlada en pruebas; no representa acceso a una base real. La prueba de cancelación SQL apunta a loopback con token ya cancelado. No instala ni modifica bases de datos.

## Siguiente incremento

Certificar 001–008 en SQL Server de desarrollo; implementar primer administrador, hash de contraseña, sesión, permisos y auditoría según SEC-01. Después habilitar catálogos/productos, edición con rowversion y los módulos transaccionales con pruebas multi-PC.
