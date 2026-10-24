# DEV-55 — Preparación verificable del despliegue B2

24/10/2026. Baseline `2728fa0`; conserva navegación/carga DEV-54. Diseño previo al código.
El usuario confirma 027 aplicada y código actualizado en su PC; no se inspeccionó su SQL desde cloud.

## Problema y alcance

B1 sólo comprobaba SQL al llegar una operación. El proceso podía anunciar RUNNING con una
identidad/instalación incompletas. HTTPS dependía de configuración genérica de Kestrel sin
selección explícita del certificado de despliegue. Se añade una barrera de arranque y un
comando local de diagnóstico, antes de aceptar conexiones. No hay nueva migración.

- `--check-server`: configuración no secreta, identidad de servicio, certificado HTTPS y acceso
  SQL mínimo, sin iniciar HTTP ni modificar usuarios, tablas o certificados. Códigos acotados,
  sin imprimir SID/servidor/base/subject/rutas/excepciones del proveedor. No prueba Hacienda.
- En ejecución normal el mismo diagnóstico bloquea el arranque. No hay interruptor de omisión
  en appsettings/HTTP. TestServer sustituye explícitamente la dependencia por una fixture;
  no usar ese resultado como prueba del despliegue físico.
- Fuera de Development: Windows, SID configurado igual al proceso, cuenta sin Administradores
  ni identidades incorporadas privilegiadas. SQL bajo Windows integrada y rol limitado.
  Development sólo permite escucha loopback; conserva SQL/rol mínimo y restricciones de BD local.
  Sin huella explícita el diagnóstico informa DevelopmentTlsUnverified (salida 1); el certificado
  del SDK lo comprueba Kestrel al iniciar, sin atribuirle preparación LAN/producción.
- Certificado **TLS, distinto del fiscal**: selección exacta por thumbprint desde LocalMachine/My;
  vigencia, SAN con nombre exacto (sin fallback a CN ni wildcard), EKU Server Authentication,
  clave privada accesible y cadena local confiable. Sin PFX/contraseña en appsettings/CLI.
  Cadena sin descargas AIA ni consulta OCSP/CRL: no se declara comprobación de revocación;
  confianza/revocación de CA y clientes se verifican en el despliegue real. No omitir TLS.
- Única URL HTTPS explícita, sin Kestrel:Endpoints alternativos. Configuración del certificado
  controlada por el host; TLS 1.2/1.3. No permitir que otra sección habilite un listener HTTP.
- SQL también exige que los cuatro procedimientos 027 existan y sean ejecutables, además de
  rechazar privilegios amplios. Un rol vacío o un EXECUTE retirado bloquea la identidad.
- Registro/configuración local se resuelve desde el directorio de la aplicación; no desde el
  directorio de trabajo de un futuro servicio Windows. No capturar configuración sensible en Git.

## Validación prevista

Pruebas puras de configuración/certificados y startup bloqueado, HTTP existente; Windows real
para store TLS, clave/cadena/nombre y conexión HTTPS con validación de confianza; SQL limitado
real con permisos 027 incompletos. Regresión completa SQL/WPF y fuentes intactas. Ningún
certificado/credencial MH o servicio permanente se instala en CI; certificados TLS sintéticos
se retiran al terminar.

## Continuación y límites

No cierra B2/SEC-01 global: siguen provisión/certificado/firewall y prueba real en las PCs,
instalación como servicio Windows, estaciones con identidad fuerte y operaciones/evidencias
fiscales con autorización transaccional/reautenticación. B3 conserva candidatos, no importador
aprobado. A2/T01–T06 siguen pendientes; C–H no se implementan por inferencia.
Emisión/firma/transmisión/PROD permanecen bloqueadas. Próximo paso: despliegue real asistido de
B2 y fuentes oficiales, después aprobación/importador fiscal y contratos C. No crear permisos
fiscales sin caso implementado. No pedir claves/certificados fiscales por chat.

## Evidencia local

Baseline Linux: 429 aplicación + 23 HTTP correctas, 16 Windows omitidas explícitamente.
Después del bloque: 429 aplicación + 56 servidor = **485** correctas; 18 Windows omitidas.
Ocho pruebas del verificador y ocho PDF intactos. Compilación Release previa sin warnings/errores;
compilación final también sin warnings/errores.

[CI Windows #231](https://github.com/Gust4av033/GoDental/actions/runs/40141630582) correcto,
sobre **3a2c90710b9b4c8c09a169843fb23e8f07c32b50**, job `121014404366`: Release cero
warnings/errores, **503 xUnit (429 aplicación + 74 servidor), cero omitidas; 829 comprobaciones
SQL / 1268 WPF**, sin errores de binding reportados. Son 35 xUnit nuevas (33 portables + dos
TLS nativas) y cuatro comprobaciones SQL nuevas (39 totales de identidad). Incluye HTTPS real
con validación estándar del cliente, hostname incorrecto, cadena no confiable, ausencia de
certificado, arranque bloqueado antes de escuchar y rechazo de grants de vistas/columnas.
Dieciocho casos Windows se ejecutaron; sólo el runner aislado instala/retira una CA **TLS**
sintética en LocalMachine/Root y una hoja con clave en LocalMachine/My. No hay certificado MH.
CI limita pruebas bloqueadas a dos minutos sin recopilar dumps. Nueve proyectos NuGet, cero
avisos altos/críticos; consulta local sin vulnerabilidades conocidas. Ocho pruebas documentales,
ocho fuentes intactas y 001–027 sin cambios. El cierre posterior sólo documenta evidencia.

Persisten avisos de herramientas Actions DEP0040/DEP0169 y Node 20 forzado a Node 24: SEC-06,
no warnings C# ni vulnerabilidad nueva confirmada. No cerrar S1 por esta prueba: falta cuenta,
SQL/certificado/firewall y confianza desde las PCs reales, servicio Windows e identidad fuerte
para estaciones, operaciones/evidencias fiscales y reautenticación/aprobación de secretos.
La conexión HTTPS nativa usa un host de ensayo con el selector real, no acredita el despliegue
completo bajo la cuenta real del negocio ni interoperabilidad Hacienda.

Revisión adicional del guard: se incluyen vistas y funciones de datos, SELECT/UPDATE por
columna, funciones escalares con EXECUTE y roles fijos privilegiados de servidor. La revisión
B1 sólo cubría permisos completos sobre tablas y procedimientos; se añade prueba de grant
sobre PasswordHash y sobre una vista real instalada. No basta la pertenencia al rol ni ocultar
botones. SEC-01 global sigue abierto; este control no equivale a pentest o aislamiento completo.

La [CI del PR #232](https://github.com/Gust4av033/GoDental/actions/runs/40141633686) también
pasó completa en otro runner. El fixture espera hasta diez segundos a que Windows confirme
la cadena de la CA recién provisionada; no omite errores de confianza. El caso negativo usa
una CA que nunca se instala, independiente del caché de retirada de raíces. No se reintenta
ningún envío fiscal: es preparación local sintética de pruebas. Código de producción sin cambios
durante este ajuste del fixture.
