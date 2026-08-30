# Comprobación manual — rediseño UI Figma

Ejecutar contra `uifigmastyle/publish/PlanoOpenSpaceIT.Windows.exe`. Esta copia usa exclusivamente `uifigmastyle/runtime-data`.

## 1. Arranque, temas, iconos y accesibilidad

- [ ] La ventana abre sin panel de error y muestra los cinco planos.
- [ ] La barra, selector vertical de planos, inspector lateral, diálogos, menús y listas tienen una jerarquía compacta de herramienta de trabajo; el lienzo sigue siendo el área dominante.
- [ ] El selector **Apariencia** ofrece Claro profesional, Penpot oscuro, Alto contraste y Proyector.
- [ ] Recorre los cuatro temas: no aparece morado/púrpura, texto y bordes conservan contraste, y el lienzo y SVG no cambian de aspecto.
- [ ] En Proyector se aprecian tipografía, bordes y estados más visibles; no es una mera variante clara de Alto contraste.
- [ ] Selecciona un tema, cierra normalmente, vuelve a abrir y comprueba que se restaura la elección.
- [ ] Al pulsar `Tab`, todos los controles utilizables muestran foco visible: pestañas, botones, selector de tema, selector de escenario, campos, menú Más, diálogos y marcadores de puesto.
- [ ] Los iconos acompañan al texto de acciones importantes; Más, Recargar y Cerrar usan icono sin texto pero tienen tooltip y etiqueta accesible.
- [ ] La tipografía y los iconos se cargan localmente; sin conexión no aparece ningún hueco en la interfaz.

## 2. Visor y marcadores

- [ ] Cambia entre los cinco planos desde el panel izquierdo vertical; el plano activo queda diferenciado y el panel no colisiona con la ficha derecha.
- [ ] Reduce la ventana por debajo de 1100 px: el selector izquierdo se reduce a abreviaturas, pero siguen disponibles los cinco planos y sus tooltips.
- [ ] Con la rueda, acerca y aleja el plano conservando el punto bajo el cursor; el indicador muestra únicamente el porcentaje de zoom actual, sin modo de escenario ni número de planos.
- [ ] Arrastra tanto sobre el plano como sobre el fondo vacío del visor: ambos desplazan la vista sin iniciar el arrastre nativo de la imagen.
- [ ] Haz clic en un puesto: se abre la ficha. Haz clic sobre el plano: se cierra.
- [ ] Botón derecho en el plano: aparecen Añadir puesto, Copiar referencia de celda y Restablecer vista; prueba las tres acciones.
- [ ] Todos los puestos son círculos perfectos a cualquier nivel de zoom y en los cinco planos. Libre usa un anillo simple; ocupado, un círculo relleno; reservado, un doble anillo; e inconsistente, un borde discontinuo. No aparece punto, barra ni `!` en el centro.
- [ ] Comprueba los colores de estado en cada apariencia: libre, ocupado, reservado e inconsistente mantienen su significado y se distinguen sobre el plano. Solo los puestos con persona, dispositivo, ubicación y roseta rellenados se muestran como ocupados; una asignación parcial se muestra como inconsistente. El patrón debe seguir siendo legible aunque no distingas esos colores.
- [ ] La guía visible de chinchetas muestra los cuatro estados con el mismo color y patrón que los pines; no aparece texto de rueda, arrastre ni referencia de celda sobre el plano.

- [ ] Selecciona un puesto y comprueba que recibe un anillo azul adicional, independiente de su estado.
- [ ] Arrastra un puesto, confirma el guardado y recarga: la posición persiste. Crea y elimina un puesto de prueba; tras eliminarlo, la ficha derecha se cierra.
- [ ] Activa **Añadir puesto** y vuelve a pulsarlo para cancelar: el modo termina y no queda abierto el menú contextual de añadir puesto.

## 3. Ficha y validación

