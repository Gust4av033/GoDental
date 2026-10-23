# DEV-39 — Paréntesis: captura y localización de datos

**Fecha:** 23/10/2026 · **Estado:** implementado; validación Windows/SQL en curso.

Se atiende después de 2.2A (DEV-38, CI #142) y antes de continuar 2.2B. Fuentes:
DM-01/DB-01/ARQ-01, DEV-10/13/24/29, UX-01 y solicitud del usuario del 23/10/2026.

## Alcance y decisiones

- País de proveedor en selector en español; país histórico desconocido se conserva al editar.
- Teléfono/WhatsApp admiten número nacional **sin prefijo** o internacional con `+`.
  Región del proveedor según país; clientes nacionales SV. Un prefijo explícito identifica la región
  del número sin cambiar el país comercial. No se agrega un prefijo que el usuario no eligió.
  Se valida estructura/longitud posible, no existencia/propiedad de la línea. No SMS ni llamadas.
- DUI: 9 dígitos, presentación `00000000-0`. NIT: 14 dígitos, `0000-000000-000-0`;
  se admite también DUI como NIT (selector de formato), conforme a Hacienda. Opcionales cuando
  no apliquen; sin verificaciones tributarias externas ni pretensión de certificación fiscal.
  Captura/pegado con límites, formato consistente y validación también al guardar.
- Confirmado por el usuario: **14 departamentos, 44 municipios actuales y distrito opcional**.
  Selector dependiente departamento → municipio → distrito, con opción sin especificar.
  No se reasigna automáticamente una dirección antigua a otro municipio. Valores históricos
  fuera del catálogo se conservan si no se cambian; cambios territoriales deben pertenecer al catálogo.
- Contacto del cliente: seleccionar/reutilizar contacto registrado o datos generales, y opción
  nuevo/otro. Editar el existente conserva su ID; crear otro no sobrescribe el anterior.
- Categorías: crear directamente en Administrar categorías reutilizando el alta existente de
  ProductService y su autorización/auditoría/duplicados; editar y desactivar conservan sus reglas.
- No bloquear edición de un nombre o referencia por un identificador/teléfono histórico inválido
  que permanezca **sin cambios**. Un dato nuevo/modificado debe cumplir el formato.

## Diseño por capas y migración 020

Reglas de formato/localización en Domain/Application; metadatos telefónicos de
`libphonenumber-csharp` (Apache-2.0), sin servicio externo. Países en español embebidos
(i18n-iso-countries, MIT); catálogo territorial embebido y versionado, sin consultas de red al usar la app.
Controles de captura y selección en WPF, validación de guardado en Application y comparación
con históricos dentro de la transacción SQL/rowversion existente. No SQL en las vistas.

020 agrega **Distrito NVARCHAR(100) NULL** a `cli.ClienteDirecciones` y `ven.PedidosMvp`:
el pedido copia el distrito de la dirección guardada como snapshot, para conservarlo aunque
se edite luego el cliente. No convierte ni rellena históricos, no crea tablas/procedimientos;
se mantienen 76 tablas, 8 vistas y 18 procedimientos. Mantener sin cambios la forma JSON
original de PlaceOrder y otras solicitudes recuperables; se agrega información al snapshot,
no al contrato anterior de solicitud. Actualizar todas las estaciones después de 020.

## Fuentes y límites

- [Hacienda: NIT persona natural](https://www.mh.gob.sv/servicios/inscripcion-modificacion-o-reposicion-de-nit-persona-natural/), actualizado 15/01/2026;
  DUI como NIT para personas naturales salvadoreñas mayores de edad; NIT de 14 dígitos se mantiene cuando corresponde.
- [Ley especial de reestructuración municipal](https://transparencia.mh.gob.sv/downloads/pdf/700-DGCG-LY-2024-001.pdf)
  y [Asamblea: distribución](https://www.asamblea.gob.sv/node/12806).
- Los nombres del catálogo operativo no son códigos CAT-012/CAT-013 ni una homologación DTE.
  La integración fiscal permanece pendiente; no se inventan códigos fiscales a partir del orden del JSON.
- El respaldo de datos y archivos, instalador y migrador de producción continúan en DEV-36.

### Catálogos y licencias embebidos

- Países españoles: [i18n-iso-countries](https://github.com/michaelwittig/node-i18n-iso-countries/blob/55c3a72603faf0185661e80841074e1c2fcf8db5/langs/es.json),
  revisión `55c3a72603faf0185661e80841074e1c2fcf8db5`; 250 países/territorios, nombres y alias.
  [Licencia MIT](licenses/i18n-iso-countries-MIT.txt), sin consultas de red durante captura.
- División territorial: hechos geográficos tomados de [listado de departamentos, municipios y distritos](https://github.com/juanmedina100/departamentos-distritos-municipios-el-salvador/blob/4daa18c039940d41504064570a7c6f853c1d7153/departamentos-distritos-municipios-sv.json),
  revisión `4daa18c039940d41504064570a7c6f853c1d7153`, transformados al formato propio y contrastados con
  la Ley, [Censo BCR 2024/2025](https://censo2024.bcr.gob.sv/wp-content/uploads/tablas-geoportal/2025/Aplicacion_del_Grado_de_Urbanizacion_en_El%20Salvador.pdf)
  y [Cuscatlán Norte](https://cuscatlannorte.gob.sv/). Se corrigen San Bartolomé **Perulapía**, San Francisco **Menéndez**,
  **Bolívar** y distrito **San José** (La Unión Norte); no se reutiliza código del proyecto fuente.
- Teléfonos: NuGet **libphonenumber-csharp 9.0.41**, revisión fuente `e7bba34432f7f4cee7c8f34b9912a1f9369fc0d8`,
  [licencia Apache-2.0](licenses/libphonenumber-csharp-Apache-2.0.txt). Sólo se evalúa longitud posible/estructura;
  un número posible puede no estar asignado. Catálogos versionados, no actualizados silenciosamente.

## Actualización y uso

Cerrar ambas estaciones; aplicar `database/020_Distritos_Formularios.sql` después de 019
sobre la instancia y base **guardadas en Ajustes**. Respaldar antes y actualizar ambas aplicaciones.
Para una base de desarrollo marcada se puede usar la herramienta existente, que ejecuta 001–020
con guardas; no usarla para forzar una instalación sobre una base productiva:

```powershell
git switch feature/desarrollo
git pull --ff-only origin feature/desarrollo
$servidor = Read-Host 'Instancia SQL guardada en Ajustes'
dotnet run --project tests/GoDental.DatabaseTests -c Release -- --install-development --server "$servidor" .
dotnet run --project src/GoDental.Wpf -c Release
```

Para una base existente fuera de esa herramienta, ejecutar sólo las migraciones faltantes en orden
mediante `sqlcmd -S <instancia> -d <base> -E -b -i database/020_Distritos_Formularios.sql` (sustituir marcadores).
020 no conecta ni instala automáticamente en las laptops desde el entorno cloud.

- Proveedores → ficha: país en lista y número nacional o `+` internacional en Contacto.
- Clientes → ficha: Datos fiscales con selector NIT 14 / DUI como NIT 9; guiones al capturar/pegar.
- Clientes → Direcciones: departamento, municipio actual y distrito opcional; valores históricos se ven y conservan.
- Clientes → Contactos: cargar registrado mantiene su ID; datos generales y Otro crean un nuevo contacto.
- Productos → Administrar categorías → Nueva categoría; se crea activa y conserva permisos/auditoría existentes.

## Secuencia

Este complemento no reemplaza 2.2A y no abre un módulo nuevo. Después continúa **2.2B: cobranza y
documentos de crédito** (DEV-30). Instalador/actualizador productivo y respaldo automatizado siguen
pendientes en DEV-36; no se declara la etapa 2 terminada.
