# DEV-28 · Marca, acceso, ajustes y comprobante

Incremento solicitado en `feature/desarrollo`. Se consultan AGENTS, DM-01, DB-01, ARQ-01, UX-01 y las referencias `Imagenes/`: 221214 para Ajustes/apariencia y el patrón de tarjetas de 221111. No hay una pantalla de login específica en esas composiciones; se extiende su lenguaje, sin copiar funciones de configuración que aún no tienen contrato.

## Decisiones previas a implementación

- El logo facilitado por el usuario se adapta a PNG transparente con limpieza de fondo mediante edición de imagen, conservando diente, colores y rotulación. `assets/branding/GoDentalLogo.png` es el recurso común para WPF y PDF, incluido en ambos ensamblados; no depende de rutas locales ni descargas al abrir el programa. La placa usa la superficie dinámica del tema: clara en Light y oscura en Dark, conservando el PNG transparente y los colores originales. Ajuste solicitado posteriormente por el usuario. En Dark, el componente compartido eleva el contraste de la rotulación mediante una máscara del mismo PNG; conserva el diente y su degradado, sin modificar el archivo usado en PDF.
- El comprobante omite SKU y hora. Conserva productos/cantidades/importes, fecha de venta, número, cliente/pedido, teléfono 7203-7117 y leyenda interna. El logo se incorpora al encabezado. No se cambia la venta ni el inventario, y no se agrega migración.
- Login: marca real, formulario con jerarquía clara, acceso a Ajustes y tema; conserva PasswordBox, limpieza de claves, primer administrador, cambio obligatorio de contraseña, estados/carga/error y flujo automático de comprobación. Enter usa la acción principal del formulario.
- Ajustes conserva los contratos actuales de conexión local y apariencia. Se reorganiza en pestañas; desarrollo/certificado local quedan en una sección avanzada. Guardar y comprobar conservan estados y botones uniformes. La conexión no se puede editar mientras hay sesión; el tema permanece disponible. No se agregan configuración fiscal, backups o parámetros comerciales sin contrato.
- Preparación desaparece del menú y se elimina su contenido técnico. La ruta histórica `SetupPage.Preparation` redirige a Ajustes por compatibilidad; la comprobación automática del acceso sigue activa. No se eliminan comprobaciones de instalación ni reglas de seguridad.

## Validación

Compilación Release local de toda la solución con `EnableWindowsTargeting=true`, sin errores ni advertencias; **172 pruebas xUnit correctas**. Se generan comprobantes reales sintéticos de una y cuatro páginas. La extracción con PyMuPDF confirma logo embebido en todas las páginas, fecha sin hora, ausencia de SKU, contacto/pedido, teléfono del emisor y leyenda interna. Se inspecciona visualmente la primera página del ejemplo corto; conserva resumen, pagos, entrega y firma.

El smoke Windows se amplía para comprobar recurso de marca embebido, compatibilidad de navegación, bloqueo/desbloqueo de conexión por sesión, persistencia de tema sin modificar el perfil, acción de acceso con Enter y dimensiones visibles, y pestañas de Ajustes a 760×680 y 540×500 en ambos temas. Acceso se renderiza a 920×660 y 760×540. Resultado del código `669d959`: [CI Windows #96 correcto](https://github.com/Gust4av033/GoDental/actions/runs/39632247487), compilación Release sin advertencias/errores, **172 xUnit, 374 comprobaciones SQL (127 MVP) y 451 WPF**, sin errores de binding. Se confirman logo embebido, navegación antigua redirigida, bloqueo por sesión y persistencia del tema, acción de acceso visible con Enter en ambos tamaños y las dos pestañas de Ajustes en claro/oscuro.

Capturas y resultados en [test-results-96](https://github.com/Gust4av033/GoDental/actions/runs/39632247487/artifacts/12359582385). El render automático de las vistas WPF no se presenta como inspección manual de esas imágenes ni del monitor. El PDF sintético sí se inspeccionó localmente. SQL usa bases aisladas de CI.

No se cambia SQL ni se requiere migración. La cola/impresora real, el DPI físico y la base del usuario no se modifican durante las pruebas.
