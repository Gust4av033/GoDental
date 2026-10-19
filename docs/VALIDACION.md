# Validación de baseline v0.1

## MVP operativo · lógica · 19/10/2026

Incremento y límites en [DEV-17](DEV-17-MVP-Logica.md). Compilación Release cruzada para Windows sin errores/advertencias y 116 casos xUnit correctos, incluyendo generación de PDF. Se añade suite SQL aislada `--run-mvp` y migraciones aditivas 012–013 (76 tablas/8 vistas/17 procedimientos). Revisión T-SQL estática satisfactoria; ejecución SQL y pruebas WPF de las funciones nuevas pendientes. El contenedor SQL Server no pudo descargarse en el entorno Linux por denegación de red. No se instalaron cambios en la base del usuario ni se certifica todavía el MVP funcional completo.

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
