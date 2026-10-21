# GoDental

GoDental es una aplicación de escritorio para la operación comercial de un depósito dental: inventario, compras/importaciones, ventas/POS, caja, crédito, seguridad, fiscal y comercial/online.

## Estado actual y siguiente etapa

La revisión del **21/10/2026** en [DEV-30](docs/DEV-30-Revision-Fase-2.md) distingue los recorridos implementados de los complementos y módulos pendientes respecto de DM-01. El núcleo operativo está conectado; compras/recepciones/importaciones, devoluciones, dashboard/reportes y las integraciones conservan trabajo pendiente. La segunda etapa empieza por recuperación de operaciones y complementos del MVP; el orden y criterios de aceptación están en ese documento.

Rama activa: **`feature/desarrollo`**. CI Windows #98 del código `28ffc5f`: Release sin errores/advertencias, **173 xUnit, 374 comprobaciones SQL y 570 WPF** correctas. La validación del piloto en dos estaciones, respaldo/restauración y despliegue sigue pendiente.

## Stack

- .NET 8 + C# 12
- WPF / MVVM
- SQL Server central
- Dapper + Microsoft.Data.SqlClient
- Microsoft.Extensions.Hosting / Dependency Injection
- QuestPDF
- xUnit

## Arquitectura

```text
GoDental/
├── GoDental.sln
├── src/
│   ├── GoDental.Domain/
│   ├── GoDental.Application/
│   ├── GoDental.Infrastructure/
│   └── GoDental.Wpf/
├── tests/
│   └── GoDental.Tests/
├── database/
├── docs/
├── scripts/
└── .github/
```

Reglas principales:

1. `Domain` no conoce WPF, Dapper, SQL Server ni QuestPDF.
2. `Application` depende únicamente de `Domain` y define contratos/casos de uso.
3. `Infrastructure` implementa acceso a datos, archivos, PDF e integraciones.
4. `Wpf` es UI + composition root; no contiene SQL ni lógica transaccional.
5. Las operaciones críticas se validan en SQL Server y deben ser seguras para múltiples PCs.
6. No se crea `GoDental.Worker` hasta que exista una tarea que deba ejecutarse con WPF cerrado.

## Abrir en Visual Studio 2022

1. Clonar el repositorio.
2. Abrir `GoDental.sln` en **Visual Studio 2022** con el workload **.NET desktop development** y SDK .NET 8 instalado.
3. Establecer `GoDental.Wpf` como proyecto de inicio y ejecutar.
4. La aplicación abre la ventana de acceso. Desde allí, abrir **Ajustes de conexión** para guardar el servidor/base y comprobar el acceso con la cuenta de Windows. No hace falta SQL para abrir la interfaz.
5. Restaurar paquetes NuGet y compilar la solución.

## Base de datos

La versión 0.3 incluye usuarios y catálogo de la [v0.2](docs/DEV-02-Seguridad-Catalogo.md), más existencias, lotes/vencimientos, ajustes, bajas, transferencias y Kardex con auditoría. También incorpora filtros y mantenimiento de categorías. [Alcance y guía de inventario](docs/DEV-03-Inventario-operativo.md).

La continuación agrega [conteos físicos](docs/DEV-09-Conteos.md): apertura, captura, cierre con ajustes y cancelación auditados. Su pantalla y la de proveedores están conectadas según [DEV-12](docs/DEV-12-UI-Conteos-Proveedores.md). Requiere la migración 011.

También incorpora [proveedores, contactos y referencias comerciales de productos](docs/DEV-10-Proveedores.md), con permisos, control de versiones y auditoría, y [clientes, direcciones, crédito e historiales](docs/DEV-13-Clientes.md).

El [incremento del MVP operativo](docs/DEV-17-MVP-Logica.md), incorporado a `feature/desarrollo`, agrega saldo inicial, códigos, precios/cotizaciones, ventas, caja, abonos, comprobante interno y pedidos/entregas. Requiere 012–013, más 014 para los códigos automáticos. Sus pantallas operativas están conectadas; [DEV-15](docs/DEV-15-Coordinacion-UI.md) identifica los contratos y las funciones que aún no tienen UI, como mantenimiento de códigos de barras y fiscal pendiente. Compras, recepciones, importaciones y otras ampliaciones conservan su trabajo pendiente.

