# Auditoría de refactorización de `core/app.js`

> Estado: bloquea 6.4 y 6.5 hasta construir cobertura funcional independiente.

Este directorio conserva la evidencia de la auditoría realizada sobre el
commit `854fa07` (`Migrate first frontend harness batch`). No es código de
producción ni debe cargarse desde `Resources/index.html`.

## Snapshot de Prettier

`app.js.prettier-120.snapshot.js` es la salida generada, sin escribirla sobre
el árbol de trabajo, con:

```text
npx --yes prettier@3.5.3 --print-width 120 Resources/js/core/app.js
```

| Documento | SHA-256 | Líneas | Bytes |
|---|---|---:|---:|
| `Resources/js/core/app.js` original | `d4c858a1730213043169020f6da8782d532d4556100ca3c0f0a5d049c29371ac` | 1.080 | 202.373 |
| `app.js.prettier-120.snapshot.js` | `9499cf2db4a64a2be1945cec69964eb247160652cf35097356677639dcd0ee89` | 5.286 | 231.769 |

La diferencia de líneas no representa más lógica: Prettier expandió objetos,
callbacks, cadenas de llamadas y bloques antes compactados. Los contadores
orientativos se conservaron: 2.909 puntos y coma y 235 ocurrencias de
`function` en ambos textos. Los bytes crecieron un 14,5 %.

La política acordada para una futura reanudación es usar el `printWidth` de
Prettier de 120 caracteres. Los literales, template literals, expresiones
regulares y comentarios indivisibles son excepciones normales del impresor;
no se partirán manualmente, porque convertiría un commit de formato mecánico
en una transformación semántica manual.

El antiguo umbral conversacional de `app.js <= 1.450 líneas` queda retirado.
Después del formato exigiría eliminar el 72,6 % de las líneas físicas y mediría
el estilo del impresor, no la complejidad. La métrica de una futura partición
debe definirse sobre el árbol ya formateado o mediante fronteras funcionales,
no contra las 1.080 líneas compactadas.

## Resultado de la auditoría

El reformateo dejó rojas 69 comprobaciones en 26 harnesses. La clasificación
se hizo con criterio estricto:

- **A**: garantía funcional real sin cobertura funcional independiente.
  Requiere sustitución antes de reformatear, partir o modularizar `app.js`.
- **B**: garantía real cubierta por otra prueba funcional independiente.
  Puede retirarse nombrando esa prueba.
- **C**: la comprobación sólo afirma texto, forma o arquitectura interna y no
  demuestra una garantía de comportamiento. Puede retirarse sin sustituto.

No se contó como B una aserción funcional coexistente dentro de la misma
prueba roja, porque no es una red independiente del extractor textual.

