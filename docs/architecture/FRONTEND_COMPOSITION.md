# Composicion del frontend WebView

## Objetivo

El frontend conserva scripts clasicos globales mientras se ordena sin cambiar el contrato WebView2. `Resources/js/core/app.js` se carga el ultimo y compone las dependencias; no debe volver a absorber logica de una funcionalidad ya extraida.

## Capas fisicas

```text
Resources/js/
├─ core/       bootstrap, estado transversal, bridge y coordinacion fina
├─ shared/     utilidades reutilizables entre funcionalidades
└─ features/   comportamiento agrupado por dominio de producto
```

## Responsabilidades actuales

| Ubicacion | Responsabilidad |
|---|---|
| `core/app.js` | bootstrap, `appState`, bridge WebView, carga de datos, rutas de vista y orquestacion de render |
| `shared/ui/` | tema, cabecera/apertura/cierre del panel derecho |
| `shared/workspace/` | estado, calidad y presentacion de puestos |
| `features/map/` | aspecto, viewport, pines, celdas, densidad y detalle de celda |
| `features/filters/` | coincidencia, contador, chips y controles de filtros |
| `features/selection/` | modo Seleccionar, multiseleccion, limpieza y reglas Bulk |
| `features/managed-areas/` | clusters, cards, tamano, contenido y drag |
| `features/*` restantes | Planner, escenarios, analitica y dashboard mediante helpers existentes |

## Regla de dependencias

Una factory de feature recibe sus dependencias por inyeccion. No debe importar de forma oculta el bridge, modificar archivos JSON, ni guardar datos operativos.

```text
core/app.js -> factory de feature -> DOM o estado inyectado
core/app.js -> WebViewBridge -> C# -> DataStore
```

El orden de scripts en `Resources/index.html` es parte del contrato. No introducir `type="module"`, `defer` o `async` parcialmente mientras las APIs se publiquen como `window.*`.

## Limite deliberado de core/app.js

Debe quedarse como coordinador de bajo nivel, no como segundo hogar de reglas de producto:

- bootstrap y composicion de factories;
- estado transversal y cambio de vista;
- mensajes WebView y ciclo de carga/recarga;
- coordinacion entre renderizadores de feature.

Pendientes futuros, solo como tareas separadas: render de Inspector, detalle de cluster, Planner, escenarios, problemas y render completo del mapa. No son requisitos para que la estructura fisica actual sea mantenible.
