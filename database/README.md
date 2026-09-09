# Base de datos

La arquitectura aprobada define una secuencia SQL 001–008. Este repositorio conserva desde el inicio los nombres y el orden para evitar que cada módulo invente su propia estructura.

> **Importante:** los archivos 001–008 presentes aquí son marcadores y todavía no contienen el SQL aprobado. No deben ejecutarse como migraciones reales hasta sustituir su contenido por los scripts SQL v0.1 formalmente validados contra DB-01.

Orden:

1. `001_Core.sql`
2. `002_Inventario.sql`
3. `003_Compras.sql`
4. `004_ClientesVentas.sql`
5. `005_Fiscal_Base.sql`
6. `006_Comercial_Base.sql`
7. `007_Seed.sql`
8. `008_ViewsProcedures.sql`

Una vez un script haya sido ejecutado en producción, no se reescribe como si nunca hubiera ocurrido; los cambios posteriores deben tener estrategia de migración/versionado.
