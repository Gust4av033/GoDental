# Fuentes aportadas y contratos oficiales pendientes

23/10/2026. Inventario exacto en [fuentes-oficiales.json](fuentes-oficiales.json): ruta,
ruta original, commit de incorporación, blob Git, tamaño, SHA-256, páginas y edición impresa.
Los ocho archivos llegaron a `main` en `bf2d4b9`; se incorporaron a `feature/desarrollo` antes
de diseñar esta revisión. La carpeta se normalizó de `DTEPDF.` a `DTEPDF` para permitir
checkout en Windows. **Los ocho PDF conservan exactamente sus bytes.**

Procedencia: material aportado al repositorio y atribuido a Hacienda por el usuario. Ediciones
leídas dentro de cada PDF; autenticidad de descarga y vigencia actual sin verificar en línea.
La fecha de metadatos PDF no sustituye edición impresa ni fecha de publicación. No se inventó
URL original por archivo. Las referencias usan **página ordinal del PDF**, incluida portada;
en varios manuales difiere de la página impresa.

## Inventario normativo y administrativo

| ID | Título exacto (sin extensión) | Edición impresa / fecha | Páginas | Uso y límites |
| --- | --- | --- | --- | --- |
| F01 | Aviso mandatario referente a ventas con factura | Sin versión / 05/09/2024, p.1 | 1 | Consumidor final e identificación; contrastar con F08/manual funcional/schema |
| F02 | Guía del Proceso de Incorporación para ser Emisor de Documentos Tributarios Electrónicos | 1.3 / 12/2025, control p.2 | 8 | Transmisión frente a portal de Facturación, incorporación y pruebas, pp.6–8 |
| F03 | Manual de Usuario de Consulta Pública de los DTE y Eventos | 1.1 / 05/2026, control p.2 | 17 | Consulta humana/estados; no contrato de reconciliación API |
| F04 | Manual de Usuario del Sitio de Emisores DTE 1.4 | 1.4 / 10/2026, control p.2 | 19 | Gestión de emisor/establecimientos y usuario API, pp.18–19 |
| F05 | Manual de acreditamiento y obtención de certificado de firma electrónica | 1.1 / 05/2026, control p.2 | 25 | Credenciales/certificados por ambiente, pp.7–14; no adaptador de firma |
| F06 | Manual de usuario para la Solicitud de Ingreso al ambiente para pruebas | 2.1 / 09/2026, control p.2 | 14 | p.8 remite al anexo 1 del Manual Funcional para pruebas de Transmisión |
| F07 | Manual para la obtención  de la Autorización como Emisor de DTE | 1.0 / 05/2026, control p.2 | 13 | p.7 pruebas por tipo, invalidación/contingencia; p.11 autorización |
| F08 | Normativa de Cumplimiento de los Documentos Tributarios Electrónicos_Versión 2.0 | 2.0 / 25/05/2026, portada p.1 | 137 | Normativa y anexos; no sustituye contratos técnicos vigentes |

La doble separación entre “obtención” y “de” en F07 forma parte del nombre original.
Se estudió el contexto de incorporación, pruebas, certificado, autorización y consulta, junto
con las reglas y anexos aplicables de F08. Varios PDF tienen extracción de texto parcial;
se usó OCR para localizar pasajes y se contrastaron visualmente reglas sensibles (p.16 de
F08, p.7 de F07). El OCR temporal no reemplaza al PDF ni se incorpora como fuente.

Hallazgos que condicionan el diseño, todavía sin implementar contrato:

- F08 §7.1.1–7.1.2, pp.15–16: identidad fiscal distinta de la comercial. Numeración
  consecutiva centralizada o por establecimiento; por punto de venta/tipo sólo con autorización
  expresa. No diseñar una secuencia automática independiente por FE/CCFE/caja.
- F08 §7.2–7.3, p.16: hasta ocho decimales en cuerpo, dos en resumen, redondeos descritos
  y holgura de una centésima. Reutilizar un motor de dos decimales no basta.
- F07 p.7: Sistema de Transmisión contempla pruebas de invalidación **y contingencia**
  además de las de los tipos solicitados. Cantidades/casos concretos: Manual Funcional vigente.
- F08 §10.1, p.22: transmitido satisfactoriamente y condiciones, rechazado, invalidado,
  transitorio. No demuestra los literales de una API. `OBSERVADO` interno no es observación MH.
