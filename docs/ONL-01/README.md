# ONL-01 — Prospección, Campañas y Conversión

**Versión:** v0.2 interna · conectores externos pendientes

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


## Primer bloque operativo · DEV-46

[DEV-46](../DEV-46-Online-Interno.md) conecta alta/edición manual de prospectos públicos,
fuentes/fechas, coincidencias, exclusiones centrales y vínculo con cliente existente mediante
compra confirmada (DM-01 §15.2). El usuario confirmó este recorrido; alta de cliente se mantiene
en Clientes, sin crédito automático. Conserva opt-out y origen; no reescribe operaciones comerciales.
Migración 025, sin tablas nuevas. Zonas, campañas/plantillas, scoring, atribución, consentimiento,
proveedores y descubrimiento/envío siguen pendientes de requisitos y ONL-01 v0.2.
La validación del incremento y siguiente bloque están en DEV-46; no equivale a Online completo.

## Ampliación conforme a referencia · DEV-47

[DEV-47](../DEV-47-Online-Referencia.md) define zonas, segmentos reutilizables, plantillas,
campañas preparadas/aprobadas y seguimiento manual. La referencia visual es 221200 de Imagenes.
Aprobación interna no permite envío ni acredita consentimiento. Sustituye el alcance manual
limitado de DEV-46 para esta continuación; proveedores, mapa y conectores siguen pendientes.
