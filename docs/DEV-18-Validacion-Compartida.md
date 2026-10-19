# DEV-18 · Cierre de diálogos en las pruebas de Windows

El smoke de Windows se detenía después de capturar Proveedores en Light: `RenderDialogAsync` cerraba el formulario de apertura de conteo, cuyo `HasUnsavedChanges` activa siempre la confirmación de descarte. Como no había respuesta automatizada, el runner esperaba una interacción humana.

El helper de capturas ahora responde únicamente a la confirmación hija del formulario que está cerrando y comprueba que éste ya no sea visible. Ejecuta la acción de descarte, nunca el guardado del formulario original. Las comprobaciones existentes de Volver/Descartar permanecen vigentes.

Este ajuste pertenece al arnés compartido de validación, sin cambios en el producto WPF. Se incorpora mediante el mismo commit común a `feature/ui` y `feature/logica`, conservando el historial de ambas y sin mezclar el refinamiento visual con los cambios de negocio. La ejecución de CI en Windows verifica el cierre real; compilar en Linux no demuestra que el diálogo haya cerrado.
