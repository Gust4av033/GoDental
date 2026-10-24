# DEV-53 — Custodia genérica de candidatos secretos (base B3)

24/10/2026. Continúa DEV-51 después del refinamiento de navegación DEV-52. Diseño previo al
código; no cambia contratos Hacienda, tablas ni permisos SQL. Baseline remoto: `262c3d7`.

## Alcance y decisiones

Se implementa un almacén **de candidatos opacos no verificados**, privado del servidor Windows.
No es el importador fiscal ni acredita certificado, pertenencia, vigencia o autorización MH.
`IFiscalSecretCandidateStore` permite crear una versión inmutable, abrirla en un buffer desechable
y revocarla. No existe versión activa, selección automática, rollback ni importación HTTP/WPF.
El futuro `IFiscalSecretStore` de firma sólo consumirá versiones aprobadas con contrato oficial,
reautenticación, permisos específicos y auditoría transaccional; ese caso aún no existe.

- Referencia tipada: identificador interno de emisor GUID, TEST/PROD internos, propósito
  (material de firma/protección/API) y versión GUID. No NIT, ruta ni alias arbitrario del cliente.
- Cada raíz se liga una sola vez a emisor/ambiente/SID de servicio. TEST y PROD requieren raíces
  y cuentas de servicio distintas en el despliegue; no se acepta otra partición ni fallback.
- DPAPI CurrentUser, nunca LocalMachine; entropía de contexto liga cada blob a su referencia.
  Un marcador protegido impide reutilizar una raíz cambiando emisor/ambiente/SID por configuración.
- Directorio local fijo, existente, con ACL protegida: sólo SID de servicio y SYSTEM. Rechazo de
  enlaces/reparse points, UNC, medios no NTFS/fijos, ACL amplias o SID distinto del proceso.
  Se comprueba antes de cada operación. Provisión explícita de raíz nueva y vacía, nunca reparar
  silenciosamente ACL de un directorio existente ni leer una raíz con permisos inseguros.
- Publicación por archivo temporal **ya cifrado** y rename sin reemplazo; bloqueo de archivo por
  referencia entre procesos; flush durable. Una escritura incompleta no publica candidato.
  Revocación persistente impide nuevas lecturas; no puede retirar bytes ya abiertos por otro
  proceso. No borrar tombstones ni revivir referencias; restaurar un backup antiguo requiere
  reconciliar revocaciones externas antes de permitir futuros usos fiscales.
- Máximo 64 KiB por candidato, lectura cifrada acotada, errores públicos genéricos sin rutas ni
  excepción interna. Buffers propios se limpian al salir/dispose; el llamador limpia su entrada.
  No serializar bytes, no logs de contenidos, no devolverlos por endpoints.
- Diagnóstico local `--check-secret-store`: sólo datos sintéticos aleatorios; crea raíz temporal
  privada dentro de la raíz ya provisionada, comprueba DPAPI/lectura/reinicio/versión/revocación y
  la elimina. No importa archivos ni inicia Kestrel/SQL. Provisión local `--initialize-secret-store`
  crea únicamente raíz vacía ligada a la identidad actual, sin capturar secretos.

## Validación prevista

Pruebas puras de referencias/buffer; Windows real para DPAPI CurrentUser y ACL, manipulación,
particiones, concurrencia, reinicio, rotación por versión y restauración cifrada bajo la misma
identidad. Identidad distinta: token de inicio de sesión Windows con cuenta sintética local y denegación ACL/DPAPI.
El fixture crea/retira esa cuenta con NetAPI en runner elevado; CI exige ejecutarlo. En Windows
local sin elevación sólo ese caso se omite explícitamente. El bloqueo entre procesos usa un
proceso hijo independiente, sin esperar mediante sleeps ni compartir estado en memoria.
Fixtures sintéticos fuera de artefactos de CI; nunca certificados/credenciales MH.
Regresión Release/xUnit/SQL/WPF y comprobación de fuentes oficiales intactas.

## Límites y continuación

No cierra B2 ni SEC-01/03 globales. Pendientes: provisión/TLS en las PCs reales, estaciones con
identidad fuerte, autorización de operaciones/evidencias fiscales y reautenticación del caso real;
importador conforme T05/T06, aprobación/auditoría y rotación oficial, recuperación del perfil/DPAPI
en otro host (o reaprovisionamiento oficial), backup coordinado y prueba de restauración real.
A2 sigue bloqueado por T01–T06. Emisión/firma/transmisión/PROD deshabilitadas.
Última migración **027**. Aplicar sólo pendientes en orden; no aplicadas a las PCs desde cloud.

## Evidencia de esta iteración

Baseline local previa: Release sin warnings/errores, 418 xUnit de aplicación y 22 HTTP correctas.
Después de implementar: Release sin warnings/errores, **429 aplicación + 23 HTTP = 452**
correctas en Linux; 16 pruebas Windows omitidas explícitamente (DPAPI/ACL no se simulan).
Ocho pruebas del verificador y ocho PDF SHA-256 correctos; 001–027 sin cambios.
Validación nativa: [CI Windows #215](https://github.com/Gust4av033/GoDental/actions/runs/40113142251)
sobre **ba6ca34f388618a0f0a9701a735c96ad1d8503de**, job `120928859988`: Release cero
warnings/errores; **468 xUnit (429 aplicación + 39 servidor), sin omitidas; 825 comprobaciones
SQL y 1170 WPF**, sin errores de binding reportados. Incluye 16 pruebas Windows reales y
la denegación ACL/DPAPI bajo otra cuenta sintética, bloqueos entre procesos y comandos locales.
Las 1170 WPF conservan las 64 comprobaciones de navegación DEV-52 incorporadas del remoto.
Nueve proyectos NuGet consultados, cero avisos altos/críticos en CI; consulta local sin
vulnerabilidades conocidas. Ocho pruebas documentales, ocho PDF intactos y 001–027 sin cambios.
No hay llamadas/credenciales MH ni validación LAN personal. El cierre posterior sólo documenta
esta evidencia. Avisos Node DEP0169 de herramientas Actions siguen registrados en SEC-06.
