# DEV-34 · Códigos de barras y formatos de impresión

Solicitud del usuario posterior a 2.0B, antes de 2.1A: visualizar/imprimir códigos generados en tamaños distintos, y formatos de comprobantes/tickets de envío.

## Estado comprobado

Productos → ficha → Códigos de barras muestra el valor textual, tipo y estado. El código interno GD + Id de diez dígitos es CODE128; existe búsqueda POS por valor activo. Todavía no se codifican/renderizan barras ni se genera PDF de etiquetas. No basta cambiar una fuente o imprimir el valor textual para crear una etiqueta escaneable.

Ventas y PDF genera comprobante interno de venta/entrega en Carta, con vista previa, guardar e imprimir. El diálogo selecciona impresora/papel, pero el documento mantiene el diseño Carta; reducirlo no lo convierte en ticket térmico legible. No existe factura fiscal/DTE emitida ni etiqueta oficial de courier. Se conserva esa distinción al agregar formatos.

## Siguiente complemento de impresión

Definir medidas físicas en mm (ancho/alto de etiqueta, separación/márgenes/columnas si es hoja), cantidad de copias, simbología, resolución y modelo de impresora. Generar barras con biblioteca de simbología verificable, conservar quiet zones y ancho mínimo del módulo, no deformar por escalado libre; rechazar tamaños que no permitan codificar el valor. Vista previa/PDF deben ser del mismo documento enviado a impresión, con tamaño real 100% y manejo de área imprimible. Añadir selector de formato para comprobantes (Carta/A4 y rollos 80/58 mm) y documento de entrega con datos del pedido; plantillas distintas, sin simular DTE ni guía de transportista.

Preferencia confirmada por el usuario: todavía no tiene impresora; preparar tamaños configurables en mm, sin fijar un modelo. Los presets sugeridos son etiquetas 50×30/40×30 mm, Carta/A4 y tickets 80/58 mm, con validación de cada diseño. Esa consulta no bloquea 2.1A. Implementar el complemento con pruebas de decodificación de barras, dimensiones PDF, paginación y WPF antes de declararlo disponible. Comprobar escáner/impresora física en el piloto.
