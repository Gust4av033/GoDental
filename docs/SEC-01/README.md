# SEC-01 — Seguridad, Roles, Permisos, Auditoría y Sesiones

**Versión:** v0.1

Ampliación de diseño DTE (PREPARADO, sin cierre S1): [seguridad fiscal](../FIS-01/Seguridad-Fiscal.md),
[servidor](../FIS-01/Arquitectura-y-Servidor.md) y [secretos](../FIS-01/Secretos-y-Certificados.md).
Identidad desde contexto autenticado, permiso actual del servidor y SQL mínimo; IdUsuario del
cliente no prueba identidad. Sólo se añaden permisos de emisión/eventos/reintento al existir
su funcionalidad real. Los tres permisos fiscales actuales conservan alcance interno.

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

## Política inicial del servidor · DEV-51

Sesión opaca de 256 bits: máximo ocho horas y veinte minutos sin actividad; una activa por
usuario/estación. Se revoca al salir, al nuevo acceso del mismo par y al cambiar/desactivar/
bloquear la credencial o estación. Estado/permisos consultados en SQL por petición. Cambio
obligatorio de contraseña impide emitir token; se completa por WPF hasta implementar su
recorrido remoto. Alta de estaciones no disponible por HTTP. Esta política no modifica aún la
sesión del escritorio; SEC-02 de WPF permanece abierto. Ver [DEV-51](../DEV-51-Servidor-Identidad.md).


## Revisión de implementación y cierre de seguridad

[DEV-45](../DEV-45-Seguridad-Hallazgos.md), 23/10/2026, registra revisión limitada del código,
evidencia y controles pendientes por fase. El acceso directo a SQL no se considera por sí mismo
una frontera confiable de identidad: cerrar mínimo privilegio/impersonación antes de conectores
y producción, junto con custodia de secretos fiscales. Expiración, ACL, respaldo/restauración y
firma de distribución se verifican antes de instalador/piloto. Riesgos altos/críticos se corrigen
antes de habilitar su función. Auditoría NuGet en CI no sustituye la revisión del código/despliegue.

Base B3: [DEV-53](../DEV-53-Custodia-Secretos.md) define custodia privada Windows para candidatos
opacos, ligada a SID/emisor/ambiente/propósito/versión. No otorga permisos fiscales nuevos ni
habilita importación/activación: el caso sensible y su reautenticación/auditoría siguen pendientes.
