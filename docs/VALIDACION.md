# Validación de baseline v0.1

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
