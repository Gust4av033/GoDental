# GoDental

Sistema de gestión de un depósito de insumos odontológicos en El Salvador.
Base del proyecto según **DM-01 v0.2** y **DB-01 v0.1** del 6 de septiembre de 2026.

## Estado

Repositorio inicial de documentación y arquitectura C#. Los proyectos establecen las dependencias entre capas; todavía no implementan ventas, login, persistencia ni una interfaz operativa.
DB-01 continúa como borrador técnico: no se incluyen scripts SQL ejecutables ni una base de producción.

## Tecnología definida

C# · .NET 8 · WPF/MVVM · SQL Server central · Dapper · QuestPDF · Microsoft.Extensions.DependencyInjection.
Las bibliotecas externas se incorporarán con versiones revisadas al implementar cada módulo; este esqueleto todavía no las instala.

## Documentación

- [Documento Maestro DM-01](docs/DM-01.md)
- [Modelo de datos DB-01](docs/DB-01.md)
- [Arquitectura y dependencias](docs/ARQUITECTURA.md)
- [Roadmap y pendientes](docs/ROADMAP.md)
- [Consolidación de repositorios anteriores](docs/CONSOLIDACION.md)
- [Documentos originales y hashes](docs/referencias/)

Las transcripciones Markdown facilitan la lectura en GitHub; los documentos originales conservan las tablas y el formato normativo.

## Estructura

```text
GoDental.sln
src/
  GoDental.Domain/
  GoDental.Application/
  GoDental.Infrastructure/
  GoDental.Wpf/
database/           # plan de evolución SQL, pendiente de revisión de DB-01
docs/               # documentación vigente y referencias originales
```

GoDental.Worker está reservado para una fase futura y aún no se crea.

## Desarrollo

En Windows, instalar el SDK de .NET 8 y las herramientas de escritorio .NET/WPF.

```powershell
dotnet restore GoDental.sln
dotnet build GoDental.sln --configuration Release --no-restore
```

Wpf es por ahora una biblioteca de presentación, sin ventana de inicio. Se convertirá en aplicación ejecutable durante la fase 2, junto con MVVM, DI, navegación, temas y login.
No se necesita una conexión SQL para compilar esta base.

## Reglas esenciales

- View → ViewModel → Application → Repository → SQL Server; ninguna View ejecuta SQL.
- Una venta completa es atómica y no permite stock negativo ni correlativos duplicados.
- Inventario trazable y promedio ponderado móvil con costo puesto en bodega.
- Permisos granulares y auditoría; el administrador también puede vender.
- Venta y documento fiscal separados; DTE y Online son integraciones futuras.
- Backups diarios `.bak`; nunca sincronizar MDF/LDF activos con OneDrive.
- Ramas por cambio y PR para integrar cambios relevantes a `main`.

No agregar credenciales, datos reales de clientes, backups ni archivos de producción.
