# DEV-27 · Comprobante interno para entrega y vista previa

Ampliación autorizada en `feature/desarrollo`: presentación tipo factura comercial, vista previa y salida a PDF/impresora. Se mantiene la separación Venta != DTE de FIS-01. No se generan identificadores fiscales ni se cambia la confirmación, el cobro o el inventario.

## Datos y contratos definidos antes de implementar

- `SaleSnapshot` incorpora `OrderData? Delivery` opcional. La lectura de venta/comprobante consulta el pedido vinculado por `IdVenta` en la misma transacción de lectura, sobre las tablas de 013 existentes. Dirección/contacto/referencias son el snapshot del pedido; no se sustituye con la ficha actual del cliente. Ventas sin pedido siguen funcionando. No requiere migración.
- El cliente procede del JSON histórico de la venta: nombre/comercial, identificación y contacto sólo cuando están presentes. JSON antiguo vacío o inválido usa un respaldo seguro; no se inventan datos. Encabezado GoDental por defecto; `Pdf:IssuerName/Address/Phone/Email` opcionales permiten datos reales del negocio, sin textos ficticios ni datos fiscales inventados.
- `IReceiptRenderer.Render` conserva la salida PDF. `RenderPreview` devuelve `ReceiptDocument(Pdf, Pages)`: bytes PDF y páginas de imagen de 300 DPI generados por QuestPDF desde el mismo documento y snapshot, con composición, márgenes y paginación comunes. No se reconstruye una factura en XAML ni se calculan precios/impuestos en WPF. Guardar desde la vista previa utiliza sus mismos bytes PDF.
- `SaleReceiptService.PreviewAsync` aplica los mismos permisos VER+REIMPRIMIR y control de sesión que ExportAsync; sólo admite venta confirmada. La generación se ejecuta fuera del hilo UI.
- `IPdfPrintService.PrintAsync(ReceiptDocument, nombre)` pertenece a presentación. Muestra el diálogo de impresora de Windows y pagina las imágenes del documento con proporción Carta conservada dentro del área imprimible. El PDF guardado conserva texto/vector; impresión utiliza páginas de alta resolución. Cancelar o fallar al guardar/imprimir no vuelve a confirmar la venta.

## Presentación

PDF Carta: encabezado de marca/número/fecha/estado de pago, bloques cliente y entrega, tabla de SKU/producto/cantidad/precio/descuento/impuesto/total, pagos y referencias, efectivo recibido/cambio/saldo, resumen con total destacado, observaciones de recepción y pie con numeración de páginas y advertencia de comprobante interno. No se incluyen firmas digitales, aceptación fiscal ni datos que no estén disponibles. Los importes se toman de la venta confirmada; no se recomponen subtotales ni descuentos históricos.

Datos proporcionados por el usuario para el emisor: **GoDental · Tel. 7203-7117**. Se configuran en la sección Pdf; dirección/correo se omiten mientras no se proporcionen.

Ventas y PDF ofrece Previsualizar junto a Guardar PDF. La consulta del comprobante ocupa una página propia dentro de ese espacio: Volver, Guardar PDF, Imprimir, anterior/siguiente y zoom. Papel blanco para lectura/impresión dentro de la interfaz Light/Dark. Los botones permanecen de tamaño uniforme. Se limpia el documento al cambiar de venta, recargar el listado o cerrar sesión; una respuesta de generación tardía no se aplica a otra selección/sesión.

La vista previa no guarda archivos temporales ni requiere navegador, visor PDF externo, conexión a Internet o dependencias nuevas. Impresión real depende de la impresora/controlador de Windows; las pruebas no envían trabajos automáticamente.

## Validación

Compilación Release local sin errores/advertencias y 172 pruebas xUnit correctas. Se generan documentos reales de una y cuatro páginas con datos sintéticos, textos largos y JSON histórico vacío/incorrecto; se comprueban permisos, selección/sesión obsoletas, límites de página/zoom, guardado de los mismos bytes y cancelación de impresión sin escrituras comerciales.

Se inspeccionaron localmente imágenes del PDF sintético de una página y de la última página del documento largo. La extracción con PyMuPDF confirmó teléfono del emisor, contacto/pedido, encabezado en todas las páginas, los 45 SKU sin pérdida, total final, leyenda interna y texto dentro de los límites Carta. Los ejemplos de prueba no contienen datos reales de clientes.

[CI Windows #94 correcto](https://github.com/Gust4av033/GoDental/actions/runs/39509926320), código `2300811`: **172 xUnit, 374 comprobaciones SQL (127 MVP) y 425 WPF**, sin errores de compilación ni binding. SQL confirma que el comprobante conserva el domicilio del pedido aunque cambie la ficha del cliente y que obtiene el pedido asociado automáticamente. WPF verifica imagen real, zoom, guardado/impresión del documento revisado, retorno al detalle y acceso desde pedido, con acciones visibles de 152×36 DIP en 1366×768 y 1093×614, en Light/Dark. Se verifica la composición para Carta y A4 dentro de su área imprimible sin deformar páginas.

Capturas WPF y PDF sintéticos en [test-results-94](https://github.com/Gust4av033/GoDental/actions/runs/39509926320/artifacts/12305156625). Las capturas de CI no se presentan como inspección manual de esas imágenes ni certificación del DPI físico. La cola/impresora real y el recorrido en la base del usuario quedan para comprobación manual; las pruebas no enviaron trabajos ni datos externos.
