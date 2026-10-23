# GoDental

Prioridad del 23/10/2026: [DEV-44](docs/DEV-44-Fiscal-Interno.md), Fiscal interno y primer bloque de Online interno ([DEV-46](docs/DEV-46-Online-Interno.md)); después escalas/listas/promociones. [DEV-45](docs/DEV-45-Seguridad-Hallazgos.md) registra seguridad y fases de corrección. En la PC faltan migraciones **desde 021, incluida: 021 → 022 → 023 → 024 → 025**.

GoDental es una aplicación de escritorio para la operación comercial de un depósito dental: inventario, compras/importaciones, ventas/POS, caja, crédito, seguridad, fiscal y comercial/online.

## Estado actual y siguiente etapa

La revisión de [DEV-30](docs/DEV-30-Revision-Fase-2.md) y los incrementos DEV-31–44 distinguen
el alcance implementado de lo pendiente de DM-01. El MVP conecta inventario/conteos,
proveedores/clientes, POS/caja/crédito/pedidos y comprobantes; ya incluye compras/recepciones,
importaciones con costeo, posventa/cobranza, reparto contra entrega y reportes operativos.
Fiscal incorpora preparación/revisión interna; Online conecta prospectos/fuentes/exclusiones y vínculo por compra confirmada. Emisión DTE, campañas y conectores siguen pendientes.
El orden vigente y los controles de seguridad están en DEV-44/45.

Rama activa: **`feature/desarrollo`**. [CI Windows #171](https://github.com/Gust4av033/GoDental/actions/runs/39912832720) valida `e8cab1a`: Release sin advertencias/errores, **347 xUnit / 721 SQL / 965 WPF**, sin errores de binding. Auditoría NuGet: siete proyectos sin avisos conocidos.
La aceptación física entre dos estaciones, restauración real y despliegue productivo siguen pendientes.

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
016_Parametros_Comerciales.sql
017_Compras_Recepciones.sql
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

Validación de 2.0A: [CI Windows #106](https://github.com/Gust4av033/GoDental/actions/runs/39644476282), código `c3d3445`, Release sin advertencias/errores, **187 xUnit, 399 comprobaciones SQL y 575 WPF** correctas. La próxima entrega es 2.0B; los módulos nuevos y el piloto real siguen pendientes.

## Complementos comerciales del MVP

[DEV-32](docs/DEV-32-Complementos-MVP.md) conecta la pestaña **Ajustes → Comercial**, **Ficha del producto → Códigos de barras** y **Tarjeta** manual en cobros/abonos. Requiere 016 después de 015; actualizar todas las estaciones. Tarjeta registra un comprobante externo ya aprobado y exige referencia; no se conecta con un procesador ni afecta efectivo. Las etiquetas físicas, series y conciliación bancaria siguen pendientes. El fondo del logo acompaña el tema y la rotulación mantiene contraste en Dark.

## Compras, recepciones e importaciones · 2.1

[DEV-33](docs/DEV-33-Compras-Recepciones.md) conecta el menú Compras: OC en borrador, confirmación, recepción parcial/total, aplicación con costo completo, lotes, promedio y Kardex. Requiere 017 después de 016. Una respuesta perdida se recupera por el mismo identificador; no se crea otra recepción. [DEV-37](docs/DEV-37-Importaciones-Costeo.md) añade la pestaña Importación, eventos manuales, gastos y cierre de costos por valor neto; las recepciones parciales consumen el presupuesto congelado. Requiere 018 y actualizar todas las estaciones. [DEV-38](docs/DEV-38-Posventa.md) conecta 2.2A: devoluciones/anulaciones desde la venta, autorización administrativa, fotos en carpeta central, reingreso vendible al costo histórico y reintegro en efectivo después de reducir la deuda. Requiere 019; respalda BD y carpeta de evidencias y actualiza todas las estaciones. [DEV-39](docs/DEV-39-Formularios-Datos.md) agrega el paréntesis de formularios: teléfonos/países, formatos DUI/NIT, direcciones actuales con distrito, contactos y alta directa de categorías (020). [DEV-40](docs/DEV-40-Cobranza-Documentos.md) implementa 2.2B: avisos de cobranza, recibos de abono y estados de cuenta con posventa trazable (021). [DEV-34](docs/DEV-34-Impresion-Formatos.md) conecta barras gráficas, etiquetas con copias/medidas y distribución Carta/A4, tickets 58/80 mm y notas internas de entrega. Incluye vista previa, PDF e impresión a tamaño real y preferencias locales; no necesita migración nueva ni una impresora ya elegida.

Inicio incorpora un panel operativo con indicadores consultados por servicio, actividad reciente y accesos por permiso: [DEV-35 · Inicio](docs/DEV-35-Inicio-Panel-Operativo.md).

[DEV-41](docs/DEV-41-Reparto-ContraEntrega.md) implementa reparto contra entrega con custodia y rendición (022). [DEV-42](docs/DEV-42-Reportes-Operativos.md) conecta Reportes e Inicio: ventas/posventa, margen histórico, inventario/custodia, vencimientos y compras recibidas (023). La continuación vigente se detalla en DEV-43; el piloto y DEV-36 quedan después de los complementos y módulos nuevos.
