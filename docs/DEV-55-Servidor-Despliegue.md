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
compilación final y Windows/SQL/WPF pendientes de evidencia nueva, no reutilizar CI de DEV-54.
