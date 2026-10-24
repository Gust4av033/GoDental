# DEV-34 · Códigos de barras y formatos de impresión

Complemento autorizado después de 2.1A. El usuario aún no tiene impresora: las medidas son configurables, sin fijar marca ni controlador. La secuencia principal continúa en **2.1B · importaciones y prorrateo**; esta entrega no cierra esa fase.

## Alcance implementado

- Productos → ficha → Códigos de barras: selección de un código activo, barras reales, etiquetas PDF, vista previa e impresión. El mantenimiento y la búsqueda POS existentes conservan su comportamiento.
- CODE128 interno (`GD` + Id de diez dígitos), CODE128 externo, EAN13 y UPC-A con sus dígitos de control. OTRO no identifica una simbología y se rechaza para impresión; no se convierte silenciosamente.
- Presets de etiqueta **50×30 y 40×30 mm**, ancho/alto propios, 1–200 copias, página individual o distribución en Carta/A4. En hoja, columnas/filas se calculan por las medidas, margen y separación; se continúa en páginas adicionales, sin estirar etiquetas para llenar la hoja. Nombre limitado a dos líneas y SKU a una; el valor codificado se conserva completo.
- Ventas → detalle: comprobante interno de venta o nota interna de entrega. Presets **Carta, A4, ticket 80 mm y ticket 58 mm**; ancho, alto y margen editables en mm. Los tickets usan diseño vertical propio y paginación según el alto indicado (250 mm inicial); no se reduce el diseño Carta.
- Nota de entrega: requiere venta confirmada con pedido asociado, usa contacto/teléfono/dirección/referencia/fecha/responsable y productos/cantidades del snapshot existente, omite precios y pagos y deja constancia de recepción física. No es guía oficial de courier ni documento fiscal.
- Guardar PDF e Imprimir usan exactamente el documento previsualizado. Cambiar medidas, copias, papel o tipo requiere actualizar la vista previa; las acciones quedan bloqueadas mientras no coincida.
- Preferencias locales en `%LOCALAPPDATA%\GoDental\printing.json`, separadas de conexión y sin datos de negocio/credenciales. Conservan medidas/papel, no impresora fija; las copias de etiquetas vuelven a 1 al cargar. Un error al leer preferencias no impide consultar la venta o mantener sus códigos; se informa y se permiten medidas en pantalla.

No se crean tablas, procedimientos ni una migración 018. La base continúa en **017**.

## Cómo usarlo

1. Abre **Productos → ficha → Códigos de barras** y selecciona un código activo. Si no hay uno, agrégalo/genera el interno con el mantenimiento existente.
2. Elige el tamaño de etiqueta o escribe ancho y alto. Para etiquetas sueltas/rollo usa **Etiqueta**; para hoja convencional usa **Carta/A4**, ajusta margen y separación e indica copias.
3. Pulsa **Ver etiquetas**. Se muestran las barras y todas las páginas de la solicitud. Si el código necesita más ancho, el mensaje indica el mínimo: aumenta el ancho o cambia de etiqueta.
4. Guarda el PDF o imprime. Usa tamaño real **100%**, papel con las medidas del documento y un controlador que lo admita. El zoom en pantalla sólo cambia la vista, no el PDF ni la impresión.
5. En **Ventas → detalle**, configura **Formato del documento**, elige **Venta/Entrega** y pulsa **Previsualizar**. Puedes modificar el formato desde la vista previa y pulsar **Actualizar vista**. Entrega requiere pedido asociado; el comprobante sigue siendo interno, sin emisión fiscal/DTE.

Para actualizar desde PowerShell, en la raíz del repositorio y con la aplicación cerrada:

```powershell
git fetch origin
git switch feature/desarrollo
git pull --ff-only origin feature/desarrollo
dotnet run --project src/GoDental.Wpf -c Release
```

Este complemento no necesita ejecutar el instalador SQL. Si el equipo sigue antes de 017, aplica primero la actualización descrita en DEV-33 a la instancia realmente guardada en Ajustes.

## Diseño y límites técnicos

Application define `PrintLayout`, `LabelPrintOptions`, los contratos y `BarcodeLabelService`. La generación de etiquetas vuelve a leer los códigos del producto, exige `INVENTARIO.VER`, código activo y la misma revisión de sesión antes/después de renderizar; no escribe datos ni genera otra operación de inventario. Los comprobantes conservan venta confirmada, `VENTAS.VER` + `VENTAS.REIMPRIMIR`, revisión de sesión y selección vigentes. WPF no ejecuta SQL.

Infrastructure usa **ZXing.Net 0.16.11 (Apache-2.0)** para codificar y QuestPDF para SVG vectorial/PDF. Las barras son negras sobre blanco, con quiet zones de 11 módulos y ancho mínimo de módulo 0.254 mm para CODE128 / 0.33 mm para EAN/UPC. La etiqueta individual deja 1.5 mm internos antes de la zona libre; los códigos largos pueden requerir más de 50 mm. No se deforman módulos para hacerlos caber. Alto de etiqueta 28–150 mm, ancho 20–200 mm, margen de hoja 2–25 mm, separación 0–15 mm. Se limita el área rasterizada total para evitar solicitudes excesivas.

Carta/A4 y páginas personalizadas mantienen su tamaño en `ReceiptDocument`; el motor PDF redondea MediaBox al cuarto de punto (error máximo ≈0.045 mm). Las imágenes para vista/impresión usan 300 dpi. El documento físico WPF conserva las dimensiones al 100%, solicita el tamaño al controlador, valida el PrintTicket y compara papel/área imprimible. Si no coincide o recorta contenido se detiene con una indicación; no reduce el código ni envía una página recortada silenciosamente. Para etiquetas individuales se requiere que el controlador admita ese tamaño y sus márgenes; alternativamente, distribuir en Carta/A4.

La impresora y el lector físicos siguen pendientes del piloto: hay que comprobar escala real, márgenes del controlador, orientación/avance y lectura en el material elegido. Las pruebas digitales no sustituyen esa comprobación. No se configura courier, DTE ni una impresora inexistente.

## Validación

Las pruebas cubren decodificación de imágenes realmente renderizadas para CODE128 (40/50 mm), EAN13 y UPC; tamaños MediaBox PDF, copias/paginación en hoja, tickets 58/80, Carta/A4, notas de entrega con texto largo, rechazo de tamaños/códigos inválidos, permisos/estado activo, persistencia de preferencias, cambios de formato y limpieza por sesión. `StbImageSharp 2.30.16` (Unlicense o MIT) se usa sólo en tests para leer los píxeles antes de decodificar.

El smoke WPF verifica etiquetas y comprobantes en Light/Dark, vista compacta, enlaces, guardado/impresión del mismo documento, dimensiones físicas sin escalado y rechazo de papel incompatible. La ejecución y evidencia finales se registran al terminar la validación.

Resultado final sobre **`d88fde3`**, integrado con el panel de Inicio de DEV-35: [CI Windows #128 correcto](https://github.com/Gust4av033/GoDental/actions/runs/39692050957), Release sin errores/advertencias, **241 xUnit, 477 comprobaciones SQL y 725 WPF**, sin errores de binding. Compilación Release y test de solución también correctos en Linux con `EnableWindowsTargeting=true` (241). Se inspeccionaron localmente las imágenes reales de etiquetas 40/50 mm, distribución Carta y ticket de 58 mm; las capturas WPF de ambos temas quedan en los resultados de CI. No se probó hardware físico ni se modificó la BD personal.
