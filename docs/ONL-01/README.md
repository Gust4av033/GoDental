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

## Continuación priorizada · 23/10/2026

El usuario prioriza módulos nuevos antes de escalas/listas/promociones. Después de la preparación
Fiscal interna de [DEV-44](../DEV-44-Fiscal-Interno.md), continuar Online interno; este módulo
no se declara implementado todavía. Cerrar fuentes/consentimiento y reglas de conversión antes
de acciones externas. Los controles S1 de [DEV-45](../DEV-45-Seguridad-Hallazgos.md) son previos
a conectores; no incorporar envíos ni descubrimiento externo con proveedores inventados.
