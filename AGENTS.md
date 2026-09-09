# Instrucciones de GoDental

- Responder en español. Lenguaje principal C# y base SQL Server.
- Consultar docs/DM-01.md, docs/DB-01.md y sus originales antes de diseñar módulos.
- Conservar las referencias originales; decisiones nuevas se documentan con estado y versión.
- Mantener .NET 8, WPF/MVVM, Dapper, QuestPDF y DI definidos en DM-01; no sustituir tecnologías silenciosamente.
- Domain no depende de WPF ni persistencia. La UI no ejecuta SQL.
- No inventar aprobación de DB-01 ni reglas pendientes.
- No incluir secretos, respaldos o datos reales de clientes.
- Probar atomicidad y concurrencia cuando se implementen operaciones críticas.
- main debe compilar; usar ramas y PR para cambios relevantes.
