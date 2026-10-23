# DEV-45 — Revisión inicial de seguridad y fases de corrección

23/10/2026, El Salvador. Solicitada junto con la prioridad Fiscal/DTE y Online (DEV-44).
Alcance: código y configuración versionados actuales, autenticación/permisos, SQL/archivos,
dependencias NuGet directas/transitivas y diseño de despliegue. No es una auditoría exhaustiva,
pentest independiente ni inspección de las PCs, LAN, SQL o credenciales del negocio.

## Evidencia revisada

- `Pbkdf2PasswordHasher`: PBKDF2-SHA256, 600000 iteraciones, salt aleatorio de 16 bytes,
  comparación de tiempo constante y borrado del buffer derivado. `SqlIdentityGateway`:
  bloqueo/intentos, validación de longitud y mensajes de acceso genéricos.
- Application y SQL validan permisos; SQL revalida usuario/estado/permisos, versiones, ledger
  y transacciones. Pruebas reales existentes de permisos revocados, concurrencia y rollback.
  La UI no constituye la frontera de autorización. `WorkstationConnectionFactory` comprueba
  base/servidor de sesión y usuario activo; esto no equivale a identidad SQL de mínimo privilegio.
- Las rutas SQL revisadas usan parámetros para entradas de usuario; interpolaciones de columnas,
  filtros y orden son fragmentos internos. No se observó una inyección en las rutas revisadas;
  no se declara auditada exhaustivamente cada consulta ni cada dependencia.
- TLS obligatorio en configuración de estaciones; certificado autofirmado sólo en servidor
  local y BD de desarrollo/pruebas, validado por `WorkstationSettingsValidator`. LAN/TLS real
  y la conexión alternativa de configuración requieren comprobación de despliegue.
- Evidencias: rutas administradas, rechazo de enlaces/redirecciones de filesystem, límites PNG,
  hash y propietario. Revisar ACL del recurso compartido en las PCs antes de uso real.
- El diario durable guarda tickets/identidades/fecha/scope; **no** guarda el cuerpo de solicitudes,
  contraseñas ni certificados. No registrar como fuga de datos algo que no almacena.
- Búsqueda limitada de patrones de clave privada/password en archivos rastreados actuales:
  cuatro coincidencias revisadas, correspondientes a placeholder del ejemplo local y pruebas
  sintéticas/generadas. Ninguna clave privada observada. No cubre historial completo de Git,
  archivos ignorados, backups ni secretos del entorno; no se imprimen valores encontrados.
- NuGet: consulta `dotnet list GoDental.sln package --vulnerable --include-transitive --format json`
  con Windows targeting: **7 proyectos**, sin avisos conocidos ni problemas de consulta al revisar.
  Esto no significa ausencia de vulnerabilidades desconocidas ni seguridad general garantizada.
  Nueva comprobación reproducible `scripts/check-dependencies.ps1` en CI: falla si la consulta
  no es válida o hay avisos High/Critical; conserva JSON como artefacto y muestra otros avisos.

## Hallazgos / brechas a corregir

La prioridad expresa impacto potencial y condición de despliegue; no todos los puntos son una
vulnerabilidad explotada o un CVE. No se conectó a una instalación real para comprobar su exposición.

| ID | Prioridad / naturaleza | Evidencia y riesgo | Corrección y fase |
| --- | --- | --- | --- |
| SEC-01 | Alta, frontera de despliegue | Conexión Windows integrada y escrituras SQL directas; la autorización de aplicación recibe IdUsuario. Si la identidad SQL del operador permite acceso directo amplio, puede saltarse el cliente o impersonar sus parámetros. No se verificaron privilegios de la BD personal. | **S1, antes de DTE externo/producción**: diseñar identidad SQL y frontera confiable, limitar tablas/procedimientos y probar denegación de acceso directo/impersonación. Si el modelo de amenaza requiere cliente no confiable, usar servicio central que autentique y autorice; sólo ocultar una cadena SQL no resuelve esto. DEV-36 no puede declarar producción lista sin este cierre. |
| SEC-02 | Media, sesión | `UserSession` no implementa expiración absoluta/inactividad; depende de cierre manual y cambios de sesión. | **S2 antes del piloto**: cerrar política, bloqueo por inactividad, reautenticación sensible y pruebas de respuestas tardías. Revalidación SQL existente no sustituye esa política. |
| SEC-03 | Alta condicionada a DTE, control preventivo | Las tablas fiscales son cimientos; `ValorProtegido` no es por sí mismo un gestor de secretos. No hay transporte/firma/verificador oficial ni custodia implementada. Hoy no hay interfaz para capturar claves ni adaptador DTE activo. | **S1 bloqueante para activar DTE**: referencias a almacén seguro/certificado, ACL, separación pruebas/producción, rotación y revocación, TLS/destinos autorizados, respuestas sanitizadas, reconciliación e idempotencia. Nunca habilitar emisión real mediante un estado manual o payload no verificado. |
| SEC-04 | Alta operacional si se usa en producción | DEV-36 sigue sin respaldo/restauración automatizados, retención/copia secundaria y recuperación de BD + evidencias comprobada. | **S2 antes de conservar datos productivos/piloto**: implementar y restaurar en destino separado; proteger backups y evidencias. No confundir este riesgo de disponibilidad con un CVE del código. |
| SEC-05 | Media, archivos/datos | Evidencias y archivos locales dependen de permisos del perfil/recurso compartido. No hay política final de retención, cifrado del disco/backup ni ACL instalada y comprobada. | **S2, antes del instalador/piloto**: permisos mínimos por carpeta/usuarios, cifrado y copias protegidas, retención/limpieza y prueba de acceso no autorizado. Conservar validación de rutas/hashes existente. |
| SEC-06 | Media, cadena de suministro | Workflow usa tags de acciones (`@v4`) y no hay proceso final de firma/verificación del instalador/actualizador. | **S2 antes de distribuir**: pin de acciones a revisiones verificadas, revisión de dependencias y firma/verificación de artefactos, compatibilidad y rollback. Auditoría NuGet agregada ahora; no sustituye esos controles. |