El perfil sin credenciales en `%LOCALAPPDATA%\GoDental\workstation.json` alimenta ahora tanto el diagnóstico como las operaciones. Para este avance se usa Windows integrada sobre SQL local; la cuenta SQL de producción con mínimo privilegio y su almacén protegido sigue pendiente. No se guarda una contraseña SQL en el perfil.

En este equipo se preparó `GoDental_Development`, vacía de usuarios y productos reales. En la ventana de acceso, elige tu primer administrador. Para otro equipo de desarrollo con SQL local puedes ejecutar:

```powershell
dotnet run --project tests/GoDental.DatabaseTests -- --install-development .
```

La herramienta limita el destino a una base local de desarrollo marcada. Nunca instala sobre `GoDental` ni sobre una base existente sin su marcador.

`database/` contiene la **baseline SQL v0.1** aprobada y validada estáticamente contra DB-01. Para una base nueva los scripts deben ejecutarse, obligatoriamente, en este orden:

```text
001_Core.sql
002_Inventario.sql
003_Compras.sql
004_ClientesVentas.sql
005_Fiscal_Base.sql
006_Comercial_Base.sql
007_Seed.sql
008_ViewsProcedures.sql
009_Seguridad_Parametros.sql
010_Inventario_Operativo.sql
011_Conteos_Inventario.sql
012_MVP_Operativo.sql
013_Pedidos_MVP.sql
014_Codigos_Automaticos.sql
015_Resolucion_Operaciones.sql
```

La baseline define 72 tablas, datos iniciales controlados, 8 vistas y 12 procedimientos almacenados. Las migraciones 010 y 011 elevan el total a 17 procedimientos; 012–013 añaden cuatro tablas de operaciones/pedidos (76 en total). No crea un usuario Administrador con contraseña por defecto y no utiliza triggers.

Para una instalación local puede usarse `scripts/install-database.ps1` si `sqlcmd` está disponible. La primera ejecución debe hacerse contra una instancia de **desarrollo**, nunca directamente contra producción.

## Regla comercial de precios

`docs/VEN-01A.md` cierra la precedencia que VEN-01 había dejado pendiente:

**lista base → condición/tipo de cliente → comparación con promoción → mejor precio válido → descuento manual autorizado**.

No existe acumulación automática de beneficios; una promoción sólo puede acumularse cuando está configurada expresamente como acumulable.

## Probar antes de subir cambios

Desde PowerShell en la raíz del repositorio:

```powershell
.\scripts\verify.ps1
```

Ese script ejecuta Restore + Build Release + Tests + Coverage, equivalente al pipeline principal de GitHub Actions. Los resultados quedan en `TestResults/`.

También puedes ejecutar las pruebas desde **Visual Studio → Test → Test Explorer**.

## GitHub / CI

Cada Pull Request a `main` y cada push a `main` ejecuta CI automáticamente. También puede lanzarse manualmente desde **Actions → CI → Run workflow**. Los resultados y cobertura se publican como artifacts descargables.

Guía completa: [`docs/GITHUB.md`](docs/GITHUB.md).

## Configuración sensible

Nunca subir connection strings reales, contraseñas, tokens, certificados, `.bak`, logs o secretos fiscales/comerciales. Los valores locales viven fuera del control de versiones.

## Documentación

La documentación fuente vive en `docs/` y es la referencia para decisiones funcionales/técnicas. `VEN-01A` complementa `VEN-01` y prevalece sobre sus propuestas provisionales en materia de precedencia de precios. Si el código necesita contradecir una regla documentada, primero se versiona la decisión; no se introduce una excepción silenciosa.

## Conteos y proveedores

Las interfaces de conteos físicos y proveedores consumen los servicios existentes: apertura/captura/cierre/cancelación de conteos y fichas/contactos/referencias comerciales. [DEV-12](docs/DEV-12-UI-Conteos-Proveedores.md) conserva la entrega inicial; DEV-25/29 documentan el refinamiento. La validación automatizada Windows/SQL vigente se resume en DEV-30; el recorrido manual entre las estaciones sigue pendiente.

