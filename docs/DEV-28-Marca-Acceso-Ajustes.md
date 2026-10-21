# DEV-28 · Marca, acceso, ajustes y comprobante

Incremento solicitado en `feature/desarrollo`. Se consultan AGENTS, DM-01, DB-01, ARQ-01, UX-01 y las referencias `Imagenes/`: 221214 para Ajustes/apariencia y el patrón de tarjetas de 221111. No hay una pantalla de login específica en esas composiciones; se extiende su lenguaje, sin copiar funciones de configuración que aún no tienen contrato.

## Decisiones previas a implementación

- El logo facilitado por el usuario se adapta a PNG transparente con limpieza de fondo mediante edición de imagen, conservando diente, colores y rotulación. `assets/branding/GoDentalLogo.png` es el recurso común para WPF y PDF, incluido en ambos ensamblados; no depende de rutas locales ni descargas al abrir el programa. Se presenta sobre superficie clara para conservar el azul original también con tema oscuro.
- El comprobante omite SKU y hora. Conserva productos/cantidades/importes, fecha de venta, número, cliente/pedido, teléfono 7203-7117 y leyenda interna. El logo se incorpora al encabezado. No se cambia la venta ni el inventario, y no se agrega migración.
- Login: marca real, formulario con jerarquía clara, acceso a Ajustes y tema; conserva PasswordBox, limpieza de claves, primer administrador, cambio obligatorio de contraseña, estados/carga/error y flujo automático de comprobación. Enter usa la acción principal del formulario.
- Ajustes conserva los contratos actuales de conexión local y apariencia. Se reorganiza en pestañas; desarrollo/certificado local quedan en una sección avanzada. Guardar y comprobar conservan estados y botones uniformes. La conexión no se puede editar mientras hay sesión; el tema permanece disponible. No se agregan configuración fiscal, backups o parámetros comerciales sin contrato.
- Preparación desaparece del menú y se elimina su contenido técnico. La ruta histórica `SetupPage.Preparation` redirige a Ajustes por compatibilidad; la comprobación automática del acceso sigue activa. No se eliminan comprobaciones de instalación ni reglas de seguridad.

## Validación

Compilación Release local de toda la solución con `EnableWindowsTargeting=true`, sin errores ni advertencias; **172 pruebas xUnit correctas**. Se generan comprobantes reales sintéticos de una y cuatro páginas. La extracción con PyMuPDF confirma logo embebido en todas las páginas, fecha sin hora, ausencia de SKU, contacto/pedido, teléfono del emisor y leyenda interna. Se inspecciona visualmente la primera página del ejemplo corto; conserva resumen, pagos, entrega y firma.

El smoke Windows se amplía para comprobar recurso de marca embebido, compatibilidad de navegación, bloqueo/desbloqueo de conexión por sesión, persistencia de tema sin modificar el perfil, acción de acceso con Enter y dimensiones visibles, y pestañas de Ajustes a 760×680 y 540×500 en ambos temas. Acceso se renderiza a 920×660 y 760×540. La validación nativa se ejecuta en CI después de sincronizar el incremento; su resultado se registrará al finalizar.

No se cambia SQL ni se requiere migración. La cola/impresora real, el DPI físico y la base del usuario no se modifican durante las pruebas.
