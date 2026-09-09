# ONL-01 — Prospección, Campañas y Conversión

**Versión:** v0.1

El módulo Comercial/Online automatiza descubrimiento, normalización, deduplicación, scoring y preparación, pero mantiene control humano sobre comunicaciones comerciales.

## Reglas centrales

- Conservar fuente pública y fecha de obtención; priorizar datos comerciales públicos y minimizar datos personales.
- Deduplicar prospectos contra Prospectos y Clientes por teléfono, email, dominio, nombre+municipio y otros identificadores válidos.
- `ListaNoContactar` es central y se verifica antes de cada envío; opt-out debe respetarse incluso si el contacto vuelve a importarse.
- Campañas siguen estados controlados y requieren aprobación humana en v0.1.
- WhatsApp/Meta/Email se abstraen detrás de proveedores autorizados; no usar automatizaciones no oficiales ni credenciales personales embebidas.
- Comercial puede consultar stock/rotación/vencimiento para sugerir ofertas, pero no modifica Inventario ni envía promociones automáticamente.
- Conversión prospecto → cliente conserva la fuente/origen y evita duplicados.
- Un futuro `GoDental.Worker` puede ejecutar prospección periódica/preparación si WPF está cerrado, pero no enviar campañas sin cumplir la política de aprobación.

Proveedor WhatsApp/Meta, fuentes permitidas, consentimiento por canal, scoring y atribución quedan pendientes para v0.2.
