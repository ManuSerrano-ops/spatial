# Contrato DOM de `Resources/js/core/app.js`

Inventario previo al rediseño. Los anclajes de este documento no se renombran ni se eliminan; pueden cambiar de posición y recibir clases visuales nuevas.

## Reglas transversales

- `app.js` busca los elementos mediante `document.getElementById`; todos los IDs listados son obligatorios.
- Los paneles identificados como diálogos deben seguir siendo elementos `<dialog>`: el código invoca `showModal()` y `close()`.
- Durante una solicitud, el selector global `button, select, input[type="checkbox"]` deshabilita controles. Los controles interactivos deben conservar esos elementos nativos.
- Persona, dispositivo y ubicación son campos de texto libre; solo roseta conserva el `datalist` de sugerencias.
- Los marcadores del plano se generan dinámicamente como botones dentro de `#pins`. No se deben sustituir por nodos no interactivos.

## IDs obligatorios

| Área | IDs | Uso de `app.js` |
|---|---|---|
| Visor de planos | `mapwrap`, `stage`, `plan`, `grid-labels`, `pins`, `tabs` | `mapwrap` recibe paneo, zoom y menú contextual; `stage` recibe transformación y variables de cuadrícula; `plan` recibe `src`, `alt`, clic y geometría; los otros tres son contenedores dinámicos. |
| Estado y búsqueda | `search`, `status`, `tooltip`, `message` | Filtrado inmediato, mensajes de estado, previsualización y errores/confirmaciones. |
| Ficha de puesto | `detail-panel`, `close-panel`, `seat-kicker`, `title`, `detail`, `seat-name`, `person`, `device`, `location`, `roseta`, `assignment-status`, `notes`, `save`, `delete-assignment`, `delete-seat`, `scenario-note` | Carga/edición de asignaciones, borrado y cierre de ficha. `detail-panel` recibe la clase `hidden`. |
| Rosetas | `rosetas-list` | `datalist` rellenado con rosetas conocidas; persona, dispositivo y ubicación no usan catálogos. |
| Herramientas de plano | `add-seat`, `add-seat-label`, `context-menu`, `context-add-seat`, `context-copy-cell`, `context-reset-view` | Alta de puesto, menú contextual, copia de celda y restablecimiento de vista. `add-seat-label` cambia de texto sin borrar el icono embebido. |
| Escenarios | `scenario-mode`, `scenario-guide`, `scenario-guide-dialog`, `new-scenario`, `scenario-dialog`, `scenario-name`, `cancel-scenario`, `create-scenario-confirm`, `delete-scenario`, `diff`, `diff-dialog`, `diff-title`, `diff-empty`, `diff-list`, `apply`, `apply-dialog` | Cambio/creación/borrado de escenario, guía, diff, selección y aplicación. `scenario-mode`, `diff`, `apply`, `delete-scenario` y `undo` reciben cambios de `disabled`. |
| Deshacer | `undo`, `undo-dialog`, `undo-preview`, `confirm-undo` | Vista previa y confirmación del último cambio. |
| Auditoría y recuperación | `history`, `history-dialog`, `events-list`, `backups`, `backups-dialog`, `backups-list` | Consulta de eventos, lista de backups y botones dinámicos de restauración. |
| Utilidades | `integrity`, `integrity-dialog`, `integrity-summary`, `integrity-list`, `export-excel`, `reload` | Informe de integridad, exportación con selector nativo y recarga. |
| Menú responsivo | `more-menu`, `more` | Apertura/cierre mediante la clase `open` y `aria-expanded`. |
| Apariencia | `theme` | Selección de los cuatro temas y persistencia local por usuario. |

## Clases y atributos que forman parte del contrato