## Clientes y VS Code

El módulo Clientes incorpora ficha, contactos, direcciones fiscales/de entrega, condiciones comerciales y configuración separada de crédito. Las consultas de ventas, cuentas y abonos leen las tablas existentes según permisos. [Alcance y comprobaciones de DEV-13](docs/DEV-13-Clientes.md).

Para compilar y depurar desde una computadora Windows con VS Code, consulta [DEV-14](docs/DEV-14-VSCode-Windows.md). El repositorio incluye tareas de compilación/pruebas y configuración de F5.

## Desarrollo

- Ramas vigentes: `feature/desarrollo` para lógica y UI, separadas por capas y commits; `main` para la versión integrada y validada. `feature/ui` y `feature/logica` son referencias históricas. [Flujo y recuperación de ramas anteriores](docs/DEV-08-Ramas.md).
- `main` debe mantenerse integrable.
- Ejecutar `.\scripts\verify.ps1` antes de integrar cambios relevantes.
- Convenciones adicionales: `CONTRIBUTING.md`.
- Instrucciones para agentes/asistentes de programación: `AGENTS.md`.

## Recorrido MVP en interfaz

Caja, POS/cotizaciones, crédito/abonos, pedidos/entregas y comprobantes PDF internos tienen páginas propias conectadas a los servicios. [Alcance, recuperación de operaciones y comprobaciones de DEV-19](docs/DEV-19-UI-MVP.md).

## Altas con código automático y cierre

Clientes, productos, proveedores y cajas reciben código al guardar; las cuentas del personal muestran código de empleado. Requiere la migración **014**. Los formularios conservan los códigos existentes y muestran los nuevos como consulta. Se corrigió el cierre para detener/liberar el host antes de apagar WPF; las pruebas incluyen procesos separados para acceso y ventana principal. [Detalles y comprobaciones](docs/DEV-20-Codigos-Cierre.md).

Las ocho composiciones de `Imagenes/` guían el refinamiento compartido y las distribuciones de POS/cobro, caja y crédito: [DEV-23 · Referencias visuales](docs/DEV-23-UI-Referencias-Imagenes.md).

El refinamiento específico de Clientes reorganiza su listado, ficha y ventanas de edición según la referencia: [DEV-24 · Clientes](docs/DEV-24-Clientes-Refinamiento.md).

Productos e Inventario continúan ese patrón en el catálogo, categorías, existencias, lotes, Kardex, movimientos y conteos físicos: [DEV-25 · Productos e Inventario](docs/DEV-25-Productos-Inventario-Refinamiento.md).

Ventas, Caja, Crédito y Pedidos continúan con listados y fichas separados, ventanas de operación y confirmación de venta conectada al comprobante: [DEV-26 · Refinamiento comercial](docs/DEV-26-Ventas-Caja-Refinamiento.md).

El acceso y Ajustes usan la marca original de GoDental y las referencias visuales del proyecto; Preparación deja de aparecer en el menú. El comprobante incluye el logo, sin SKU ni hora: [DEV-28 · Marca, acceso y ajustes](docs/DEV-28-Marca-Acceso-Ajustes.md).

Proveedores actualiza listado, ficha, contactos y productos relacionados; la revisión también refina Usuarios y sus ventanas de administración: [DEV-29 · Proveedores y revisión de UI](docs/DEV-29-Proveedores-Usuarios-Refinamiento.md).

## Recuperación de operaciones · segunda etapa

[DEV-31](docs/DEV-31-Recuperacion-Operaciones.md) implementa el primer bloque de la segunda etapa: diario local antes de enviar, consulta al reiniciar y cierre definitivo bajo el mismo bloqueo SQL que la escritura. **Operaciones pendientes** permite recuperar un resultado o resolver y liberar sin volver a cobrar automáticamente. Requiere **015** además de las migraciones anteriores. El diario no guarda contraseñas ni el contenido comercial; tras reiniciar se consulta/cierra la solicitud y se revisa el módulo antes de una nueva captura. No se han aplicado migraciones en la PC del usuario desde este entorno.