- F08 §11.3–12.2, pp.25–27: PDF no es DTE, fidelidad, advertencia de ensayos, conservación
  y protección de datos. F03 describe consulta humana, no recuperación técnica de incertidumbre.

Ante contradicción registrar ambas fuentes y verificar edición oficial vigente; no mezclar
versiones de campos/catálogos ni escoger el ejemplo más cómodo.

## Paquetes que faltan antes de contratos externos

**PENDIENTE_DE_DOCUMENTACION_OFICIAL / PENDIENTE_DE_CONTRATO_OFICIAL.**
El dominio permitido por el plan es `factura.gob.sv`. La política de red del entorno no permite
ese dominio. No se sortea mediante mirrors, blogs o CI. La fase externa se detiene aquí;
la especificación y el diseño de seguridad pueden avanzar.

| ID | Obtener del sitio oficial | Cierra estas decisiones |
| --- | --- | --- |
| T01 | Manual Tecnológico para la Integración del Sistema de Transmisión y anexos vigentes | Autenticación, hosts/ambientes, recepción/consulta, respuestas, errores, límites y reconciliación |
| T02 | Manual Funcional del Sistema de Transmisión y anexo de pruebas mínimas | FE/CCFE, receptor, impuestos, fechas, eventos, casos/cantidades de certificación |
| T03 | Catálogos de Facturación Electrónica vigentes | Códigos/versiones/vigencia y compatibilidad por schema |
| T04 | JSON Schemas FE, CCFE y eventos requeridos, con dependencias | Nombres/tipos/condiciones/precisión, versiones y validación local |
| T05 | DTE – Firmador, distribución/documentación oficial y términos | Instalación, versión/hash, compatibilidad, licencia y seguridad |
| T06 | SVFE-API-Firmador y documentación asociada | Entrada/salida, formato real de certificado, errores e interoperabilidad |

Incorporación manual cuando no hay acceso desde Codex:

1. Descargar las versiones vigentes **desde `factura.gob.sv`** en un equipo con acceso.
   Conservar nombre original, URL exacta, fecha de obtención y versión visible.
2. Aportar sólo documentación/paquetes públicos; nunca certificado personal, contraseña,
   token ni exportación autenticada. Revisar términos antes de versionar binarios.
3. Incorporar fuentes aprobadas bajo `DTEPDF/tecnicos/` o schemas en una carpeta versionada
   de contratos a definir en fase C. Registrar dependencias y SHA-256.
4. Actualizar inventario y matriz **tras leerlos**; no basta crear archivos vacíos o marcar
   un booleano. Verificar origen y compatibilidad de versiones FE/CCFE/eventos.
5. Resolver filas pendientes; cerrar diseño final y sólo entonces implementar contratos.
   Un inventario con hashes no autoriza emisión ni producción.

## Registro de contratos externos

| Contrato | Fuente que falta | Estado |
| --- | --- | --- |
| Autenticación MH, token y renovación | T01 | PENDIENTE_DE_CONTRATO_OFICIAL |
| Recepción/consulta/reconciliación TEST/PROD | T01/T02 | PENDIENTE_DE_CONTRATO_OFICIAL |
| FE/CCFE/eventos, campos/schema/catálogos | T02/T03/T04 | PENDIENTE_DE_CONTRATO_OFICIAL |
| Firma y formato de certificado | T05/T06 | PENDIENTE_DE_CONTRATO_OFICIAL |
| QR/enlace de consulta fiscal y archivo entregable | F08/F03 contextuales; T01–T04 pendientes | PENDIENTE_DE_CONTRATO_OFICIAL |

No hay endpoints, DTO de Hacienda, algoritmo propio ni URLs de QR aprobados. Rutas y entidades
en los otros documentos son conceptos **internos GoDental**, no campos del JSON oficial.
Cada revisión de contrato necesita diff, fuente, fecha, pruebas y decisión de activación;
conservar el contrato usado por documentos históricos.

## Comprobación reproducible del material

Con Python 3.9 o posterior, desde la raíz del repositorio:

```powershell
python -m unittest discover -s tests/tooling -v
python scripts/verify-fiscal-sources.py --report TestResults/fiscal-sources.json
```

CI ejecuta ambas comprobaciones antes de compilar. Se esperan ocho fuentes intactas y un
aviso de T01–T06 pendientes. `--require-contracts` devuelve código 2 mientras falten paquetes;
es un diagnóstico documental, **no** una autorización de emisión ni preflight productivo.