| Selector/atributo | Uso |
|---|---|
| `.pin` | El paneo excluye los marcadores con `event.target.closest('.pin')`. Es clase obligatoria en cada botón de puesto creado dinámicamente. |
| `.dialog-close` | Dentro de `#scenario-dialog`, el primer elemento con esta clase cierra el diálogo. |
| `[data-close]` | Cada elemento recibe un manejador que ejecuta `document.getElementById(element.dataset.close).close()`. El valor debe ser el ID de un `<dialog>`. |
| `.hidden` | Se añade/elimina en `#detail-panel`. |
| `.show` | Se añade/elimina en `#tooltip` y `#context-menu`. |
| `.open` | Se añade/elimina en `#more-menu`. |
| `.is-active` | Se añade/elimina en `#add-seat` durante el modo de alta. |
| `.is-busy` | Se añade/elimina en el control que originó una acción para mostrar ocupado sin sustituir el estado deshabilitado. |
| `.active` | Se asigna a la pestaña del plano activo generada dentro de `#tabs`. |
| `.grid-column-label`, `.grid-row-label` | Clases de las etiquetas de cuadrícula creadas dentro de `#grid-labels`. |
| `.change-row`, `.activity-row` | Filas generadas para Diff, historial, backups e integridad. |
| `free`, `occupied`, `reserved`, `inconsistent`, `scenario-touch`, `selected`, `dim` | Estados adicionales de cada `.pin`. Su estilo comunica el estado del puesto, cambios de escenario, selección y filtrado. |


## Manejadores de interacción

| Origen | Evento | Efecto |
|---|---|---|
| `#mapwrap` | `dragstart` (captura) | Cancela el arrastre nativo. |
| `#mapwrap` | rueda | Zoom anclado al cursor. |
| `#mapwrap` | `pointerdown`, `pointermove`, `pointerup` | Paneo; los `.pin` quedan excluidos para preservar el arrastre de puestos. |
| `#mapwrap` | `contextmenu` | Muestra el menú contextual con la celda calculada. |
| `#plan` | clic | Crea puesto si está activo el modo de alta; si no, cierra la ficha. |
| `#tabs` | botones creados dinámicamente | Cambia de plano, reinicia la vista y cierra ficha. |
| `#pins` | botones creados dinámicamente | Selección, arrastre, tooltip y guardado de la nueva posición. |
| `#scenario-mode` | `change` | Cambia entre realidad y escenario. |
| `#theme` | `change` | Aplica y guarda la apariencia local sin tocar datos compartidos. |
| Escenarios | clic en `scenario-guide`, `new-scenario`, `cancel-scenario`, `create-scenario-confirm`, `delete-scenario`, `diff`, `apply`, `apply-dialog` | Abre/cierra diálogos y despacha las acciones de escenario. |
| Deshacer | clic en `undo`, `confirm-undo` | Solicita vista previa y ejecuta el deshacer confirmado. |
| Herramientas | clic en `history`, `backups`, `integrity`, `export-excel`, `reload` | Abre cada diálogo o despacha la acción correspondiente. |
| Ficha | clic en `save`, `delete-assignment`, `delete-seat`, `close-panel` | Guarda/borrar/oculta la ficha. |
| Plano | clic en `add-seat`, `context-add-seat`, `context-copy-cell`, `context-reset-view` | Cambia modo, crea puesto, copia celda o reinicia zoom y paneo. |
| Documento | `pointerdown` | Cierra menú contextual y el menú Más si el clic ocurre fuera. |
| `[data-close]` | clic | Cierra el diálogo apuntado por el atributo. |

## Salidas dinámicas que deben conservar su contenedor

- `#tabs` recibe botones de planos.
- `#grid-labels` recibe las etiquetas de columnas y filas.
- `#pins` recibe botones de puesto con las clases de estado.
- `#diff-list` recibe filas con checkboxes de selección.
- `#events-list`, `#backups-list` e `#integrity-list` reciben filas de actividad; los backups añaden un botón **Restaurar**.
- `#rosetas-list` recibe sugerencias de rosetas conocidas.

## Sprite de iconos

- El sprite local está embebido en `index.html`; cada `<use href="#icon-…">` debe apuntar a un `<symbol>` existente.
- Los iconos se insertan dentro de los botones existentes, nunca sustituyen IDs ni separan etiquetas del elemento.
- Los símbolos usan `currentColor`, trazo uniforme y tamaños CSS de 16, 20 o 24 px.

## Anclajes no consultados por `app.js`

`.topbar`, `.brand`, `.header-actions`, `.map-panel`, `.pin-legend`, `.more-panel`, `.form-grid`, `.panel-actions`, `.dialog-content`, `.dialog-actions`, `.wide-dialog` y las clases de presentación del HTML actual no son contrato JavaScript. Pueden reorganizarse durante el rediseño siempre que los anclajes anteriores sigan presentes.
