# Configuración local de GoDental.Server · bloque B1

Servidor ASP.NET Core .NET 8 para identidad de operador. No reemplaza todavía la conexión
comercial de WPF y no emite DTE. No instalar certificados/credenciales fiscales en este bloque.
Instalador/servicio Windows automatizado, estación autenticada criptográficamente y migración
de operaciones fiscales corresponden a B2/B3. Este procedimiento requiere al responsable de SQL
y Windows en el equipo de despliegue; no se ejecutó en las PCs personales desde cloud.

## 1. Base de datos

Aplicar todos los pendientes **021 → 022 → 023 → 024 → 025 → 026 → 027**, con respaldo previo
y el destino real de Ajustes de conexión. La última no contiene las anteriores. No recrear una
base con registros ni usar el instalador completo contra otra base por asumir el nombre.

Ejemplo PowerShell para una base existente, con `sqlcmd` instalado. Sustituir instancia/base
por las reales; las variables no contienen contraseñas:

```powershell
$sqlInstance = 'TU_INSTANCIA'
$databaseName = 'TU_BASE'
$pendingScripts = Get-ChildItem .\database\*.sql |
    Where-Object { $_.Name -match '^0(21|22|23|24|25|26|27)_' } |
    Sort-Object Name
if ($pendingScripts.Count -ne 7) { throw 'Faltan scripts 021–027.' }
foreach ($migration in $pendingScripts) {
    & sqlcmd -S $sqlInstance -d $databaseName -E -b -i $migration.FullName
    if ($LASTEXITCODE -ne 0) { throw "Migración detenida: $($migration.Name)" }
}
```

Con SQL local de desarrollo y certificado autofirmado, `sqlcmd -C` permite esa comprobación
local; no usarlo para omitir TLS de una conexión LAN/productiva. Crear usuarios GoDental y
estaciones desde el recorrido actual de WPF; el servidor no tiene bootstrap ni alta remota de
estaciones. Una cuenta con cambio obligatorio debe completarlo primero en WPF.

## 2. Identidad SQL de servicio

Usar cuenta Windows dedicada, distinta del operador y del instalador. El runtime **rechaza
db_owner/sysadmin**, roles amplios y permisos directos detectados; no hay interruptor para
permitirlos en Development. No ejecutar el servidor bajo el usuario administrador de SQL para
“solucionar” un 503 de identidad. Windows integrada en SQL, sin contraseña SQL en appsettings.

El responsable ejecuta el siguiente patrón en SQL bajo su identidad de instalación. Sustituir
cuenta/base; no agregar contraseñas ni compartirlas en chat. El script 027 no crea este login:

```sql
USE master;
CREATE LOGIN [EQUIPO\GoDentalSvc] FROM WINDOWS;
GO
USE [TU_BASE];
CREATE USER [EQUIPO\GoDentalSvc] FOR LOGIN [EQUIPO\GoDentalSvc];
ALTER ROLE GoDentalServerRuntime ADD MEMBER [EQUIPO\GoDentalSvc];
GO
```

Si ya existen login/usuario, el responsable verifica su configuración en vez de recrearlos.
El rol sólo ejecuta cuatro procedimientos de autenticación/sesiones. La cuenta no recibe acceso
directo a usuarios, ventas, fiscal, DDL ni procedimientos comerciales. Revisar también grupos
Windows heredados, roles/GRANT adicionales y permisos del servidor. Los clientes comerciales
actuales conservan su deuda de acceso SQL; no se declara cierre global SEC-01.

## 3. TLS y ejecución

HTTPS obligatorio, sin confiar automáticamente en `X-Forwarded-Proto`, sin redirección de
credenciales HTTP. Predeterminado `https://localhost:7443`, sólo el equipo local. Para LAN,
configurar certificado HTTPS confiable y nombre accesible, firewall y cuenta del servicio antes
de cambiar escucha. TLS del host **es distinto del certificado de firma fiscal MH**.

Desarrollo local: el SDK ofrece `dotnet dev-certs https --trust` bajo la identidad/perfil que
ejecuta el host. No sustituye el certificado y la confianza necesarios para un servicio/LAN.
El host usa la configuración HTTPS normal de Kestrel; material privado fuera de Git y logs.

Ejecutar desde PowerShell **bajo la cuenta Windows del servicio**, con permiso de lectura de
los binarios y de su certificado HTTPS. Estas variables sólo configuran valores no secretos:

```powershell
$env:Server__SqlServer = 'TU_INSTANCIA'
$env:Server__Database = 'TU_BASE'
$env:Server__Url = 'https://localhost:7443'
dotnet run --project src/GoDental.Server -c Release --no-launch-profile
```

Sólo para desarrollo **local**: `ASPNETCORE_ENVIRONMENT=Development` y
`Server__LocalDevelopmentSqlCertificate=true` permiten certificado SQL local en una base
`GoDental_Development` o `GoDental_Tests_...` validada. No relajan identidad SQL ni HTTPS.
No usar esta opción en LAN/PROD. No enviar una cadena de conexión del cliente al servidor.

