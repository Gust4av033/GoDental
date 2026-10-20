# Instrucciones de GoDental

- Responder y documentar decisiones del proyecto en español. Lenguaje principal: C#; base de datos: SQL Server.
- Antes de diseñar o implementar un módulo, consultar DM-01, DB-01 y ARQ-01, más el documento funcional correspondiente bajo `docs/`.
- ARQ-01 manda la ubicación del código: Domain → Application → Infrastructure/WPF según responsabilidades; no romper dependencias silenciosamente.
- Mantener .NET 8, WPF/MVVM, Dapper, QuestPDF y Microsoft.Extensions DI/Host salvo decisión arquitectónica versionada.
- Domain no depende de persistencia/UI. Application no depende de WPF ni implementaciones SQL. Infrastructure no depende de Views/ViewModels. WPF no ejecuta SQL.
- No usar Service Locator, `GenericRepository<T>`, estado global mutable para concurrencia, `.Result`, `.Wait()` ni `Thread.Sleep` en UI.
- No inventar tablas, procedimientos, reglas fiscales, proveedores externos o decisiones marcadas como pendientes. Si falta una definición, documentarla antes de implementarla.
- No incluir secretos, credenciales, backups, datos reales de clientes, PAN/CVV, certificados ni configuración local sensible.
- Operaciones críticas deben contemplar transacción, concurrencia multi-PC, permisos y auditoría según SEC-01.
- Toda View nueva debe respetar UX-01: recursos compartidos, Light/Dark, estados loading/empty/error, accesibilidad y 1366×768.
- Mantener `feature/desarrollo` como rama activa para lógica y UI; separar responsabilidades por capas y commits. `main` conserva la versión integrada y validada. Las ramas `feature/ui` y `feature/logica` quedan como referencias históricas; no continuar trabajo nuevo en ellas. Ver `docs/DEV-08-Ramas.md`.
- Antes de integrar: `dotnet build GoDental.sln -c Release` y `dotnet test GoDental.sln -c Release`.