- [ ] Selecciona un puesto y comprueba que se cargan nombre, persona, dispositivo, ubicación, roseta y notas.
- [ ] Persona, dispositivo y ubicación aceptan texto libre y no muestran sugerencias de catálogo. Roseta conserva sus sugerencias.
- [ ] Selecciona **Reservado**, guarda y comprueba el pin ámbar aun sin campos de asignación. Vuelve a **Automático**: los campos se conservan y el estado se recalcula.
- [ ] Guarda una asignación válida y confirma que aparece un mensaje breve azul que desaparece solo y que los cambios persisten tras recargar.
- [ ] Borra una roseta o una nota, guarda y recarga: el campo queda vacío.
- [ ] Intenta guardar una roseta duplicada: se bloquea y aparece un error rojo persistente que identifica la roseta, puesto, posición, persona, equipo y ubicación que ya la usan; el formulario conserva lo escrito.
- [ ] Intenta guardar un dispositivo duplicado: se bloquea con el mismo comportamiento.
- [ ] Asigna una persona ya usada: se guarda y muestra la advertencia no bloqueante.
- [ ] Quita una asignación y prueba eliminar un puesto de prueba; verifica que ambas acciones siguen disponibles y se confirman.

## 4. Esperas, controles y errores

- [ ] Al pulsar Guardar, el botón de origen pasa a estado ocupado azul con indicador giratorio; el resto de acciones queda deshabilitado.
- [ ] En el acto, el estado muestra `Guardando asignación…`. Tras dos segundos de espera, muestra además `Esperando respuesta…`; ambos textos permanecen visibles y legibles.
- [ ] Al completar con éxito, todos los controles se reactivan y el botón recupera su aspecto normal.
- [ ] Para forzar una espera, ejecuta el helper contra los datos aislados y espera a que escriba `locked`:

```powershell
& "G:\Proyecto Planos\phm\phm\uifigmastyle-tests\PlanoOpenSpaceIT.LockHelper\bin\Debug\net8.0\PlanoOpenSpaceIT.LockHelper.exe" "G:\Proyecto Planos\phm\phm\uifigmastyle\runtime-data\data"
```

- [ ] Con el helper reteniendo el bloqueo, guarda un cambio. Comprueba el estado ocupado y el mensaje escalonado. Si sueltas con Enter antes de diez segundos, el guardado termina correctamente.
- [ ] Repite sin soltar durante más de diez segundos. Debe aparecer un error rojo persistente, los controles deben reactivarse y el formulario debe conservar los valores introducidos. Tras soltar y recargar, el cambio no debe existir.

## 5. Escenarios, Diff y deshacer

- [ ] Crea un escenario, edita una asignación y mueve un puesto dentro de él; la realidad no cambia todavía.
- [ ] Abre Diff: las filas permiten seleccionar cambios mediante checkbox.
- [ ] El botón de cabecera **Aplicar seleccionados** muestra su texto, se habilita cuando corresponde y aplica los cambios seleccionados tras confirmar. El botón homónimo del diálogo conserva el mismo comportamiento.
- [ ] Borra un escenario de prueba y confirma que no altera la realidad.
- [ ] Prueba Deshacer: se muestra la vista previa, confirma y comprueba que el resultado persiste tras recargar.

## 6. Utilidades, iconos y responsive

- [ ] Abre Historial y comprueba que carga eventos con icono y texto legibles.
- [ ] Abre Backups, comprueba que lista las copias y prueba Restaurar solo sobre una copia de prueba cuyo efecto puedas verificar.
- [ ] Abre Verificar integridad y confirma que muestra rosetas duplicadas, marcas históricas, asignaciones sin puesto y posiciones huérfanas, sin categorías de catálogos ni correcciones automáticas.
- [ ] Exporta Excel: aparece el selector nativo de carpeta, permite elegir recordar la carpeta y muestra la ruta final al terminar. Cancela otra exportación y confirma que no genera fichero ni muestra error.
- [ ] Comprueba icono y función en: guía de escenarios, crear/eliminar escenario, añadir puesto, deshacer, Diff, Aplicar seleccionados, Historial, Backups, Exportar, Verificar integridad, Guardar, Quitar asignación, Eliminar puesto y las tres acciones del menú contextual.
- [ ] Reduce la ventana por debajo de 1100 px: en **Más** aparecen Diff, Historial, Backups, Exportar y Verificar integridad, con icono y texto, y todos siguen funcionando.

## 7. Cierre

- [ ] Cierra la aplicación normalmente.
- [ ] Si creaste puestos, escenarios o asignaciones solo para comprobar el rediseño, elimínalos o restáuralos dentro de `uifigmastyle/runtime-data`; nunca modificar los datos del proyecto original.
