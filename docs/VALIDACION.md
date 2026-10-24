# Validación de baseline v0.1

## Custodia genérica B3 · 24/10/2026

[DEV-53](DEV-53-Custodia-Secretos.md), código `ba6ca34f388618a0f0a9701a735c96ad1d8503de`:
[CI Windows #215](https://github.com/Gust4av033/GoDental/actions/runs/40113142251) correcto.
Release cero warnings/errores; **468 xUnit (429 aplicación + 39 servidor), cero omitidas;
825 comprobaciones SQL / 1170 WPF**, sin errores de binding reportados. Dieciséis pruebas
Windows reales: DPAPI/ACL y otra cuenta sintética, particiones/versiones, manipulación,
revocación, archivos bloqueados entre procesos, restauración bajo misma identidad/perfil y CLI.
Nueve proyectos NuGet, cero avisos altos/críticos; ocho pruebas documentales y ocho fuentes
intactas. Node DEP0169 de herramientas Actions permanece en SEC-06. 001–027 sin cambios.
Linux: Release sin warnings/errores, 452 pruebas correctas y 16 Windows omitidas explícitamente.

No hay importador/aprobación fiscal, firma, transporte ni material real MH. Recuperación DPAPI
en otro host, reconciliación de backups antiguos, permisos/reautenticación/auditoría fiscal y
despliegue LAN real siguen pendientes. B2/SEC-01/03 globales abiertos. Última migración 027;
ninguna aplicada a la PC personal desde cloud. El cierre sólo cambia documentación.

## Servidor B1 e identidad · 23/10/2026

[DEV-51](DEV-51-Servidor-Identidad.md), código `c9b9710`, [CI Windows #203](https://github.com/Gust4av033/GoDental/actions/runs/40029963174):
Release cero warnings/errores, **440 xUnit (418 existentes + 22 HTTP) / 825 SQL (35 nuevas) /
1106 WPF**, sin errores de binding reportados. Ocho pruebas documentales/fuentes intactas;
parser PowerShell correcto. NuGet: nueve proyectos, cero avisos altos/críticos. CI PR #204 correcto.
Baseline nueva previa a cambios en `9600e15`: Release sin warnings/errores y 418 xUnit.

027 agrega 82ª tabla y cuatro procedimientos (22 totales), sin modificar 001–026. Prueba de
actualización desde 026 con datos, rol SQL limitado, revocación/expiración, concurrencia y rollback.
HTTP usa TestServer e identidad sintética; SQL usa usuario limitado real en LocalDB aislado.
No se certifica TLS/provisión de las PCs ni cierre global S1, secretos fiscales o emisión MH.
Pendientes personales **021 → 022 → 023 → 024 → 025 → 026 → 027**. Guía en
[configuración del servidor](Servidor-Configuracion.md). El cierre sólo cambia documentación.

## Preparación del nuevo plan DTE · 23/10/2026

[DEV-50](DEV-50-Preparacion-DTE.md) registra fetch/HEAD/PR y auditoría del estado real antes
de cambiarlo. Baseline inicial `657d836`: restore/Release con SDK 8.0.426 y Windows targeting,
cero warnings/errores y **418 xUnit**. Sin LocalDB/WPF ejecutables en Linux.
FIS-01 pasa a v0.2 de especificación; ocho PDF incorporados sin cambiar bytes y con inventario
SHA-256, más verificador documental. Contratos técnicos externos, servidor y credenciales siguen
pendientes; no hay DTE real, pruebas MH ni migración nueva.

Validación final sobre `f805dac`: [CI Windows #199](https://github.com/Gust4av033/GoDental/actions/runs/40025528003),
Release sin warnings/errores, **418 xUnit / 790 SQL / 1106 WPF**, más ocho pruebas del
verificador documental. Ocho PDF intactos; ningún script 001–026 modificado. NuGet: siete
proyectos, cero avisos altos/críticos. El CI del PR #200 también pasó. DEV-50 distingue esta
evidencia local/Windows de la validación fiscal real todavía pendiente y registra los avisos de
herramientas Actions. Este cierre sólo cambia documentación.

Las secciones siguientes son evidencia histórica de sus respectivos commits.

## Compras y recepciones · etapa 2.1A · 21/10/2026

[DEV-33](DEV-33-Compras-Recepciones.md), código `c9a7ae5`, [CI Windows #116 correcto](https://github.com/Gust4av033/GoDental/actions/runs/39684813213): Release sin advertencias/errores, **218 xUnit**, **477 comprobaciones SQL** y **625 WPF**, sin errores de binding. Agrega 39 comprobaciones SQL de OC/recepción, concurrencia, costo/FX, lotes, rollback y recuperación, con UI conectada y formularios en ambos temas. Localmente pasaron Release y test de solución (218) con Windows targeting.

Requiere **017 después de 016**, actualizando todas las estaciones. La BD del usuario no se modificó desde este entorno. Compras directas con costo completo queda implementado; importaciones/prorrateo y tratamiento de costo incompleto corresponden a 2.1B. [DEV-34](DEV-34-Impresion-Formatos.md) registra la impresión configurable solicitada, aún sin implementación. Piloto entre PCs e impresora/lector/DPI físicos siguen pendientes.

## Complementos MVP · etapa 2.0B · 21/10/2026

[DEV-32](DEV-32-Complementos-MVP.md), código `f34b1a3`, [CI Windows #110 correcto](https://github.com/Gust4av033/GoDental/actions/runs/39678804036): Release sin advertencias/errores, **202 xUnit**, **438 comprobaciones SQL** y **593 WPF**, sin errores de binding. Agrega 39 comprobaciones SQL de parámetros/códigos/tarjeta y las pantallas/formularios en ambos temas. El fondo del logo sigue el tema y su rotulación mantiene contraste en Dark. Localmente pasaron Release y test de solución (202) con Windows targeting.

Requiere **016 después de 015**, actualizando todas las estaciones. No se aplicó en la BD del usuario. La validación WPF y sus capturas son automáticas, sin certificar hardware/DPI físico. DEV-32 detalla los límites. La siguiente entrega del plan es 2.1A, compras y recepciones; etiquetas, series, conciliación y terminal bancaria siguen pendientes.

## Recuperación de operaciones · etapa 2.0A · 21/10/2026

[DEV-31](DEV-31-Recuperacion-Operaciones.md), código `c3d3445`, [CI Windows #106 correcto](https://github.com/Gust4av033/GoDental/actions/runs/39644476282): Release con 0 advertencias/errores, **187 xUnit**, **399 comprobaciones SQL** y **575 WPF**, sin errores de binding. Incluye 24 comprobaciones SQL de recuperación, procesos terminados antes/después del commit y cierre real con pendiente durable. La pantalla consulta/resuelve sin reenviar automáticamente. Las suites SQL siguen usando bases aisladas del runner.

Requiere migración **015**, con actualización coordinada de todas las estaciones; no se aplicó en la base del usuario. La puesta en operación entre dos PCs, respaldo/restauración y hardware sigue pendiente. DEV-30 conserva la revisión original; los hallazgos R1/R2 se resuelven en este incremento y los demás bloques continúan pendientes.

## Revisión vigente · 21/10/2026

[DEV-30](DEV-30-Revision-Fase-2.md) revisa `feature/desarrollo` en `28ffc5f` contra DM-01 y los documentos funcionales. Compilación Release local con SDK 8.0.426 y Windows targeting: 0 advertencias/errores; **173 xUnit aprobadas**. La consulta NuGet de paquetes directos/transitivos no reportó vulnerabilidades conocidas.

[CI Windows #98](https://github.com/Gust4av033/GoDental/actions/runs/39637090540) del mismo código: **173 xUnit, 374 comprobaciones SQL y 570 WPF** correctas, sin errores de binding y con Release sin advertencias/errores. SQL usa exclusivamente bases aisladas del runner. El smoke comprueba también salida real de los procesos de acceso y ventana principal.

Las pantallas de existencias iniciales, Pedido → POS y vista previa/impresión están conectadas (DEV-26/27); los estados «UI pendiente» inferiores corresponden a sus entregas históricas. La recuperación se completa después en DEV-31 y los primeros complementos en DEV-32; siguen pendientes los módulos restantes de DEV-30 y la validación operativa de dos estaciones, respaldos/restauración y hardware. No se confirma instalación de 014 en la PC del usuario ni se declara producción aprobada.

## Códigos automáticos y cierre · 19/10/2026 (El Salvador)

[DEV-20](DEV-20-Codigos-Cierre.md) y [CI Windows #61](https://github.com/Gust4av033/GoDental/actions/runs/39349292934), código `9547d41`: Release sin errores/advertencias, 140 casos xUnit, 347 comprobaciones SQL y 125 WPF correctas. Incluye altas simultáneas, colisiones históricas, rollback y reaplicación de 014, códigos de personal y dos procesos que terminan al cerrar acceso/espacio de trabajo después de limpiar el host. Total SQL vigente: 76 tablas, 8 vistas y 18 procedimientos. No se aplicaron migraciones en el equipo del usuario.

## MVP operativo · lógica · 19/10/2026

Incremento y límites en [DEV-17](DEV-17-MVP-Logica.md). [CI Windows #44](https://github.com/Gust4av033/GoDental/actions/runs/39328112837), código `f51f75c`: Release sin errores/advertencias, **116 casos xUnit**, **329 comprobaciones SQL** (102 del nuevo MVP) y **101 comprobaciones WPF** correctas, sin errores de binding. Incluye generación real de PDF, concurrencia, rollback e instalación/reaplicación de 012–013 (76 tablas/8 vistas/17 procedimientos). El contenedor SQL Server no pudo descargarse en Linux; SQL se ejecutó en LocalDB aislado del runner Windows. DEV-18 documenta la corrección del cierre de capturas integrada en ambas ramas. No se instalaron cambios en la base del usuario; faltan conectar las nuevas pantallas y validar el recorrido real en dos estaciones para cerrar el MVP funcional completo.

## Solución .NET

- La solución mantiene cuatro proyectos: Domain, Application, Infrastructure y WPF, más el proyecto de pruebas.
- El destino oficial es .NET 8 / C# 12; WPF usa `net8.0-windows`.
- La composición usa Generic Host + Dependency Injection.
- El flujo de GitHub Actions ejecuta restore, build Release, tests y cobertura en Windows y ya cuenta con ejecuciones satisfactorias en `main`.
- `scripts/verify.ps1` reproduce localmente restore + build + tests + coverage.

## Estructura del repositorio

- La estructura está alineada con ARQ-01.
- La carpeta de PDF de Infrastructure quedó normalizada como `Pdf/`; se eliminó la variante duplicada `PDF/`.
- Visual Studio 2022 es el IDE de referencia para el desarrollo del proyecto.

## SQL v0.1

Se revisó estáticamente la secuencia completa `database/001` a `008` contra DB-01 y la documentación funcional vigente.

Resultados:

- `001`–`006` crean **72 tablas**; DB-01 documenta exactamente las mismas 72 tablas.
- No se detectaron tablas faltantes ni tablas adicionales fuera del modelo aprobado.
- Las referencias cruzadas principales entre esquemas/scripts resuelven dentro de la baseline.
- Se utilizan PK, FK, CHECK, índices y ROWVERSION de acuerdo con el diseño.
- No se utilizan triggers.
- `007_Seed.sql` es idempotente, no crea credenciales administrativas por defecto y activa la precedencia comercial definida en VEN-01A.
- `008_ViewsProcedures.sql` contiene **8 vistas** y **12 procedimientos almacenados**.
- Las operaciones críticas de venta, caja, abonos, correlativos y recepción de compra usan transacciones explícitas y mecanismos de bloqueo para concurrencia.

### Límite de esta validación

En este entorno no se ejecutó la secuencia sobre una instancia real de SQL Server. Por tanto, la baseline está aprobada para integración y desarrollo, pero la instalación del motor debe certificarse ejecutando `001 → 008` sobre una base local/de desarrollo antes de producción.

## Decisión VEN-01A

VEN-01A cierra la precedencia de precios que VEN-01 v0.1 había dejado pendiente:

**lista base → condición/tipo de cliente → comparación con promoción → mejor precio válido → descuento manual autorizado**.

No existe acumulación automática entre beneficios; una promoción sólo se acumula cuando está marcada expresamente como acumulable. `007_Seed.sql` refleja esta decisión con `Precios.PrecedenciaDefinida = true`.

## Alcance pendiente intencional

No forman parte de la baseline funcional inicial y continúan como evolución/preproducción: integración DTE real con Hacienda, proveedor definitivo de tarjeta, automatizaciones externas de WhatsApp/Instagram/Facebook y otras decisiones operativas que VEN-01A clasifica como diferidas.

Esta validación certifica el **punto de partida técnico/documental del repositorio**, no una aplicación funcional terminada.

## Avance de escritorio · 17/10/2026

- [DEV-01](DEV-01-Arranque.md): Shell/MVVM, navegación, menú compacto, Light/Dark, perfil local y diagnóstico SQL de sólo lectura.
- Build Release: 0 errores y 0 advertencias, con SDK 10.0.401, C# 12 y destinos .NET 8 de la baseline. No se cambió `global.json`.
- Pruebas xUnit existentes: 2 aprobadas, con cobertura.
- Smoke WPF: 20 comprobaciones aprobadas y vistas renderizadas en 1280×720 y 910×510. Se revisaron los PNG; no hubo errores de binding. El tamaño reducido permite revisar el área disponible con escalado, sin afirmar certificación completa de DPI/hardware.
- Auditoría NuGet, incluyendo dependencias transitivas: sin vulnerabilidades conocidas reportadas por las fuentes consultadas tras fijar Pkcs 10.0.13.
- Distribución local de prueba: Windows x64 autocontenida con runtime 8.0.32.
- No se conectó a una base operativa, no se instalaron scripts y no se certificaron aún login, permisos, ventas ni integridad SQL en ejecución.

## Catálogo y seguridad · v0.2 · 17/10/2026

Esta sección actualiza los límites del incremento anterior según [DEV-02](DEV-02-Seguridad-Catalogo.md).

- Se instaló 001–009 en `localhost/GoDental_Development`, separada de cualquier base operativa: 72 tablas, 8 vistas y 12 procedimientos. La base de desarrollo queda sin usuarios/contraseñas predeterminados y sin productos artificiales.
- 49 comprobaciones SQL sobre bases aisladas `GoDental_Tests_<GUID>`: primer administrador concurrente, bloqueo y contadores concurrentes, cambios de contraseña, vendedor/permisos, gestión de cuentas, contexto y vinculación de sesión, productos/categorías, rowversion, trazabilidad y rollback de auditoría.
- 16 casos xUnit de política, hashes, sesión y validaciones, incluyendo los dos casos anteriores de Result.
- 22 comprobaciones de smoke WPF, con vistas de acceso, productos y usuarios en Light/Dark y estado de vendedor. Sin errores de binding.
- Build Release local con SDK 10.0.401, C# 12, .NET 8; el proyecto conserva el SDK .NET 8 definido por `global.json` para los equipos que lo tengan instalado.
- Los cambios no habilitan producción ni certifican todavía ventas, compras, movimientos de stock, backups o dos laptops por LAN.

## Inventario operativo · v0.3 · 17/10/2026

Esta sección actualiza el alcance de inventario según [DEV-03](DEV-03-Inventario-operativo.md): existencias, lotes y vencimientos, ajustes, bajas, transferencias y Kardex reales en SQL, además de filtros y mantenimiento de categorías.

- Build Release sin errores ni advertencias; 20 casos xUnit aprobados.
- SQL Server 2022 local: 49 comprobaciones de regresión y 40 de inventario en bases aisladas, incluyendo costo decimal, transferencias con lotes, última unidad concurrente, permisos revocados y rollback de auditoría.
- 28 comprobaciones WPF y capturas Light/Dark; sin errores de binding. Se corrigió una regresión de filtros reproducida con la vista enlazada, el contraste de fechas/categorías en oscuro y el foco del editor.
- Instalador general actualizado a 001–010 y verificación opcional de ambas suites SQL.
- La validación cubre movimientos de inventario de este incremento. Ventas, recepciones, caja, backups y dos laptops por LAN conservan su validación pendiente.

## Conteos y proveedores · lógica · 19/10/2026

- `scripts/verify.ps1 -SqlIntegration` completo en Windows con SQL Server local: Release sin errores ni advertencias; 40 casos xUnit y 57 comprobaciones WPF existentes correctas, sin errores de enlaces.
- 194 comprobaciones SQL correctas en bases aisladas: 49 de seguridad/catálogo, 40 de inventario, 67 nuevas de conteos y 38 nuevas de proveedores. No se aplicaron migraciones ni datos de pruebas sobre `GoDental_Development` del usuario.
- Conteos: captura/cierre concurrentes, protección de reservas hasta 0.0001, fotografía obsoleta incluso cuando el saldo vuelve a su valor anterior, lotes, costos históricos, permisos revocados y rollback atómico. Reaplicar 009–011 conserva cantidades, pendientes, versiones e historial.
- Proveedores: código único/histórico, versión del agregado con contactos y relaciones, principal único activo, costos/moneda/MOQ, ocultación de costo tras revocar permiso, ausencia de movimientos de stock y rollback de auditoría.
- Los servicios/repositorios nuevos se registran en DI. No hay cambios en las vistas ni ViewModels WPF; los contratos y la guía para conectarlos están en DEV-09, DEV-10 y DEV-11.
- La migración 011 conserva 72 tablas/8 vistas y eleva a 17 los procedimientos. Debe instalarse en la base elegida antes de conectar las pantallas de conteos.
- El CI incorpora las cuatro suites SQL sobre bases LocalDB aisladas del runner, además de xUnit y smoke WPF. Las pruebas concurrentes usan conexiones distintas al mismo SQL; no equivalen a una certificación real de dos PCs por LAN ni a despliegue productivo.
- La primera ejecución completa de CI detectó que tres pruebas de sesión dependían de `localhost`. `IWorkstationConnectionOpener` permite que sólo el ejecutable de pruebas redirija ese transporte a LocalDB, manteniendo la fábrica real de sesión/contexto y todas sus comprobaciones; la aplicación continúa usando `SqlConnectionFactory` y rechazando perfiles LocalDB. El cambio de base se prueba ahora con otra base aislada, sin consultar la base de desarrollo del usuario.


## Interfaz del recorrido MVP · 19/10/2026

[CI Windows #50](https://github.com/Gust4av033/GoDental/actions/runs/39331437129), commit `10610a8` de `feature/ui`: Release sin errores ni advertencias, 139 pruebas xUnit, 329 comprobaciones SQL (40 inventario, 49 seguridad/catálogo, 67 conteos, 38 proveedores, 33 clientes y 102 MVP) y 123 comprobaciones WPF correctas, sin errores de binding. Se renderizan páginas independientes de Caja, POS, Cotizaciones, Crédito, Pedidos y Ventas/PDF, más sus diálogos en Light/Dark y tamaños compactos. Las pruebas unitarias nuevas cubren solicitudes congeladas, reenvío/consulta por OperationId, conflictos, cambio de sesión, cálculos comerciales del servicio, conversión, autorización sin conservar contraseña, abonos, pedidos y exportación.

SQL usa exclusivamente bases aisladas LocalDB del runner. No se ejecutó `scripts/verify.ps1` en el equipo del usuario ni se modificó su base. Las capturas y pruebas automatizadas no sustituyen el recorrido manual entre dos estaciones, impresión/hardware y comprobación física del DPI. El contrato de resolución negativa definitiva tras una respuesta perdida está pendiente para lógica en DEV-15; la UI conserva la solicitud incierta para evitar duplicados. Detalle y alcance en DEV-19.


## Refinamiento con Imagenes · 20/10/2026

`feature/desarrollo`, código `e14af53`: [CI Windows](https://github.com/Gust4av033/GoDental/actions/runs/39449594494) aprobó Release (0 errores/advertencias), 144 pruebas xUnit, 349 comprobaciones SQL y 144 WPF, sin errores de binding. DEV-23 documenta la revisión de conexiones y los cambios de componentes compartidos, catálogo POS, cobro, caja y crédito. La selección por tarjetas conserva el DTO y método reales, no registra pagos ni altera totales; Cobrar permanece visible. Light/Dark y ventanas compactas cubiertos por smoke. Pendiente revisión manual con el monitor y DPI del usuario; SQL se ejecuta sólo en bases aisladas del runner.
