# DEV-51 — Servidor HTTPS e identidad de operador (B1 / base B2)

23/10/2026. Continuación de DEV-50/FIS-01. Diseño registrado antes del código/SQL.
Alcance: GoDental.Server ASP.NET Core .NET 8, autenticación GoDental y permisos actuales.
No implementa contratos Hacienda, firma, secretos fiscales, emisión ni migración completa WPF.

## Decisiones de seguridad internas

- Sesión opaca aleatoria de 256 bits, sólo en cabecera Bearer sobre HTTPS. SQL conserva hash
  SHA-256 del token, nunca el token. No JWT fabricado, cookies, query tokens ni IdUsuario libre.
- Política inicial del servidor: ocho horas absolutas, veinte minutos de inactividad; una sesión
  activa por usuario/estación. Un nuevo acceso revoca la anterior de ese par. Logout revoca en
  SQL y sobrevive reinicios. Desactivar usuario/estación, bloquear cuenta, exigir cambio de clave
  o cambiar el hash de contraseña invalida acceso en la siguiente petición.
- Cada petición autenticada consulta identidad/permisos actuales en SQL. Contexto por petición,
  sin UserSession singleton del escritorio. No hay operaciones fiscales de escritura aún; las
  futuras deberán revalidar permisos/versiones dentro de su transacción.
- Credenciales PBKDF2 existentes; fallos y bloqueo de cuenta se registran transaccionalmente.
  Contraseña obligatoria pendiente: no se emite token; cambiarla por el recorrido WPF actual.
  Bootstrap, cambio remoto de contraseña y administración de usuarios quedan fuera de B1.
- La estación debe existir y estar activa en sis.EquiposTrabajo. HTTP no da de alta estaciones.
  El identificador de login es declaración de estación registrada, no atestación de hardware ni
  segundo factor. Tras el login se obtiene de SQL, nunca de cabeceras/IDs de otra operación.
- Límite HTTP por host y por IP en login, cola cero y concurrencia acotada. Bloqueo por cuenta
  SQL compartido entre hosts; límites HTTP son locales al host, no un límite distribuido global.
- HTTPS obligatorio sin redirección de contraseñas, body acotado, JSON estricto, respuestas
  no-store y errores genéricos. Sin logs de cuerpos/query/cabeceras ni excepciones completas.

## Modelo DB-01 · migración 027

Ampliación aditiva **027_Servidor_Identidad.sql**, posterior a 026. Ningún script histórico cambia.
Una tabla `seg.SesionesServidor`: IdSesion bigint identity PK, TokenHash binary(32) UNIQUE,
IdUsuario/IdEquipo FK, CredencialHash binary(32), CreadaEn/UltimoUso/ExpiraEn datetimeoffset(7),
RevocadaEn nullable. Tiempos UTC desde SQL; versión de credencial liga sesión al hash vigente.
CHECKs de fechas, índices de consulta por token y usuario/estación. No historial JSON/contraseñas.

Cuatro procedimientos propios: `seg.ServidorLoginLeer`, `seg.ServidorLoginFinalizar`,
`seg.ServidorSesionValidar`, `seg.ServidorSesionRevocar`. Lectura de credencial para verificación
PBKDF2 fuera de una transacción larga; finalización revalida rowversion, estado y estación en
transacción. Intentos fallidos se incrementan aun bajo concurrencia; un resultado verificado
contra versión anterior no autoriza sesión. Escritura de sesión y auditoría atómicas.

Rol `GoDentalServerRuntime`: EXECUTE únicamente sobre esos cuatro procedimientos. No permisos
directos de lectura/escritura sobre tablas, DDL ni procedimientos comerciales. Ownership chain
estática, sin SQL dinámico, sin conceder db_owner/sysadmin. El instalador no crea login, no
agrega operadores al rol ni incluye contraseñas; el responsable asigna una cuenta Windows de
servicio separada. El runtime rechaza cuentas privilegiadas o con permisos amplios detectados.

Resultado estructural previsto: 82 tablas, 8 vistas, 22 procedimientos. Herramienta de desarrollo
y pruebas actualizan sus verificaciones, conservando detección de instalación incompleta.
La reaplicación preserva sesiones, revocaciones, permisos operativos y auditoría existentes.

## Contrato HTTP interno inicial

| Método / ruta | Acceso y comportamiento |
| --- | --- |
| GET /health/live | Sin sesión, sin detalles de SQL/configuración; no certifica preparación DTE |
| POST /api/session | Usuario/contraseña/identificador de estación existente; token sólo si acceso válido |
| GET /api/session | Perfil actual autenticado, sin hashes/connection scope/secretos |
| DELETE /api/session | Revoca sólo la sesión presentada; ningún IdUsuario del cliente |
| GET /api/fiscal/readiness | FISCAL.VER y VENTAS.VER actuales; informa que emisión/firma/transmisión siguen deshabilitadas |

No hay endpoint para asignar aceptación/sello/PROD. JSON desconocido no concede identidad o
permisos; query/cookies no autentican. TLS de host se configura localmente, distinto de certificado
fiscal. Conexión SQL Windows integrada bajo identidad del servicio, cifrado obligatorio; aceptar
certificado SQL local sólo en Development, instancia local y BD de desarrollo/pruebas validada.

## Validación y límites de cierre

Código B1 implementado: host, servicio Application, repositorio SQL y procedimientos 027.
Local: restore y Release sin warnings/errores; **418 xUnit existentes + 22 HTTP = 440** correctas.
Auditoría NuGet: nueve proyectos consultados, sin problemas ni vulnerabilidades conocidas.
La evidencia SQL/WPF actual se añadirá al terminar CI Windows; no es validación Hacienda.

Baseline previa `9600e15`: restore/Release cero warnings/errores y 418 xUnit en Linux con Windows
targeting. No asumir resultados SQL/WPF históricos como evidencia de esta iteración.
Validación final: pruebas HTTP del host real en TestServer con repositorio sintético identificado;
suite SQL con usuarios limitados reales, sesiones/concurrencia/reaplicación/auditoría, más regresión
de suites existentes y smoke WPF en CI Windows. CI no llama Hacienda ni usa credenciales reales.

No cerrar SEC-01 global: WPF comercial sigue SQL directo; provisión/ACL/TLS de las PCs reales
y migración de operaciones confiables requieren B2. No cerrar SEC-03: secret store/formato de
certificado fiscal y TEST/PROD siguen pendientes. Siguiente bloque: B2 completo y B3 genérico;
A2/C–H requieren documentación oficial faltante. Migraciones personales pendientes: **021 →
022 → 023 → 024 → 025 → 026 → 027**, todas en orden, no aplicadas desde cloud.