| Harness | Comprobación | Clase | Cobertura independiente para B |
|---|---|:---:|---|
| `analytics-final` | zero-seat map rates render as an em dash | A | — |
|  | empty analytics error does not render a visible banner | A | — |
|  | analytics view centrally hides map-only surfaces | A | — |
|  | heatmap cannot render outside the map | A | — |
|  | analytics includes a persistent-cluster table with navigation | A | — |
|  | analytics problems reuse semantic problem rows and table formatting travels with the table | C | — |
| `area-focus` | opening area does not mutate central selection | A | — |
| `bulk-selection` | Ctrl+Z and visible Undo use global hook in app | A | — |
| `cluster-background-close` | cluster click switches directly to target area outside edit mode | A | — |
|  | create and planner modes protect background closing | A | — |
| `cluster-card-content` | cluster title reserves the agreed visible-name minimum without direct action buttons | C | — |
| `cluster-card-shape` | manual edit mode exposes a resize handle only while active | A | — |
| `cluster-context-menu` | cluster cards intercept native context menus with explicit pointer coordinates | A | — |
|  | add selected is limited to same-map selection and excludes existing members | A | — |
|  | rename and edit shape reuse their explicit flows | A | — |
|  | open, merge and dissolve reuse existing flows | A | — |
|  | menu accepts explicit anchors and opens from both keyboard context-menu gestures | A | — |
|  | menu retains keyboard navigation and viewport positioning | A | — |
| `cluster-map-controls` | cluster cards move rename and adjustment actions to the contextual menu | A | — |
|  | layers are a contextual map control instead of a toolbar filter | C | — |
| `custom-cluster-card-position` | editing card consumes title and body pointer starts and blocks text selection | A | — |
|  | dragging a normal card cannot fall through to its open-cluster click | A | — |
|  | normal cluster cards expose the move handle without entering card edit mode | A | — |
|  | position is clamped in logical map space and resize remains independent | A | — |
| `custom-cluster-card-size` | right-click Edit Shape starts an active in-place session for the clicked area | A | — |
|  | resize handle pointer movement changes only draft dimensions in real time | A | — |
|  | Save persists one manual presentation record and survives rerender | A | — |
|  | Automatic reset removes manual dimensions and Ctrl+Z restores the one prior record | A | — |
|  | rendering exposes the handle and controls only for the active card without CSS size overrides | C | — |
| `detail-panel` | central close is wired to the panel button and one shared implementation | A | — |
| `detail-panel-viewport-invariance` | selection review and inspector use detail mode only | A | — |
| `keyboard-placement` | mouse Move activation creates a keyboard cursor without changing click precision | A | — |
|  | mouse Add retains its click coordinate and command path | A | — |
|  | Move has no dependency on adjacentSeat and Add uses the shared grid cursor | A | — |
| `keyboard-shortcuts` | the switch is keyboard reachable and has a visible label | A | — |
|  | editable detection stops at the first focusable path element | B | `keyboard-shortcuts-harness.js` → `editable targets do not intercept single-key shortcuts` |
|  | global Ctrl+Z reuses central undo button | A | — |
|  | non-character keyboard navigation remains outside the preference | A | — |
| `light-map-ui` | buttons invoke immediate apply without reload or viewport reset | A | — |
| `managed-area-create-workspace` | create request carries optional area context through the original createSeat command | A | — |
| `manual-cluster` | right-click menu is scoped to the map and only offers creation for two same-map workspaces | A | — |
|  | dialog requires an explicit trimmed name | A | — |
|  | conflict dialog provides available-only and move choices | A | — |
|  | creation sends workspace IDs and opens the created cluster | A | — |
|  | cluster rename uses the existing managed-area rename transaction | C | — |
|  | manual cards use persistent managed areas and ignore legacy offsets | A | — |
| `map-context-menu` | native menu is prevented on the map but not on map controls | A | — |
|  | zero selection offers map actions and one selection has no create action | A | — |
|  | add-existing dialog lists only current-map clusters | A | — |
|  | clear and remove reuse central managed-area paths | C | — |
|  | viewport positioning and keyboard controls are implemented | A | — |
| `member-inspector` | Area Detail has delegated member actions | C | — |
|  | cluster removal is explicit text | C | — |
| `selection-interaction` | background selection cleanup is centralized | C | — |
|  | pins and cards do not initiate background clearing | A | — |
|  | rectangle mode remains prior to pan/background logic | A | — |
|  | create mode and planner are protected | A | — |
|  | plan click no longer unconditionally closes detail | A | — |
| `selection-mode-visibility` | bulk visibility depends only on workspace count and never hides Select | A | — |
|  | toggle off preserves selected workspaces and Clear remains distinct | B | `selection-controller-feature-harness.js` → `leaving selection mode does not clear selected workspaces`; `clears workspace selection through the existing explicit action contract` |
| `selection-panel-cluster` | create is hidden below two selections | A | — |
|  | panel and menu share creation flow | C | — |
|  | panel add-existing shares existing dialog | C | — |
| `selection-review` | frontend uses central selection and delegated panel events | C | — |
| `toolbar` | zero and one selection hide bulk toolbar | A | — |
| `viewport` | appearance toggle snapshots and reapplies without fit or data reload | A | — |
|  | render avoids reassigning identical image source | C | — |
| `workspace-presentation` | scenario semantic source retains field distinction | C | — |
|  | problem selection routes to workspace navigation | A | — |

| Clase | Cantidad |
|---|---:|
| A — sustitución funcional necesaria | **53** |
| B — cubierta funcionalmente, retirable | **2** |
| C — forma sin garantía, retirable | **14** |
| **Total** | **69** |

## Consecuencia

El comportamiento del frontend está protegido en un 77 % por aserciones que
leen el texto de `core/app.js`: 53 de 69 comprobaciones describen garantías
reales sin prueba funcional independiente. Cualquier refactorización de
`app.js` —formato, partición o módulos— rompe la suite sin permitir distinguir
si cambió el comportamiento. Esto bloquea 6.4 y 6.5.
