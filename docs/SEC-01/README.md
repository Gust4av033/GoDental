# SEC-01 — Seguridad, Roles, Permisos, Auditoría y Sesiones

**Versión:** v0.1

GoDental será multiusuario y multi-PC con roles iniciales Administrador/Vendedor y permisos granulares. La UI mejora la experiencia, pero la autorización real se valida también en Application/Service y la integridad final en SQL Server.

## Reglas vigentes

- Mínimo privilegio y denegación por defecto.
- Contraseñas con hash lento + salt; nunca texto plano ni SHA/MD5 simples.
- Bloqueo inicial sugerido: 5 intentos fallidos → 15 minutos, parametrizable.
- Sesión incluye usuario, roles/permisos, equipo y fecha de inicio. Las operaciones relevantes asocian usuario + estación.
- Infrastructure puede establecer `SESSION_CONTEXT` de SQL Server como contexto complementario.
- Descuento >7% y ≤35% requiere justificación; >35% requiere autorización administrativa puntual. Devoluciones y ajustes sensibles requieren permisos/evidencia según módulo.
- Bitácora de negocio y logging técnico son distintos; ninguno contiene contraseñas, tokens, PAN/CVV o secretos.
- Connection strings, tokens, certificados y credenciales externas nunca se versionan.
- Cuenta SQL de aplicación con mínimo privilegio; no `sa`, no `db_owner` en operación normal y SQL Server no se expone a Internet.
- Backups `.bak` terminados, protegidos y con pruebas periódicas de restauración.

Las políticas definitivas de expiración, sesiones simultáneas, retención de logs y almacenamiento seguro de secretos se cierran antes de producción.
