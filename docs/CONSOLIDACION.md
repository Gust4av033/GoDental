# Consolidación de repositorios

GoDental reúne la documentación vigente y la arquitectura de DM-01 v0.2. Los desarrollos anteriores quedan como referencias históricas independientes; su código no se ha copiado a esta solución.

| Repositorio anterior | Commit revisado | Hallazgo |
| --- | --- | --- |
| [GoDentalAPP](https://github.com/Gust4av033/GoDentalAPP) | 44fada9c5684d26522210a9586ce1e324123a324 | WPF .NET 8, Entity Framework Core, carpetas de capas dentro de un proyecto; prototipos de clientes, insumos y facturación |
| [DepositoDental](https://github.com/Gust4av033/DepositoDental) | 0511f04d7c3dc381a0b32c3d234c4af129167df0 | WPF .NET 8, Entity Framework Core, entidades, autenticación y temas dentro de un proyecto |

Ambos usan Entity Framework, mientras que DM-01 define Dapper. La revisión cubrió estructura, proyectos, historial reciente y metadatos; no certifica la funcionalidad ni la seguridad de todo el código antiguo.

La incidencia [GoDentalAPP #1 — Problemas](https://github.com/Gust4av033/GoDentalAPP/issues/1) reporta fallos del módulo de facturas y de BD. Se conserva como antecedente; la nueva solución aún no implementa ese módulo y no afirma resolver el fallo.

Se obtuvieron copias mirror completas de los dos repositorios y se verificaron con `git fsck --full` sin errores. Se mantienen fuera del nuevo repositorio para no incorporar configuraciones e historial legado.

La limpieza prevista consiste en archivar ambos repositorios después de publicar GoDental y verificar su contenido. El archivado preserva código, commits e incidencias; no se elimina ningún repositorio. El estado de ejecución se informa al cerrar la tarea.

Si se reutiliza código posteriormente, revisar compatibilidad con DM-01/DB-01 y conservar los avisos Apache-2.0 de los originales cuando corresponda. No se ha asignado una licencia nueva al proyecto.