## 4. Operación

`GET https://localhost:7443/health/live` sólo indica que el proceso responde, no conexión SQL,
contratos disponibles o preparación fiscal. Rutas internas en DEV-51:

- POST `/api/session`: `userName`, `password`, `workstation` existente. Sólo HTTPS. No poner
  la contraseña en argumentos, query, historial de consola, scripts versionados ni capturas.
- GET `/api/session`: Bearer del login, perfil/permisos actuales. Token en memoria del cliente,
  nunca `workstation.json`, cookies o URL. Ocho horas absolutas / veinte minutos sin actividad.
- DELETE `/api/session`: revocación durable del token presentado.
- GET `/api/fiscal/readiness`: exige FISCAL.VER y VENTAS.VER; informa generación, firma,
  transmisión y producción deshabilitadas. No representa aceptación de Hacienda.

No hay aún UI WPF para este login de servidor; el login comercial existente se conserva.
Una respuesta 401 no revela si el usuario existe. 403 exige permiso o cambio obligatorio según
ruta. 429 indica límite; no reintentar automáticamente contraseñas. 503 informa fallo de proveedor
sin exponer SQL/credenciales; revisar configuración/identidad con el responsable, sin habilitar
logging de cuerpos/cabeceras/excepciones completas.

Pruebas reproducibles: `dotnet test tests/GoDental.Server.Tests -c Release` para contrato HTTP
con identidad sintética; suite SQL real aislada con `--run-server-identity --localdb .` en Windows.
No usa ni pide secretos MH, y no valida la instalación LAN personal.

## 5. Raíz privada del servidor · DEV-53 (candidatos, sin importación fiscal)

Sólo Windows y NTFS local. Ejecutar estos comandos **bajo la cuenta dedicada del servicio**,
con perfil cargado y permiso para crear su directorio en ProgramData (o provisión supervisada).
No usar la cuenta de un operador de escritorio para conservar el almacén definitivo. TEST y
PROD requieren cuentas/raíces separadas. Ningún certificado/contraseña fiscal se solicita aquí.

PowerShell, configuración **no secreta**. `IssuerId` es identificador interno estable del emisor;
no es el NIT ni un Código de Generación. Registrar ese GUID para futuras ejecuciones:

```powershell
$env:SecretStore__ServiceSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$env:SecretStore__IssuerId = 'GUID_INTERNO_DEL_EMISOR'
$env:SecretStore__Environment = 'TEST'
$env:SecretStore__Root = Join-Path $env:ProgramData 'GoDental.Secrets.TEST'
# Una sola vez: crea exclusivamente una raíz NUEVA, vacía y ligada a esa identidad/partición.
dotnet run --project src/GoDental.Server -c Release --no-launch-profile -- --initialize-secret-store
if ($LASTEXITCODE -ne 0) { throw 'Provisión detenida; revisar cuenta, perfil, ruta y permisos.' }
# Se puede repetir: usa sólo bytes aleatorios sintéticos en un subdirectorio privado temporal.
dotnet run --project src/GoDental.Server -c Release --no-launch-profile -- --check-secret-store
if ($LASTEXITCODE -ne 0) { throw 'Diagnóstico del almacén no satisfactorio.' }
```

No ejecutar la inicialización sobre un directorio existente ni cambiar ACL para silenciar un
fallo. El comando no acepta rutas compartidas, repo/OneDrive ni ubicaciones del operador:
la raíz debe ser hija directa de ProgramData con prefijo `GoDental.Secrets.`. Si ProgramData está
redirigido a un medio no local/NTFS, el backend falla de forma segura. Un fallo de permisos para
crear esa raíz se resuelve con el responsable Windows bajo la identidad real de servicio; no
conceder permisos amplios sobre ProgramData ni iniciar el host permanentemente como administrador.

No se inicia SQL/Kestrel, no hay endpoints de lectura/importación ni se activa fiscal. Diagnóstico
correcto comprueba custodia sintética, no certificado, Hacienda o preparación de producción.
El backend sólo se usa por estos comandos y pruebas hasta existir un caso fiscal aprobado.

Respaldar **la raíz completa** (incluidos marcador de partición y tombstones) de forma cifrada y
con ACL equivalentes. Un `.bak` SQL o copiar archivos a otro equipo/cuenta no conserva por sí
solo DPAPI CurrentUser. Restaurar requiere identidad/perfil/DPAPI protegidos o reaprovisionamiento
oficial documentado; no cambiar a DPAPI LocalMachine para evitar ese requisito. Restaurar un
backup anterior a una revocación puede reintroducir material retirado: mantenerlo fuera de uso
hasta reconciliar revocaciones y aprobar el preflight. La recuperación entre hosts no está validada.
