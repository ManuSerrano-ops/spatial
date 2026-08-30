# Pin state architecture

## Auditoría anterior

El render anterior construía una cadena de clases en `Resources/js/core/app.js`:

```text
pin occupied partial scenario-touch selected multi-selected
problem-critical planner-source planner-proposal-selected dim
```

Los estados reales eran:

| Capa | Estado anterior | Fuente |
|---|---|---|
| Business | `free`, `occupied`, `reserved` | `seat.state` o asignación efectiva |
| Quality | `complete`, `partial` | `seat.completeness` |
| Interaction | `selected`, `multi-selected`, `dim` | selección y filtros UI |
| Search | `search-hit` temporal | clase añadida tras render |
| Problems | `problem-critical`, `problem-warning`, `problem-info` | máximo de ValidationEngine por puesto |
| Scenario | `scenario-touch` | conjunto genérico de cambios pendientes |
| Planner | `planner-source`, `planner-destination`, `planner-unavailable`, `planner-proposal-selected` | estado de Movement Planner |
| Focus / hover | pseudo-clases del botón | navegador |

### Inconsistencias detectadas

- Business, calidad y problemas compartían el fill/color del pin.
- La calidad `partial` se trataba como una combinación de color del estado
  business, aunque representa integridad de datos.
- `scenario-touch` no identificaba ADDED, REMOVED, MOVED o MODIFIED.
- `search-hit` se aplicaba fuera del render y podía perderse en el siguiente
  render.
- Multiple reglas de `box-shadow` se sobrescribían por orden de CSS, en lugar
  de por una prioridad definida.
- Los selectores de Planner, Problems y selección usaban clases solapables.

## Arquitectura actual

`Resources/js/features/map/pin-state-helpers.js` expone `derivePinPresentation(input)`. Es una
función pura: no persiste ni muta datos; deriva presentación desde assignment,
seat, validación, Scenario Diff, Planner, selección y búsqueda.

El render produce un botón `.pin` con atributos:

```text
data-state="free|occupied|reserved"
data-quality="complete|incomplete"
data-problem="none|info|warning|critical"
data-scenario="none|added|removed|moved|modified"
data-planner="none|source|destination|blocked"
data-selected="true|false"
data-multi-selected="true|false"
data-search-hit="true|false"
```

`--pin-z` también se deriva en el helper. No se crea otra fuente de verdad.

## Capas

### Business state

| Estado | Regla | Señal visual redundante |
|---|---|---|
| FREE | sin ocupante/asignación efectiva | círculo claro con borde |
| OCCUPIED | ocupante/asignación efectiva | fill de ocupado |
| RESERVED | estado `reserved` efectivo | forma cuadrada con borde doble |

### Data quality

`complete` y `partial`/`incomplete` no modifican el fill business. Calidad
incompleta se expresa mediante badge `!` secundario.

### Interaction

- Hover: escala sutil, sin cambiar fill business.
- Focus: ring de foco visible.
- Selected: ring de selección.
- Multi selected: outline punteado adicional.
- Search hit: ring temporal; se almacena temporalmente en `ui`, se vuelve a
  derivar en cada render y expira a los 1600 ms.

### Problems

Solo se muestra la severidad máxima devuelta por `getWorkspaceMaxSeverity`.
Los símbolos son `×`, `!`, `i`. El fill de business no se reemplaza.

### Scenario

Solo en contexto Scenario. El badge usa `+`, `−`, `→`, `~` para ADDED,
REMOVED, MOVED y MODIFIED. Reality deriva siempre `none`.

### Planner

Planner mantiene símbolos sin depender solo del color:

- SOURCE: `●`
- DESTINATION: `◎`
- BLOCKED: `×` + outline punteado

## Prioridad visual explícita

La prioridad controla el ring principal y `--pin-z`; las capas inferiores no
se eliminan:

| Prioridad | Estado | z-index |
|---:|---|---:|
| 1 | BLOCKED | 60 |
| 2 | Planner SOURCE / DESTINATION | 50 |
| 3 | Selected | 40 |
| 4 | Search hit | 35 |
| 5 | Critical / Warning / Info problem | 30 |
| 6 | Multi selected | 25 |
| 7 | Scenario indicator | 10 + badge |
| 8 | Quality indicator | 10 + badge |
| 9 | Business state | 10 + fill/shape |

`Selected + Critical` mantiene el símbolo crítico, pero usa el ring de
selección. Planner conserva prioridad sobre selección y problemas.

## Accessibility and tooltip

`aria-label` begins with `displayLocation`, never technical `seat.id`, then
adds business state, persona disponible, calidad, problema máximo, Scenario y
Planner. `title` and hover tooltip use the same available data, without bridge
calls. Focus remains visibly distinct from hover.

## Scope

This system does not change SVG resources, map/workspace background, grid,
map filters, coordinates, IDs, business logic, DataStore, Planner algorithms,
ValidationEngine, Analytics or persistence.
