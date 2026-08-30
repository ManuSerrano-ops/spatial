# JavaScript del WebView

Los scripts siguen siendo scripts clasicos globales durante esta fase de migracion. No anadir `defer`, `async` o `type="module"` sin convertir de forma coordinada los contratos de `window`.

## Orden de carga

- `index.html` conserva el orden historico de los helpers.
- `js/core/app.js` debe cargarse el ultimo: captura las APIs publicadas por los helpers y registra `window.receiveFromNative`.
- Las dependencias de orden actuales son Validation antes de Dashboard, Workspace State antes de Workspace Presentation y Grid Cell Metadata antes de Map Density.

## Limites

- `shared/` contiene utilidades transversales.
- `features/` agrupa comportamiento por funcionalidad.
- `core/` contiene bootstrap y coordinacion global temporal.

La siguiente fase extraera flujos de `core/app.js` sin modificar el contrato del bridge ni los IDs DOM.


## Modulos de presentacion ya extraidos

Estas factories no conocen el bridge WebView ni escriben datos operativos. Reciben sus dependencias de `core/app.js` de forma explicita:

- `shared/ui/ui-theme-feature.js`: aplica el tema visual al documento y al selector.
- `shared/ui/detail-panel-controller-feature.js`: abre, cierra y cambia el modo comun del panel derecho sin renderizar datos de negocio.
- `features/map/map-appearance-feature.js`: mantiene la apariencia Claro/Oscuro y sus tokens CSS locales.
- `features/map/cell-appearance-feature.js`: carga y guarda la presentacion local de una celda y solicita un render mediante un callback inyectado.
- `features/map/cell-detail-feature.js`: renderiza el contenido del panel de una celda a partir de su composicion y miembros.
- `features/filters/workspace-filter-feature.js`: aplica los criterios de filtros a puestos para mapa, lista y seleccion rectangular.
- `features/filters/workspace-filter-ui-feature.js`: actualiza contador, chips y bindings de los controles de filtro.
- `features/selection/selection-controller-feature.js`: gestiona el modo Seleccionar, multiseleccion y limpieza explicita sin mezclar ambos estados.

Las preferencias visuales locales conservan sus claves historicas de `localStorage`:

```text
plano.mapAppearance
plano.gridCellAppearances
```

No se deben usar estas factories para mover coordenadas, cambiar asignaciones, actualizar membresias ni llamar al bridge.

## Regla para la siguiente extraccion

1. Identificar una responsabilidad completa y sus consumidores.
2. Crear una factory en `shared/` o `features/<dominio>/` con dependencias inyectadas.
3. Cargarla en `index.html` despues de sus helpers y antes de `core/app.js`.
4. Crear un harness especifico en `tests/<nombre>-harness.js`.
5. Ejecutar el harness, las regresiones relacionadas, `node --check` de todos los scripts y `dotnet build --no-restore`.

`core/app.js` sigue siendo el coordinador temporal de DOM, render, viewport y bridge. No convertir solo una parte a modulos ES mientras los demas scripts dependan de APIs globales `window.*`.
