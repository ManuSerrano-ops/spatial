# Arquitectura de analítica espacial

## Auditoría y límites de evidencia

La analítica parte exclusivamente del estado efectivo que ya consume la UI:

```text
EFFECTIVE STATE (REALIDAD o draft de ESCENARIO)
+ ValidationResults
+ ScenarioDiff opcional
        ↓
SpatialAnalyticsEngine puro
        ↓
SpatialAnalyticsReport
        ↓
Bridge runSpatialAnalytics
        ↓
Resumen numérico, tabla por plano y capa Heatmap
```

Las coordenadas de puestos son normalizadas (`x`, `y` entre `0..1`) y sirven para situar una entidad sobre su plano. `SpatialLocation` mantiene una referencia humana de rejilla 24 × 18 (`A-01`…`X-18`), pero no convierte coordenadas en distancias físicas, superficie o capacidad.

`seat.id`/`workstationId` sigue siendo identidad técnica. `displayLocation` es solo presentación y no participa como clave del motor, validación, Scenario Diff ni Apply.

## Métricas habilitadas

| Métrica | Fuente |
|---|---|
| Total de puestos | `maps[].seats[]` del estado efectivo |
| Ocupados, libres, reservados | `SeatStates.Derive(assignment)` |
| Ocupación y disponibilidad | Ocupados o libres / total de puestos, en porcentaje 0–100 |
| Problemas total, Critical, Warning, Info | `ValidationEngine` del mismo estado efectivo |
| Desglose por plano | `map.id` y sus puestos; no se hardcodean planos |
| Cambios de escenario | `ScenarioDiffEngine` ya calculado para base/draft |
| Puntos de heatmap | Coordenadas normalizadas válidas de puestos o cambios |

Los porcentajes vacíos devuelven `0`, evitando divisiones indefinidas. Las métricas de puestos incluyen todos los puestos; los heatmaps omiten únicamente coordenadas inválidas, porque no se pueden representar sobre el plano.

## Candidato — no habilitado

No existe un modelo fiable de subzonas: el filtro UI denominado “Zona” selecciona realmente un plano (`mapId`), mientras que `locations.json` es un catálogo de asignación sin vínculo espacial con plano, puesto o coordenada. Por ello **no se genera `byZone`**.

Tampoco se habilitan coste por m², eficiencia energética, productividad, presencia temporal, ocupación diaria, confort, ruido, capacidad, densidad física, rutas, evacuación, distancias o coste de movimiento. No hay unidades físicas, polígonos, superficies, aforos, calendario, telemetría ni rutas que lo respalden.

## SpatialAnalyticsEngine

`SpatialAnalyticsEngine.cs` es puro, determinista, read-only y sin dependencia de UI, persistencia o DOM.

- Las tasas se redondean a dos decimales y se expresan como porcentajes.
- Resultados, mapas y puntos se ordenan de forma determinista.
- Los problemas ponderan solo la representación Heatmap: `Critical = 3`, `Warning = 2`, `Info = 1`.
- Ocupación y disponibilidad aportan valor `1` por puesto representable.
- Un cambio de escenario representable aporta valor `1`.

La ponderación no es una puntuación de negocio ni una métrica científica: diferencia visualmente severidades ya existentes en `ValidationEngine`.

## Heatmap

La representación es una capa SVG ligera dentro de `#stage`, bajo los pines y con `pointer-events: none`. Así hereda el mismo pan, zoom y aspect ratio sin recalcular en cada frame, y los puestos conservan sus clics.

Cada punto se dibuja como un halo radial de radio proporcional al valor relativo de la capa actual. No se infieren superficies ni interpolaciones físicas. El selector ofrece solo:

- Ocupación;
- Disponibilidad;
- Problemas;
- Cambios de escenario, cuando el contexto dispone de diff.

La capa se recalcula únicamente tras respuesta de analítica, cambio de métrica, cambio de plano o cambio de contexto; no por hover, foco, movimiento del ratón, pan ni frame de zoom. Durante Planner se oculta visualmente sin cambiar la preferencia del usuario.

La leyenda contiene nombre, unidad, escala numérica y texto accesible. Los halos usan intensidad, borde/patrón y texto numérico equivalente en la vista Analítica; no son la única representación de los datos.

## Reality, Scenario y Compare

`DataStore.RunSpatialAnalytics(scenarioId)` usa `LoadUnlocked(scenarioId)`, por lo que REALIDAD y ESCENARIO calculan el mismo motor sobre estados efectivos distintos. En escenario calcula además un baseline sobre `base` y pasa el único `ScenarioDiffEngine` al informe efectivo.

Compare consume esos dos resultados y usa un máximo global compartido por métrica. Los porcentajes se comparan como puntos porcentuales (`pp`), no como porcentaje relativo. No se renderiza un segundo mapa ni se recalcula el diff en frontend.

## Excel

El export actual es una plantilla OOXML de tres hojas y está deliberadamente fuera del alcance de este hito. Una hoja `Resumen` segura requeriría una adición append-only de partes OOXML y pruebas estructurales específicas para no alterar filtros, dibujos, comentarios ni nombres definidos de la plantilla. La analítica espacial no cambia el contrato ni formato de exportación actual.
