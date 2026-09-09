# Seguridad

GoDental maneja información comercial, clientes, credenciales de aplicación y en el futuro datos fiscales e integraciones externas.

## No versionar

- Connection strings reales.
- Contraseñas o hashes exportados.
- Tokens, certificados, llaves DTE o credenciales de proveedores.
- PAN/CVV u otros datos completos de tarjeta.
- Backups `.bak`, MDF/LDF, logs o exportaciones con datos reales.

## Principios

- Mínimo privilegio y denegación por defecto.
- La UI no reemplaza la autorización de Application.
- Toda operación sensible debe ser trazable cuando corresponda.
- SQL Server no se expone directamente a Internet.
- La cuenta SQL de la aplicación no debe usar `sa` ni `db_owner` en operación normal.

Los detalles completos se encuentran en `docs/SEC-01/`.