No se identificó una vulnerabilidad crítica confirmada en esta revisión limitada. No se emite un
certificado de ausencia de fallos. Si una prueba posterior confirma una crítica/alta explotable,
corregir antes de habilitar su recorrido, aunque cambie la prioridad funcional solicitada.

## Fases de seguridad

- **S0 — ahora, con cada módulo:** revisión de permisos/entradas, secretos/logs, dependencias,
  amenazas y pruebas de fallos/concurrencia; corregir regresiones antes de integrar. Fiscal interno
  sin emisión real y con revisión separada; sin importación de tokens/certificados ni JSON fiscal.
- **S1 — antes de conectores DTE/Online:** cerrar SEC-01/03, identidad/broker según amenaza,
  custodia de secretos, esquemas/endpoints oficiales vigentes, proveedores autorizados y
  consentimiento/exclusiones; ensayos en ambiente de pruebas, nunca producción implícita.
- **S2 — al completar módulos y antes de instalador/piloto:** cerrar sesión, ACL/retención,
  respaldo/restauración, dependencias/advisories, firma y actualizaciones; revisar otra vez el
  conjunto y realizar pruebas de abuso. Recomendable evaluación independiente antes de DTE real.
- **S3 — piloto formal y mantenimiento:** probar acceso entre PCs/TLS/hardware y recuperación,
  documentar resultados, actualizar dependencias y responder a avisos nuevos.

El orden funcional del usuario se respeta sin relegar riesgos altos al final. El siguiente módulo
funcional después de Fiscal interno es Online interno; escalas/listas/promociones vienen después
de la prioridad de módulos nuevos. DEV-44 registra instrucciones: faltan **021 → 022 → 023 → 024**,
incluida 021, en la PC personal; no se ha modificado esa BD desde cloud.


## Validación registrada

[CI Windows #171](https://github.com/Gust4av033/GoDental/actions/runs/39912832720) sobre `e8cab1a`
(job `120279529268`): auditoría NuGet de siete proyectos sin avisos conocidos, Release sin
advertencias/errores, **347 xUnit / 721 SQL / 965 WPF**, sin errores de binding. Los logs no
contienen avisos de dependencia; el informe JSON queda en el artefacto del CI. La suite fiscal
valida permisos revocados, conflictos y rollback sin emisiones ni efectos financieros.
Esto verifica los controles implementados en el alcance de DEV-44; no cierra SEC-01–06 ni
certifica la instalación real. Repetir revisión de seguridad con los módulos y antes de sus fases
bloqueantes. Documentación posterior no cambia el código validado.


## Continuación Online interno · DEV-46

Alta manual de fuentes/prospectos y exclusiones, sin proveedor de red/Worker/envíos. URLs
son sólo referencias documentales; no se consultan ni abren y se rechazan credenciales/parámetros,
fragmentos, destinos locales/IP y esquemas distintos de HTTPS. Publicidad de datos no equivale a
consentimiento. Sin API para levantar opt-out; reingreso/edición/vínculo conservan supresión.
Permisos separados, versiones, ledger y bitácora; conversión exige compra confirmada y no
modifica venta/cliente/caja/stock. Las entradas históricas no se reescriben para normalizarlas.
SEC-01–06 siguen abiertos según sus fases; esta entrega interna no cierra S1 ni certifica producción.


Validación de DEV-46: [CI Windows #177](https://github.com/Gust4av033/GoDental/actions/runs/39961747381)
sobre `f46d530`, job `120441191089`: Release sin advertencias/errores, **364 xUnit / 761 SQL /
1011 WPF**, sin errores de binding, auditoría NuGet de siete proyectos sin avisos conocidos.
Las pruebas nuevas incluyen preservación de opt-out ante reingreso/edición/vínculo, permisos revocados,
URLs documentales sin consultas externas y rollback/recuperación. Los hallazgos SEC-01–06 siguen
abiertos en sus fases: estas comprobaciones no cierran mínimo privilegio/identidad SQL, custodia,
sesión, distribución o restauración real. Sólo cambios documentales posteriores al código validado.
