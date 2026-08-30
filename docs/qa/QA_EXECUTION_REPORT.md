# QA execution report — Milestone 9

## Clasificación de evidencia

| Estado | Significado |
|---|---|
| **AUTOMATED PASS** | Harness, sintaxis, build o auditoría estática ejecutada correctamente. |
| **AUTOMATED PASS** | Harness, sintaxis, build, auditoría estática o smoke runtime por log ejecutado correctamente; especificar el tipo de evidencia. |
| **MANUAL PASS** | Revisión humana registrada explícitamente. |
| **MANUAL PENDING** | Requiere revisión humana; no se marca PASS en este documento. |
| **NOT TESTABLE** | Requiere hardware, WebView2/UI Automation, permisos SMB o interacción humana no disponible. |
| **FAILED** | Fallo reproducible contra el comportamiento esperado. Una prueba no ejecutada se clasifica como `MANUAL PENDING`, `NOT TESTABLE` o `KNOWN LIMITATION`. |
| **FIXED / RETEST PASS** | Defecto corregido y comprobación afectada repetida con éxito. |
| **KNOWN LIMITATION** | Restricción conocida o contrato no cubierto; no equivale a PASS. |

## Resultado automático

| Comprobación | Estado | Resultado |
|---|---|---|
| Dashboard harness | AUTOMATED PASS | 13/13 |
| Spatial Analytics frontend | AUTOMATED PASS | 7/7 |
| Movement Planner frontend | AUTOMATED PASS | 10/10 |
| Validation frontend | AUTOMATED PASS | 17/17 |
| Core UX | AUTOMATED PASS | 14/14 |
| SpatialAnalyticsHarness C# | AUTOMATED PASS | 9/9 |
| MovementPlannerHarness C# | AUTOMATED PASS | 11/11 |
| ScenarioDiffEngineHarness C# | AUTOMATED PASS | 10/10 |
| ValidationEngineHarness C# | AUTOMATED PASS | 9/9 |
| ReleaseReadinessHarness C# | AUTOMATED PASS | 7/7, directorio temporal aislado |
| Release readiness estático | AUTOMATED PASS | 6/6; 62 colisiones reportadas |
| Sintaxis JS propia | AUTOMATED PASS | `node --check` para app y helpers |
| Build WPF | AUTOMATED PASS | 0 warnings / 0 errors |

## Hallazgos corregidos en M9

- Sidebar compacta: abreviaturas únicas, etiquetas accesibles y tooltip, en vez
  de puntos visualmente indistinguibles.
- Controles de selección, filtros y capas: ya no desaparecen en compacto.
- Resultados de búsqueda: posición ligada al campo y recálculo tras resize.
- Popup Más: prioridad de stacking por encima de drawers compactos.
- `Escape`: cierra menú contextual y menú Más además de los estados existentes.
- Foco: mapa y `<summary>` muestran foco visible.
- Diagnóstico SVG: callbacks de una carga antigua no contaminan el resumen de
  una recarga posterior.
- Bridge: origen WebView restringido a `plano.local`; cierre no entrega replies
  tardías y desuscribe el handler.
- Backups: publicación mediante temporal validado; backups corruptos no impiden
  listar backups válidos.
- Logs: sin usuario, máquina ni rutas absolutas.
- Recursos: `.orig` no se incrusta ni publica.

## Smoke

| Comprobación | Estado | Evidencia |
|---|---|---|
| Debug M9 | AUTOMATED PASS | Smoke runtime por log: `runtime-data/logs/audit-39256.log`: `loadInitialData` 41 ms, Dashboard, Validation (14 ms), Spatial Analytics (22 ms) y SVG 5/5. |
| Published RC M9 con extracción limpia | AUTOMATED PASS | Smoke runtime por log: `runtime-data/logs/audit-9804.log`: `loadInitialData` 44 ms, Dashboard, Validation (16 ms), Spatial Analytics (18 ms), Heatmap/analytics y SVG 5/5. La carpeta `Resources` de LocalAppData se eliminó antes del arranque; no se eliminaron datos, configuración ni backups. |
| Errores JS / rechazos no controlados en los smokes M9 | AUTOMATED PASS | 0 / 0 registrados en los logs auditados. |

## Manual QA procedure

Registrar **PASS** o **FAIL**, comentario y entorno exacto. No sustituir por
capturas sin interacción.

### Screen / DPI matrix representativa

| Test | Resolución | DPI | Acciones | Resultado esperado | PASS / FAIL |
|---|---:|---:|---|---|---|
| TEST 01 | 1280×720 | 100 % | Dashboard, Plano, Lista, Problems, Escenarios y Analítica. | Sin scroll horizontal global, textos/botones completos, sidebar utilizable. | ☐ |
| TEST 02 | 1366×768 | 125 % | Abrir inspector, Planner, Más, búsqueda y Capas. | Ningún popover/drawer queda tapado o fuera de viewport. | ☐ |
| TEST 03 | 1440×900 | 150 % | Cambiar plano, zoom, filtros, heatmap, Problems y Compare. | Mapa útil, paneles legibles, sin solapamientos. | ☐ |
| TEST 04 | 1600×900 | 175 % | Dashboard, Lista y Planner completo. | Grids se reorganizan; no se recorta contenido. | ☐ |
| TEST 05 | 1920×1080 | 100 % | Flujo completo con multiselección y Scenario Compare. | Navegación, contexto y acciones principales claros. | ☐ |
| TEST 06 | 2560×1440 | 125 % | Dashboard y Analítica por plano. | Uso razonable del espacio, sin texto microscópico. | ☐ |
| TEST 07 | 3440×1440 | 100 % | Plano con heatmap, Problems y Planner. | El mapa conserva área útil; paneles no se duplican. | ☐ |
| TEST 08 | 3840×2160 | 150 % o 200 % | Todas las vistas y diálogos. | Foco, tipografía y controles legibles. | ☐ |

### Critical user flows

| Flujo | Acción | Resultado esperado | PASS / FAIL |
|---|---|---|---|
| FLOW 1 | Abrir → Dashboard → Plano. | Contexto REALIDAD/ESCENARIO claro y navegación coherente. | ☐ |
| FLOW 2 | `/` → buscar persona → Enter. | Localiza y centra el puesto sin mostrar ID técnico como referencia principal. | ☐ |
| FLOW 3 | Editar puesto en copia aislada → guardar → revalidar. | Estado guardando/guardado/error claro y resultados actualizados. | ☐ |
| FLOW 4 | Problema → Ver en plano → corregir. | Filtro, highlight, inspector y retorno correcto. | ☐ |
| FLOW 5 | Crear escenario → editar → Compare. | Reality no cambia; diff y ValidationImpact corresponden al draft. | ☐ |
| FLOW 6 | Multiselección → Planner → escenario → Compare. | ●/◎/× distinguibles; no Apply directo a Reality. | ☐ |
| FLOW 7 | Heatmap Reality → escenario. | Leyenda, escala y capa coinciden con el contexto. | ☐ |
| FLOW 8 | Cambio real de copia aislada → Undo. | Backup, historial y restauración esperada. | ☐ |
| FLOW 9 | Exportar desde Reality. | Archivo creado con el formato existente; escenario no altera la exportación. | ☐ |
| FLOW 10 | Cerrar con y sin operación terminada → reabrir. | Sin error tardío; carga, Validation, Analytics y SVG 5/5. | ☐ |

### Keyboard, focus and color-independent meaning

| Test | Acción | Resultado esperado | PASS / FAIL |
|---|---|---|---|
| KEY 01 | Tab / Shift+Tab por Dashboard. | Orden lógico y activación con Enter/Space. | ☐ |
| KEY 02 | `/`, flechas, Enter y Escape en Search. | Resultado activo visible; Escape cierra el popup. | ☐ |
| KEY 03 | Problems y Compare con flechas, Enter, Escape. | Selección y detalle coherentes. | ☐ |
| KEY 04 | Planner: origen, destino, review y override. | Foco alcanza cada acción; Escape cierra sin modificar Reality. | ☐ |
| KEY 05 | Mapa, Filtros, Capas y menú contextual. | Foco visible; Escape cierra overlays. | ☐ |
| A11Y 01 | Revisar libre/ocupado/reservado, problemas, diff y Planner. | Significado disponible por forma, símbolo, texto o borde además de color. | ☐ |

## NOT TESTABLE HERE

- Visual real en todas las combinaciones de monitor y DPI.
- Comportamiento de lector de pantalla y teclado físico completo.
- Cierre WPF por UI Automation y ausencia de procesos WebView2 hijos.
- Semántica de bloqueo y durabilidad ante fallo eléctrico/SMB real.
- Cierre iniciado por usuario/UI Automation y limpieza completa de procesos WebView2 hijos; el `timeout` del runner puede dejar el árbol de procesos de un smoke vivo.
- Permisos y retención final de carpetas compartidas.

## Manual pending

Todos los tests de matriz, flujos y teclado anteriores. `MANUAL_QA_CHECKLIST.md`
continúa siendo la lista de verificación completa por módulo.

---

# Milestone 10 — Manual QA and release sign-off

> **Estado inicial: MANUAL PENDING.** Este apartado solo prepara y registra la
> ejecución humana. Ninguna fila se marca PASS sin resultado explícito del
> ejecutor. No se corrige ningún defecto hasta que exista un FAIL confirmado.

## Identificación y precondiciones de la sesión

| Campo | Valor a registrar antes de ejecutar TEST 01 |
|---|---|
| Commit candidato | `a9197d4 milestone-9-release-hardening` |
| EXE candidato | `publish-release-candidate/PlanoOpenSpaceIT.Windows.exe` |
| Configuración efectiva | Ruta del `config.json` junto al EXE y valor de `networkRoot` |
| Datos | Ruta de una **copia aislada** de `runtime-data`; prohibido usar datos operativos en pruebas de escritura |
| Recursos | Tras iniciar: diagnóstico `SVG 5/5` y recursos extraídos correctamente |
| Entorno | Windows, monitor, resolución, DPI, WebView2 Runtime y fecha/hora |
| Ejecuta | Nombre o iniciales del ejecutor |

Para edición, movimiento, reserva, escenarios, Planner, Apply, Undo, backup,
concurrencia o export con posible escritura, confirmar primero que `networkRoot`
apunta al entorno aislado. Si no puede confirmarse, registrar **BLOCKED** y no
ejecutar la operación.

## Registro de pruebas manuales

**Convención de resultado:** escribir `PASS`, `FAIL`, `BLOCKED` o `NOT TESTABLE`.
Para un FAIL, usar `BUG-M10-###` en la columna Defecto y completar el registro de
incidencia al final de este documento. `Resultado observado` debe contener una
frase factual; la captura es opcional pero recomendable para clipping, overlap y
estado visual.

**Criterio universal:** cada prueba funcional debe comportarse correctamente en
todas las resoluciones y DPI soportados. La matriz A–G define cobertura
representativa de layout; la resolución/DPI de cada ejecución se registra como
evidencia, no limita la validez del requisito funcional.

| ID | Área | Entorno registrado | Pasos | Resultado esperado | Resultado observado | Resultado | Captura | Defecto |
|---|---|---|---|---|---|---|---|---|
| M10-01 / TEST A | Preflight, sidebar y Dashboard | 1280×720 / 100 % | Registrar precondiciones, abrir RC, esperar `SVG 5/5`, recorrer la sidebar y Dashboard. | Config aislada confirmada; sin scroll horizontal global; nombres, KPIs, cards, badges y estado activo legibles. | QA manual PASS: `networkRoot` QA confirmado, SVG 5/5 y zona inferior/statusbar completamente visible por encima de la taskbar. | PASS CONFIRMADO — manual retest | Captura QA | BUG-M10-001 CLOSED |
| M10-02 / TEST B | Compacto, búsqueda, menús y drawers | 1366×768 / 125 % | Abrir Plano, Selección, Filtros, Capas, Más y búsqueda; redimensionar una vez; cerrar con Escape. | Controles alcanzables; popup dentro del viewport; ningún drawer/popover queda oculto; Escape cierra el overlay correcto. | QA manual PASS: sidebar, Search, overlays, drawers y Escape correctos; sin overflow horizontal ni regresión de BUG-M10-001. | PASS CONFIRMADO — manual | — | — |
| M10-03 / TEST C | Mapa y cinco SVG | Universal; entorno representativo recomendado: 1920×1080 / 100 % | Abrir Norte, Nivel 3, Sur, I+D y QC; pan, zoom, zoom semántico, selección, hover, inspector, maximizar/restaurar y Snap. | Cinco mapas completos; mapa no deformado; pin seleccionable con hitbox útil; inspector y contexto permanecen coherentes. | Pendiente | PENDING | — | — |
| M10-04 / TEST D | Lista, filtros y multiselección | 1920×1080 / 150 % | Alternar Todos/Ocupados/Libres/Reservados/Incompletos/Problemas; aplicar filtro avanzado, chips, limpiar; usar Ctrl/Shift en mapa y lista. | Lógica AND, conteos y sincronización mapa/lista correctos; columnas legibles; rango Shift y multiselección coherentes. | Pendiente | PENDING | — | — |
| M10-05 / TEST E | Dashboard y analítica espacial | 2560×1440 / 125 % | Revisar Dashboard, KPIs, problemas, ranking, widget de escenario, tabla por plano y navegación a Analítica. | Datos derivados legibles; barras y porcentajes comprensibles; navegación correcta; sin texto microscópico ni clipping. | Pendiente | PENDING | — | — |
| M10-06 / TEST F | Heatmap, Problemas y Planner en ancho amplio | 3440×1440 / 100 % | Activar cada modo Heatmap, capa Problemas, seleccionar puestos y abrir Planner desde multiselección. | Leyenda y selector visibles; pines siguen clicables; mapa conserva área útil; no se superponen paneles. | Pendiente | PENDING | — | — |
| M10-07 / TEST G | Todas las vistas y diálogos | 3840×2160 / 200 % | Recorrer Dashboard, Plano, Lista, Problemas, Escenarios, Compare, Analítica, Historial, diálogo y drawer disponibles. | Tipografía, foco y controles legibles; grids se reorganizan sin compresión, solapamiento ni menús cortados. | Pendiente | PENDING | — | — |
| M10-08 | Display location, especialmente Sur | TEST C; Sur en zoom global/operativo/detalle | Buscar ubicaciones, seleccionar puestos colindantes, inspeccionar lista e inspector; repetir en Nivel 3. | `displayLocation` es la etiqueta humana principal; IDs técnicos no lo son; las 62 colisiones se clasifican A/B/C sin cambiar la rejilla. | Pendiente | PENDING | — | — |
| M10-09 | Search | TEST B | Usar `/`; buscar persona, equipo, roseta y `displayLocation`; flechas, Enter y Escape. | Texto íntegro; popup visible; resultado centra mapa, selecciona puesto y abre inspector correcto. | Pendiente | PENDING | — | — |
| M10-10 | Centro de Problemas | TEST D | Abrir Problemas; filtrar Critical/Warning/Info, texto, plano y entidad; abrir detalle, relacionados, Ver en plano y Revalidar. | Filtros y detalle coherentes; highlight, inspector y lista corresponden al problema; revalidación actualiza el estado. | Pendiente | PENDING | — | — |
| M10-11 | Edición y validación | Cualquier matriz, **copia aislada** | Editar un dato seguro: seleccionar → editar → dirty → guardar → saving → saved; comprobar Dashboard/Analytics/Problems. | Persistencia y toast claros; Validation, Dashboard y Analytics se actualizan sin recargar. | Pendiente | PENDING | — | — |
| M10-12 | Conflicto de validación | Cualquier matriz, **copia aislada** | Crear una roseta duplicada conocida; guardar, localizar el problema y corregirlo. | Validation detecta la condición; Problems/mapa reflejan el resultado; al corregir y guardar desaparece. | Pendiente | PENDING | — | — |
| M10-13 | Escenarios y Compare | TEST D, **copia aislada** | Crear escenario, editar draft, comprobar Reality, Compare, Added/Removed/Moved/Modified, before/after, filtros y Problems. | Reality no cambia; contexto Scenario inequívoco; diff y ValidationImpact corresponden al draft. | Pendiente | PENDING | — | — |
| M10-14 | Movement Planner | TEST F, **copia aislada** | Multiselección → Planificar → destinos → propuesta → override → crear escenario → Compare. | Source/destination/blocked/unassigned distinguibles; `displayLocation` visible; Reality intacta; escenario y Compare correctos. | Pendiente | PENDING | — | — |
| M10-15 | Heatmap Reality / Scenario | TEST F, escenario aislado | Alternar Ocupación, Disponibilidad, Problemas y Cambios de escenario; usar pan/zoom/clics y activar Problemas. | Leyenda accesible; overlay no bloquea pines; capa no tapa el plano; Planner lo oculta visualmente cuando procede. | Pendiente | PENDING | — | — |
| M10-16 | Undo e Historial | Cualquier matriz, **copia aislada** | Ejecutar una operación soportada y Undo; revisar History, Validation, Analytics y Dashboard. | Estado, resultados derivados e historial vuelven al resultado esperado. | Pendiente | PENDING | — | — |
| M10-17 | Export existente | Cualquier matriz, **copia aislada** | Exportar Reality, abrir el fichero y revisar hojas/contenido/formato; revisar log resultante. | Archivo abre correctamente, conserva formato esperado, no incorpora datos inesperados ni rutas absolutas en logs. | Pendiente | PENDING | — | — |
| M10-18 | Cierre y reapertura | TEST C | Cerrar mediante X real, esperar un margen razonable, comprobar procesos y reabrir el RC. | Ventana y proceso principal terminan; no persisten hijos WebView2 de esa instancia; reapertura carga datos y SVG 5/5 sin error tardío. | Pendiente | PENDING | — | — |
| M10-19 | Concurrencia cooperativa | Dos instancias/equipos, **share aislado** | A abre; B abre; A modifica/guarda; B modifica después. Registrar exactamente el resultado. | No declarar seguridad adicional: documentar comportamiento de `.lock`, revisión, `commit.pending` y cualquier conflicto/último escritor. | Pendiente | PENDING | — | — |
| M10-20 | Share inaccesible / permisos | Share aislado | Simular permiso denegado o share no disponible durante lectura/escritura segura. | Error visible; no se crea dataset vacío ni se sobrescriben datos; aplicación permanece estable. Si no puede simularse: NOT TESTABLE. | Pendiente | PENDING | — | — |
| M10-21 | Significado sin color y teclado | TEST B y TEST D | Revisar estados Libre/Ocupado/Reservado, severidades, diff y Planner; probar Tab, Shift+Tab, flechas, Enter, Space y Escape en Search, Lista, Problems, Compare, Planner, drawers y diálogos. | Cada estado se entiende por texto/símbolo/forma/borde además de color; foco y orden de teclado son coherentes. | Pendiente | PENDING | — | — |

### Clasificación de colisiones de `displayLocation`

Registrar el resultado de M10-08 por plano y zoom. No corregir automáticamente.

| Plano | Colisiones conocidas | Global | Operativo | Detail | Clasificación A/B/C | Observación |
|---|---:|---|---|---|---|---|
| Norte | 0 | Pendiente | Pendiente | Pendiente | — | — |
| Nivel 3 | 5 celdas / 10 puestos | Pendiente | Pendiente | Pendiente | — | — |
| Sur | 57 celdas / 136 puestos | Pendiente | Pendiente | Pendiente | — | — |
| I+D | 0 | Pendiente | Pendiente | Pendiente | — | — |
| QC | 0 | Pendiente | Pendiente | Pendiente | — | — |

- **A:** no visible o aceptable.
- **B:** molesto, pero usable.
- **C:** ambiguo o impide identificar el puesto.

## Registro de defectos

```text
BUG-M10-###
Severidad: BLOCKER | HIGH | MEDIUM | LOW
Área:
Build/commit:
Entorno: resolución, DPI, monitor, Debug/RC y contexto Reality/Scenario
Precondición de datos: copia aislada / no destructivo
Pasos para reproducir:
Resultado esperado:
Resultado observado:
Captura:
Archivo sospechoso (si se conoce):
Estado: OPEN | FIXED | RETEST PASS | RETEST FAIL
Prueba afectada y retest ejecutado:
```

### BUG-M10-001

```text
Severidad: MEDIUM
Área: Window layout / responsive / bottom status area
Build/commit: a9197d4 con RC republicada desde el fix local
Entorno: 1280×720 @ 100 % DPI, taskbar visible
Precondición de datos: `networkRoot` QA confirmado en `qa-runtime-data`; prueba no destructiva
Pasos: abrir RC maximizado y revisar la zona inferior de Dashboard/aplicación
Resultado esperado: mapa, paneles, controles inferiores y statusbar quedan por encima de la taskbar
Resultado observado: QA manual confirma SVG 5/5 y statusbar/zona inferior completamente visibles por encima de la taskbar
Estado: CLOSED — PASS AFTER MANUAL RETEST
Prueba afectada y retest: M10-01 / TEST A — PASS
```

- **BLOCKER:** pérdida/corrupción de datos, Scenario modifica Reality, arranque
  imposible, guardado inseguro o Apply incorrecto.
- **HIGH:** función principal inutilizable, pantalla inaccesible o flujo
  Planner/Scenario roto.
- **MEDIUM:** clipping/overlap importante o teclado parcialmente roto.
- **LOW:** alineación, copy o detalle visual no bloqueante.

Ante un FAIL confirmado: reproducir solo ese caso, corregir la causa raíz con el
cambio mínimo, ejecutar los tests automáticos relacionados, compilar y repetir
únicamente las filas manuales afectadas. No reabrir el alcance funcional.

## Criterio de decisión

- **READY FOR PILOT:** sin BLOCKER/HIGH abiertos, pruebas críticas manuales con
  PASS y limitaciones conocidas registradas.
- **READY WITH KNOWN ISSUES:** solo defectos LOW/MEDIUM aceptados explícitamente
  y un plan de corrección posterior.
- **NOT READY:** existe BLOCKER/HIGH, resultados críticos sin ejecutar o la
  copia aislada no puede verificarse para operaciones de escritura.

No declarar `PRODUCTION READY` mientras no se haya validado manualmente la
operación sobre share/SMB y el comportamiento de concurrencia en el entorno
objetivo.

---

# M10 — evidencia técnica ejecutada (2026-08-23)

## Corrección de evidencia

| Comprobación | Estado | Evidencia ejecutada |
|---|---|---|
| PUBLISHED RC STATIC/PACKAGING VERIFICATION | AUTOMATED PASS | RC regenerada con `dotnet publish PlanoOpenSpaceIT.Windows.csproj -c Release -r win-x64 --self-contained true -o publish-release-candidate` tras la corrección visual de BUG-M10-002. |
| Configuración de RC | AUTOMATED PASS | `publish-release-candidate/config.json` se verificó tras publicar: `networkRoot` apunta exclusivamente a `G:\Proyecto Planos\phm\phm\uifigmastyle_UX_REDESIGN\qa-runtime-data`. No se cambió `config.json` de desarrollo. |
| SVG RESOURCES EMBEDDED | AUTOMATED PASS | Release-readiness static harness: recursos esperados 5/5. Esto demuestra empaquetado, no carga runtime. |
| Published RC runtime smoke | AUTOMATED PASS | RC iniciada realmente desde `publish-release-candidate/PlanoOpenSpaceIT.Windows.exe`, sin operaciones de edición, Scenario, Apply, Planner ni export. `qa-runtime-data/logs/audit-40392.log` y `audit-16940.log`, build `a0efcee4-4356-4d2f-b16a-f97606354cd5`: `lifecycle.start`, `loadInitialData` success (40/69 ms), Dashboard, Validation, Spatial Analytics y Heatmap inicializados, y `SVG 5/5`. El runner agotó 15 s; no quedó proceso `PlanoOpenSpaceIT.Windows.exe` tras la prueba. |
| Runtime SVG evidence | AUTOMATED PASS | Ambos smokes son preloads, no un recorrido UI entre mapas. En los dos logs del build actual aparecen individualmente Norte, Nivel 3, Sur, I+D y QC en conjunto (`norte`, `nivel3`, `sur`, `id`, `qc`) y cada arranque registra el agregado `SVG 5/5`. |
| JS runtime diagnostics | AUTOMATED PASS (bounded) | Auditoría de 320 entradas JSON de `qa-runtime-data/logs`: 0 `JavaScript error:`, 0 `Unhandled rejection:`, 0 nivel `error`. Límite: los listeners no cubren errores anteriores a la carga de `app.js` ni posteriores al cierre. |
| Logging privacy | AUTOMATED PASS | La misma auditoría no encontró rutas absolutas `G:\`/`C:\Users`, ni claves de usuario/máquina/host en los logs QA. |

## MAP VISUAL INTEGRATION

**Estado: FIXED IN CODE / MANUAL RETEST PENDING (BUG-M10-002).** La legibilidad manual confirmó que el filtro aplicado a la imagen completa degradaba detalles arquitectónicos en temas alternativos.

1. **Estrategia actual:** variables de tema en el host + workspace + canvas técnico neutral/claro + marco + grid + marcadores externos. No se alteró geometría, IDs, `viewBox`, recursos ni mecanismo de carga.
2. **Limitación real:** al cargarse los cinco SVG como `<img>`, las variables CSS host no penetran fills, strokes ni textos internos. Un filtro afecta la imagen completa; por esta razón no se usa para tematizar arquitectura.
3. **Variables activas:** `--workspace-bg`, `--map-surface`, `--map-border`, `--map-text`, `--map-muted`, `--map-svg-filter`, `--map-svg-opacity`, `--map-svg-heatmap-opacity`, `--map-svg-planner-opacity`.
4. **Variables no activas:** `--map-wall` y `--map-line` fueron eliminadas; quedan documentadas conceptualmente como RESERVED / NOT ACTIVE con `<img>`.
5. **Filtros por tema:** `professional-light`, `penpot-dark`, `high-contrast` y `projector` definen `--map-svg-filter: none`; `#plan` usa el único punto centralizado `filter: var(--map-svg-filter, none)`.
6. **Canvas por tema:** light `#fafbfc`; dark UI con canvas técnico claro `#e7e9ed`; high contrast y projector `#ffffff`. El workspace conserva su tema propio.
7. **Heatmap y Planner:** opacidad arquitectónica .86/.88 en light/dark y .90/.92 en high-contrast/projector. Planner conserva prioridad de cascada; el overlay Heatmap y `#pins` mantienen `pointer-events: none`; `.pin` mantiene `pointer-events: auto`.
8. **Semantic zoom:** GLOBAL .90; OPERATIVE 1; DETAIL 1, para preservar el máximo detalle arquitectónico al acercar.
9. **Cambio de mapa/fondo:** `#stage` conserva `--map-surface`, de modo que la superficie host permanece durante el intercambio de `src`; la ausencia de flash visual requiere revisión manual.
10. **SVG:** no modificados. La evidencia runtime 5/5 previa sigue siendo válida para los recursos, pero la nueva presentación requiere retest visual humano.
11. **Errores:** Node, harnesses solicitados y build pasan tras el fix.
12. **MAP APPEARANCE MANUAL QA: PENDING:** retest de Professional Light, Penpot Dark, High Contrast y Projector sobre el mismo plano.

## Functional QA M10 — automatización ejecutada y límites

| Área | Estado | Evidencia / límite |
|---|---|---|
| Search | AUTOMATED PASS (algoritmos) / MANUAL PENDING (UI) | `core-ux-harness.js` 14/14 incluye normalización, ranking y activación contractual; `/`, flechas, Enter, Escape, centering, popup y foco real requieren WebView/UI. |
| Filters y List | AUTOMATED PASS (contratos) / MANUAL PENDING (UI) | AND, chips, clear y rango Shift están cubiertos por Core UX; quick filters, conteos, sincronización mapa/lista y multiselección UI no se automatizaron. |
| Problems y Validation | AUTOMATED PASS (motor/helpers) / MANUAL PENDING (UI) | `ValidationEngineHarness` 9/9 y `validation-harness.js` 17/17 cubren reglas existentes y contratos. Problems Center, detalle, relacionados, highlight y flujo editar/corregir requieren UI. |
| Editing / persistencia | MANUAL PENDING | El harness integrado ejercita Apply/Undo aislados, no cada mutación directa Reality ni una reapertura UI. |
| Scenarios / Compare | AUTOMATED PASS (motor) / MANUAL PENDING (UI) | `ScenarioDiffEngineHarness` 10/10 y `ReleaseReadinessHarness` cubren Reality intacta antes de Apply, diff, validation e impacto. Filtros y navegación Compare siguen pendientes UI. |
| Partial Apply | AUTOMATED PASS | `ReleaseReadinessHarness` 9/9 en carpeta temporal: exactamente un cambio seleccionado llega a Reality; el no seleccionado no llega, queda pendiente en diff; backup e historial creados. |
| Scenario concurrency | AUTOMATED PASS | Nueva prueba temporal: una mutación Reality posterior vuelve obsoleto `baseRevision`; Apply se rechaza y Reality queda byte-a-byte intacta, manteniendo el draft. |
| Planner | AUTOMATED PASS (motor/helpers) / MANUAL PENDING (UI) | C# 11/11, JS 10/10 e integración temporal prueban propuesta, destinos inválidos, overrides contractuales y creación de Scenario sin mutar Reality. |
| Heatmap | AUTOMATED PASS (motor/helpers/CSS contract) / MANUAL PENDING (UI) | C# 9/9, JS 7/7 y static harness verifican datos/modos/escalas/prioridad/pointer events; render, clics y pan/zoom reales pendientes. |
| Analytics / Dashboard | AUTOMATED PASS (motor/helpers) / MANUAL PENDING (UI) | Spatial C# 9/9 y Dashboard JS 13/13; cards, navegación y refresh visual real pendientes. |
| Undo / Backup / History | AUTOMATED PASS (flujo mínimo aislado) / MANUAL PENDING (matriz) | Apply genera backup/event y Undo real restaura estado en fixture. No hay matriz de cadenas, restore UI o ZIP corrupto. |
| Export | MANUAL PENDING | No se ejecutó export del RC; no se afirma openabilidad XLSX sin prueba aislada específica. |
| Shutdown / reopen | NOT TESTABLE | El smoke confirma que el proceso no quedó vivo; no fue posible accionar X/UI Automation ni demostrar cierre normal + reapertura controlados. `audit-40392.log` contiene un cierre de otra sesión del mismo build, pero no se usa como prueba de esta secuencia. |
| Reality concurrency | KNOWN LIMITATION | Lock cooperativo y revisión global existen, pero mutaciones directas Reality no reciben `expectedRevision`; no se declara concurrency-safe. La contención con dos clientes/SMB sigue MANUAL PENDING. |
| Corrupt JSON | MANUAL PENDING | El código propaga JSON inválido y evita crear `maps.json` vacío, pero no se ejecutó matriz de JSON inválido/truncado/ausente ni recuperación de `commit.pending`. |
| File lock / permissions | NOT TESTABLE | Sin share/permisos aislados reproducibles en este runner. |
| Network failure | NOT TESTABLE | No hay share QA desconectable disponible; no se simuló SMB. |
| Keyboard / focus | AUTOMATED PASS (estático) / MANUAL PENDING | Static harness verifica hooks Escape y foco visible; recorrido/foco físico, lectores de pantalla y experiencia humana siguen pendientes. |
| DisplayLocation | KNOWN LIMITATION | Static harness detecta 62 colisiones: 5 celdas / 10 puestos Nivel 3; 57 celdas / 136 puestos Sur. Revisión de usabilidad manual, sin cambiar la rejilla. |

## Regresión final ejecutada

| Suite | Estado | Resultado |
|---|---|---|
| Dashboard frontend | AUTOMATED PASS | 13/13 |
| Spatial Analytics frontend | AUTOMATED PASS | 7/7 |
| Movement Planner frontend | AUTOMATED PASS | 10/10 |
| Validation frontend | AUTOMATED PASS | 17/17 |
| Core UX | AUTOMATED PASS | 14/14 |
| Release readiness static | AUTOMATED PASS | 16/16 |
| ReleaseReadinessHarness C# | AUTOMATED PASS | 9/9 |
| SpatialAnalyticsHarness C# | AUTOMATED PASS | 9/9 |
| MovementPlannerHarness C# | AUTOMATED PASS | 11/11 |
| ScenarioDiffEngineHarness C# | AUTOMATED PASS | 10/10 |
| ValidationEngineHarness C# | AUTOMATED PASS | 9/9 |
| Node checks | AUTOMATED PASS | `app.js`, cuatro helpers y `release-readiness-harness.js` |
| Build | AUTOMATED PASS | `dotnet build PlanoOpenSpaceIT.Windows.csproj --no-restore`: 0 warnings, 0 errors |

## Defectos M10

### BUG-M10-002

- **Severidad:** HIGH.
- **Área:** Map Visual Integration / Themes.
- **Resultado observado por QA manual:** los filtros aplicados a `<img id="plan">` empastaban colores y reducían la legibilidad de paredes, divisiones, mobiliario, textos y detalles arquitectónicos en temas alternativos.
- **Causa raíz:** un único filtro CSS transforma simultáneamente todos los píxeles del SVG externo; al ser `<img>`, CSS no puede corregir selectivamente sus elementos internos.
- **Corrección:** `--map-svg-filter: none` en los cuatro temas; canvas técnico claro/neutral separado del workspace temático; opacidades Heatmap/Planner elevadas; GLOBAL/OPERATIVE/DETAIL ajustados a .90/1/1. SVGs sin cambios.
- **Automated retest:** `FIXED IN CODE` — Node checks, Core UX 14/14, Spatial 7/7, Planner 10/10, Release Readiness 16/16 y build 0/0.
- **Estado:** **MANUAL RETEST PENDING**. No marcar PASS hasta revisión humana de los cuatro temas.

**Nota de packaging corregida:** después de cada publish, el `config.json` adyacente a la RC se restablece exclusivamente a `qa-runtime-data`; la configuración de desarrollo no se modifica.

## Conclusión M10

**Resultado sustituido por la reclasificación técnica posterior.** No se reprodujo ningún defecto funcional automático; las pruebas pendientes se registran separadamente como `MANUAL PENDING`, `NOT TESTABLE` o `KNOWN LIMITATION`.

- **VISUAL RESOLUTION/DPI QA: PENDING**
- **MAP APPEARANCE MANUAL QA: PENDING**
- **DISPLAYLOCATION COLLISION USABILITY QA: PENDING**

---

## Pausa de Functional QA — correcciones visuales pendientes de retest

### BUG-M10-003

- **Severidad:** HIGH.
- **Área:** Map visual integration / legibilidad arquitectónica.
- **Evidencia manual:** Open Space Norte se percibía lavado, con líneas, mobiliario y divisiones compitiendo con la cuadrícula.
- **Corrección:** se eliminó la atenuación de arquitectura en GLOBAL (GLOBAL/OPERATIVE/DETAIL usan opacidad base); la cuadrícula bajó de `.28` a `.14`; etiquetas de grid pasaron a 9 px, `.84` y fondo/borde menos invasivos. Se mantuvo `filter: none` y ningún SVG fue modificado.
- **Estado:** **FIXED IN CODE / MANUAL RETEST PENDING**.

### BUG-M10-004

- **Severidad:** MEDIUM/HIGH.
- **Área:** Seat markers / user pins / map contrast.
- **Evidencia manual:** las chinchetas tenían contraste insuficiente sobre el canvas técnico claro.
- **Corrección:** variables temáticas `--pin-user-ring`, `--pin-user-halo` y `--pin-user-shadow`; aro oscuro, halo claro y sombra sutil en la base del pin; verdes/ámbar de completitud más firmes. Problems, Planner, Scenario, Selection y Search mantienen reglas de overlay y z-index específicas; no se modificaron `pointer-events`.
- **Estado:** **FIXED IN CODE / MANUAL RETEST PENDING**.

**Retest técnico:** Node checks PASS; Core UX 14/14, Spatial 7/7, Planner 10/10 y Release Readiness 16/16 PASS; build 0 warnings / 0 errors; RC republicada con `networkRoot` QA. Functional QA continúa pausada.

---

## MAP VISUAL INTEGRATION rollback

**MAP VISUAL INTEGRATION: ROLLED BACK.**

Motivo: **The theme-driven map presentation reduced architectural legibility and was rejected during manual QA.**

- **BUG-M10-002:** `ROLLED BACK / VISUAL APPROACH ABANDONED`.
- **BUG-M10-003:** `ROLLED BACK / VISUAL APPROACH ABANDONED`.
- **BUG-M10-004:** `ROLLED BACK / VISUAL APPROACH ABANDONED`.

El rollback es selectivo: se retiraron variables, filtros/opacidades, canvas/frame, grid/labels y estilo de pins introducidos para la integración visual; también se retiraron las clases `show-heatmap` y `show-planner` que existían exclusivamente para esa presentación. No se revirtieron los cambios de BUG-M10-001, funcionalidades ni datos. Los SVG permanecen sin modificaciones.

**Estado:** `READY FOR MANUAL RETEST AFTER MAP APPEARANCE ROLLBACK`.

---

## Pin state system

**PIN STATE LOGIC: AUTOMATED PASS**

- `pin-state-harness.js`: 22/22.
- Business, quality, interaction, Problems, Scenario and Planner presentation is derived by the pure `derivePinPresentation` helper.
- Presentation uses data attributes instead of concatenated state classes; no persistent state or backend contract was added.
- The helper verifies deterministic output, input immutability, Reality/Scenario isolation, semantic symbols, priority and displayLocation-first ARIA.
- CSS static assertions verify business fills are separate from selected/problem overlays, Planner blocked/source/destination selectors and focus-visible support.

**PIN VISUAL CONTRAST: MANUAL QA PENDING**

Manual review is required for Free, Occupied, Reserved, Selected, Search Hit, Critical, Warning, Info, Scenario and Planner Source/Destination/Blocked on the original map appearance.

---

## Workspace identity and presentation semantics

**WORKSPACE PRESENTATION LOGIC: AUTOMATED PASS**

- Read-only `qa-runtime-data` audit: 270 workspaces; 112 with current person ID; 158 without; 268 nonblank references; 201 global `historical-occupied-without-assignment` markers (Norte 22, Nivel 3 28, Sur 151).
- `seat.name` is now presentation reference only. A current person is derived only from assignment `personId`, with the existing map-level `personId` fallback; no person is inferred from text.
- `buildWorkspacePresentation` is pure and is reused for inspector, tooltip, list, search and Planner text.
- Problems resolve targets by `mapId + technical workspaceId`, update central workspace selection through navigation, highlight the corresponding pin and render the workspace inspector.
- `workspace-presentation-harness.js`: 17/17; `pin-state-harness.js`: 22/22.

**WORKSPACE SEMANTICS MANUAL QA: PENDING**

Manual review is required for legacy references without assignments, Problems-to-inspector synchronization, Scenario Compare wording and all map contexts.

---

## Functional QA M10 — reclasificación y evidencia final

| Área | Estado | Evidencia / límite |
|---|---|---|
| Search, Filters, List | AUTOMATED PASS | Core UX 14/14 y Workspace Presentation 17/17. Popup, teclado físico y presentación renderizada: MANUAL PENDING. |
| Problems / Validation | AUTOMATED PASS | Validation frontend 17/17, ValidationEngine C# 9/9 y resolución técnica `mapId + workspaceId` cubierta por el harness de presentation. |
| Editing / persistencia | AUTOMATED PASS (aislado) | ReleaseReadiness 15/15 cubre mutación aislada, Apply parcial, backup/history, Undo y guardado rechazado tras carga inválida. UI edit/reload: MANUAL PENDING. |
| Scenarios / Compare / Partial Apply | AUTOMATED PASS | ScenarioDiff 10/10 y ReleaseReadiness 15/15. Solo los cambios seleccionados llegan a Reality; Apply con `baseRevision` obsoleta se rechaza sin mutar Reality. |
| Planner | AUTOMATED PASS | Frontend 10/10 y C# 11/11; la creación del Scenario no modifica Reality. |
| Heatmap / Analytics / Dashboard | AUTOMATED PASS | Spatial frontend 7/7 y C# 9/9; Dashboard 13/13. Render y navegación UI: MANUAL PENDING. |
| Undo / Backup / History | AUTOMATED PASS (aislado) | Apply genera backup/event y Undo restaura el fixture. Matriz manual de restore/UI: MANUAL PENDING. |
| Export structural QA | AUTOMATED PASS | ReleaseReadiness 15/15 genera XLSX en TEMP: fichero no vacío, ZIP/OOXML, `workbook.xml`, relaciones, tres worksheets y contenido de ocupación de fixture; los logs no exponen la ruta absoluta temporal. Apertura/formato visual en Excel: MANUAL PENDING. |
| JSON inválido / truncado / obligatorio ausente | AUTOMATED PASS | ReleaseReadiness 15/15 verifica error explícito, sin dataset vacío y hashes de todos los JSON del fixture sin cambios. |
| Save after failed load | AUTOMATED PASS | ReleaseReadiness 15/15 verifica que una carga inválida seguida de `SaveAssignment` se rechaza y no publica un fallback vacío. |
| File lock local | AUTOMATED PASS | ReleaseReadiness 15/15 mantiene `.lock` con `FileShare.None` desde proceso separado; la mutación directa agota el retry, falla y no publica JSON ni temporales. |
| Permission denied | NOT TESTABLE | No se alteraron ACL de desarrollo ni del QA runtime; falta un entorno ACL aislado representativo. |
| Network / SMB failure | NOT TESTABLE | No hay share QA desconectable. No se simula SMB local como sustituto. |
| Shutdown normal / WebView2 cleanup / reopen | AUTOMATED PASS (scripted) | `tests/runtime-lifecycle-harness.ps1`: `CloseMainWindow()` para PID RC 41896 y 43228, `lifecycle.closing`, 0 hijos WebView2 supervivientes por árbol y reapertura con loadInitialData, Validation, Analytics y SVG 5/5. Cierre mediante X real/UI Automation: MANUAL PENDING. |
| Reality concurrency | KNOWN LIMITATION | Las mutaciones directas de Reality se serializan cooperativamente, pero su contrato no proporciona `expectedRevision` optimista completo. **NOT VERIFIED AS CONFLICT-SAFE**. |
| Scenario concurrency | AUTOMATED PASS | `baseRevision` obsoleta rechaza Apply y Reality permanece byte-a-byte intacta. |
| Logging privacy | AUTOMATED PASS (bounded) | La exportación aislada no registró rutas absolutas temporales; los logs QA auditados mantienen 0 errores/rechazos frontend y 0 entradas `error`. |
| Published RC runtime smoke | AUTOMATED PASS | `runtime-lifecycle-harness.ps1` ejecutó dos arranques reales contra `qa-runtime-data`; cada uno alcanzó loadInitialData, Validation, Analytics y SVG 5/5. |

### Clasificación de salida

- **AUTOMATED FUNCTIONAL QA: PASS** — no se reprodujo un defecto funcional ni un blocker en los contratos, persistencia aislada, Scenario, Planner, Validation, Analytics, Dashboard, export estructural o ciclo de vida automatizado.
- **AUTOMATED STORAGE SAFETY QA: PASS** — export, carga corrupta/truncada/ausente, save-after-failed-load y lock local están cubiertos con directorios temporales. Permisos ACL y SMB quedan fuera de este entorno.
- **FUNCTIONAL DEFECT BLOCKER: NONE REPRODUCED.**
- **M10 COMPLETE QA: INCOMPLETE** — quedan revisiones humanas y entornos no reproducibles; no constituyen un `FAILED` automático.

- **WORKSPACE SEMANTICS MANUAL QA: PENDING**
- **PIN VISUAL CONTRAST: PENDING**
- **VISUAL RESOLUTION/DPI QA: PENDING**
- **DISPLAYLOCATION COLLISION USABILITY QA: PENDING**
- **Excel visual opening, rendered keyboard/focus and X/UI Automation close: MANUAL PENDING**

---

## M10 — operational Problems policy

**Estado: AUTOMATED PASS / MANUAL PROBLEMS QA PENDING.**

`ValidationResult` incorpora una clasificación explícita. Las reglas activas se
clasifican como `Operational`; `historical-occupied-without-assignment` se
conserva como `Historical`/`operational: false` para diagnóstico interno, pero
queda excluida de la proyección operacional centralizada.

| Regla | Severidad | Clasificación | Visible como problema operativo |
|---|---|---|---|
| `duplicate-network-outlet` | Critical | Operational | Sí |
| `duplicate-device` | Critical | Operational | Sí |
| `duplicate-person` | Warning | Operational | Sí |
| `assignment-missing-workspace` | Critical | Operational | Sí |
| `invalid-coordinate` | Critical | Operational | Sí |
| `historical-occupied-without-assignment` | Info | Historical | No |

La proyección `ValidationEngine.OperationalResults` alimenta DataStore,
summary/bridge, Spatial Analytics, heatmap y los flujos de Scenario/Planner.
El frontend vuelve a aplicar la misma proyección centralizada de
`validation-helpers.js` antes de construir Problems, inspector, badges,
Dashboard y filtros; no hay filtros por `ruleId` dispersos en UI.

### Auditoría read-only de `qa-runtime-data`

| Métrica | Antes (todas las detecciones) | Después (operacionales) |
|---|---:|---:|
| Problems total | 201 | 0 |
| Critical | 0 | 0 |
| Warning | 0 | 0 |
| Info | 201 | 0 |

Los 201 resultados excluidos son los diagnósticos históricos: Norte 22, Nivel
3 28 y Sur 151; I+D y QC 0. No se modificaron JSON, seats ni assignments.

| Plano | Problems operativos después |
|---|---:|
| Norte | 0 |
| Nivel 3 | 0 |
| Sur | 0 |
| I+D | 0 |
| QC | 0 |

### Evidencia

- `ValidationEngineHarness`: 10/10, incluida detección interna y clasificación
  histórica no operacional.
- `validation-harness.js`: 18/18, incluida exclusión en summary, índices,
  Problems Center y conteos por plano.
- `SpatialAnalyticsHarness`: 10/10, incluida exclusión de totales y puntos de
  heatmap Problems.
- `dashboard-harness.js`: 14/14, incluida exclusión de totals, lista y por mapa.
- RC republicada con `networkRoot` exclusivo a `qa-runtime-data`.
- Smoke RC `qa-runtime-data/logs/audit-43284.log`: Validation 0/0/0 y Spatial
  Analytics `problems: 0`, con loadInitialData y SVG 5/5 correctos.

**READY FOR MANUAL PROBLEMS QA**

---

## BUG-M10-006 — global effective workspace-state audit

**Estado: FIXED IN CODE / MANUAL GLOBAL EFFECTIVE STATE QA PENDING.**

### Política central

`SeatStates.DeriveEffectiveWorkspaceState(seat, assignment)` y su espejo puro
`workspace-state-helpers.js` determinan el estado operativo:

1. reserva explícita → `Reserved` / modo `Manual`;
2. override manual explícito (`free`/`occupied`) → estado manual;
3. en automático, `assignment.personId`, con fallback existente
   `seat.personId`, → `Occupied`;
4. sin persona actual → `Free`.

`seat.type` sigue siendo solo un atributo del dibujo heredado y nunca determina
ocupación operativa. La misma proyección se aplica en DataStore, Spatial
Analytics/Heatmap, presentación de workspace, inspector, tooltip, search,
lista, filtros y estado base del pin. No se modificaron SVG, mapa, grid,
referencias, personas ni JSON.

### Auditoría read-only de `qa-runtime-data`

| Métrica | Antes | Después |
|---|---:|---:|
| Total workspaces | 270 | 270 |
| Free | 260 | 158 |
| Occupied | 10 | 112 |
| Reserved | 0 | 0 |
| Free + persona actual | 102 | 0 |
| Occupied + sin persona actual | 0 | 0 |
| Reserved + assignment | 0 | 0 |

Los 102 casos corregidos eran Norte 10, Nivel 3 17 y Sur 75. Todos los 270
puestos están en modo automático; no existen overrides manuales persistidos en
el fixture. El listado completo (`mapId`, ID técnico, `displayLocation`,
reference, persona, estado anterior/nuevo y motivo) se genera de forma
reproducible en `../architecture/WORKSPACE_EFFECTIVE_STATE_AUDIT.md` mediante
`tests/workspace-state-audit.js`.

| Plano | Free después | Occupied después | Reserved después |
|---|---:|---:|---:|
| Norte | 23 | 10 | 0 |
| Nivel 3 | 27 | 18 | 0 |
| Sur | 108 | 84 | 0 |
| I+D | 0 | 0 | 0 |
| QC | 0 | 0 | 0 |

### Evidencia

- `workspace-state-harness.js`: 18/18; cubre automático, dibujo heredado,
  reserva/manual, IDs coincidentes en mapas distintos, filtros, pin,
  inspector, analítica, Dashboard, Heatmap, Reality/Scenario, determinismo e
  inmutabilidad.
- `workspace-presentation-harness.js`: 17/17; un marcador histórico sin
  persona es `Free`.
- `SpatialAnalyticsHarness`: 11/11; fallback `seat.personId` alimenta totals y
  Heatmap occupancy.
- Regresión frontend/backend y build: PASS, 0 warnings / 0 errors.
- RC republicada contra `qa-runtime-data`. Smoke
  `qa-runtime-data/logs/audit-45740.log`: 270 total, 112 occupied, 158 free,
  0 reserved, 0 problems, loadInitialData y SVG 5/5.

**READY FOR MANUAL GLOBAL EFFECTIVE STATE QA**

---

## BUG-M10-007 — quality, rectangle selection and Planner eligibility

**QUALITY LOGIC: AUTOMATED PASS**  
**RECTANGLE SELECTION LOGIC: AUTOMATED PASS**  
**PLANNER ELIGIBILITY: AUTOMATED PASS**

- El indicador naranja `!` del pin era `data-quality="incomplete"`, derivado
  de `SeatStates.Completeness`; no era un Validation Warning ni un Problem.
- La regla anterior marcaba partial cualquier assignment sin los cuatro campos,
  incluso si el estado efectivo era Free. La regla nueva usa
  `deriveWorkspaceQuality`: Free y Reserved son válidos por defecto; Occupied
  exige persona, equipo, ubicación y roseta, preservando los requisitos que ya
  aplicaba el contrato de compleción.
- Quality y Problems se mantienen independientes: quality usa el badge superior
  izquierdo `!`; Problems usa el símbolo/contexto de severidad independiente.
- `workspace-quality-harness.js`: 11/11. Auditoría actual read-only:
  161 Free, 112 Occupied y 0 Reserved; 3 Free eran incomplete con la regla
  anterior y 0 lo son ahora. No se modificaron datos.
- La selección rectangular se normaliza mediante `client → normalized stage`
  desde el rect transformado. Selecciona por centro, incluye límites, funciona
  en las cuatro direcciones y con pan/zoom, restringida al mapa actual y a pines
  visibles por filtros. `rectangle-selection-harness.js`: 14/14.
- Planner separa selección geométrica de elegibilidad. Sources Free muestran
  “Puesto libre”, Reserved “Puesto reservado” y Occupied sin assignment
  transferible “Datos insuficientes para mover”. Solo un assignment real con
  persona puede crear un Scenario Draft sin fabricar o perder datos. Warnings e
  Info no bloquean por severidad genérica.
- Fixture actual: 10 sources movibles con assignment moderno; 102 Occupied
  legacy-only permanecen no movibles por información insuficiente. La RC no
  crea assignments sintéticos.
- RC republicada contra `qa-runtime-data`; smoke
  `audit-34468.log`: 273 total, 112 occupied, 161 free, 0 reserved, Validation
  0, Analytics/Heatmap y SVG 5/5 correctos. El fixture actual contiene 273
  seats (tres I+D adicionales respecto a la auditoría anterior de 270); no se
  alteró durante esta tarea.

**QUALITY VISUAL QA: MANUAL PENDING**  
**RECTANGLE INTERACTION QA: MANUAL PENDING**  
**PLANNER USABILITY QA: MANUAL PENDING**

**READY FOR MANUAL QUALITY / RECTANGLE / PLANNER QA**

---

## BUG-M10-009 — Compare y Partial Apply por movimientos atómicos

**Estado: AUTOMATED PASS / PLANNER MANUAL QA PENDING.**

- `Resources/js/features/scenarios/scenario-compare-helpers.js` construye `CompareUnit` inmutable.
  Agrupa exclusivamente por `operationId`, conserva el orden de primera
  aparición y deja los cambios sin operación como unidades independientes.
- Cada movimiento se presenta como una única fila y checkbox. Apply aplana de
  forma determinista todos sus `memberChangeIds`; por diseño, la UI no puede
  enviar medio movimiento. La defensa backend `atomic-operation-incomplete`
  permanece sin cambios.
- El planner UI aplica ahora la misma elegibilidad exacta que el dominio para
  sources heredados: `seat.personId` debe resolver una vez en `people.id`; un
  `deviceName` vacío es opcional, pero uno presente debe resolver una vez en
  `devices.name`. No hay fuzzy matching ni IDs inventados.
- `scenario-compare-units-harness.js`: **24/24**. Incluye moderno, legacy,
  agrupación de 2/3 miembros, selección A+B+D, C sin miembros, remaining diff,
  validación agregada, orden/determinismo/inmutabilidad y IDs de workspace
  repetidos sintéticamente en mapas distintos.
- Identidad runtime: auditoría read-only confirma 273 `workspaceId` y 0
  duplicados. Compare no usa esa identidad para agrupar: solo `operationId`.
- Cobertura read-only actual de `qa-runtime-data`: 112 ocupados; 10 modernos
  movibles; 96 legacy movibles; 6 no movibles; total **106 movibles**. El caso
  con `deviceName` no resoluble no es movible con el contrato implementado;
  además hay tres nombres legacy adicionales que no tienen match único/exacto.

| Plano | Ocupados | Modern movible | Legacy movible | No movible |
|---|---:|---:|---:|---:|
| Norte | 10 | 0 | 9 | 1 |
| Nivel 3 | 18 | 1 | 16 | 1 |
| Sur | 84 | 9 | 71 | 4 |
| I+D | 0 | 0 | 0 | 0 |

- RC republicada con `publish-release-candidate/config.json` restaurado a
  `G:\\Proyecto Planos\\phm\\phm\\uifigmastyle_UX_REDESIGN\\qa-runtime-data`.
  `runtime-lifecycle-harness.ps1`: PASS; cierre normal PID 4052, reapertura PID
  21256, Validation, Analytics y SVG 5/5 en ambos arranques (`audit-4052.log`,
  `audit-21256.log`).

**READY FOR MANUAL PLANNER QA**

---

## BUG-M10-010 — bulk selection actions y Undo

**Estado: AUTOMATED PASS / MANUAL BULK ACTION QA PENDING.**

- `Limpiar` solo vacía selección, anchor y acción contextual pendiente. No llama
  a Undo, no modifica Reality, no elimina History y no restaura datos.
- Acciones disponibles: `Reservar` aplica únicamente a estado efectivo `Free`;
  `Reserved` es no-op “Ya reservado” y `Occupied` queda bloqueado. `Quitar
  reserva` aplica únicamente a `Reserved`; los demás son no-op “No está
  reservado”. `seat.type`, Quality y severidades Warning/Info no participan.
- La selección mixta se resume antes de confirmar y la UI envía exclusivamente
  el safe subset visible como `Aplicar a N`. El backend vuelve a validar todos
  los N y rechaza la petición completa si alguno dejó de ser elegible.
- Apply masivo usa una llamada, una transacción, un backup, una entrada History y
  una unidad Undo. No-ops y listas vacías no publican datos, History ni backup.
- Tras éxito, la acción pendiente se consume para impedir una repetición
  accidental, la selección se conserva temporalmente y aparece `Deshacer`. Ese
  botón y Ctrl+Z invocan el Undo global existente.
- `bulk-selection-harness.js`: **25/25**.
- `ReleaseReadinessHarness`: **20/20**, incluido Apply de cinco puestos, un solo
  evento/backup, Undo exacto y rechazo atómico sin escrituras.
- Regresión completa frontend/backend: PASS. Build: 0 warnings / 0 errors.
- RC republicada tras cierre normal de la instancia bloqueante; configuración
  restaurada a `qa-runtime-data`. Smoke lifecycle: PASS; cierre PID 47156,
  reapertura PID 42972, Validation, Analytics y SVG 5/5 en ambos arranques.

**READY FOR MANUAL BULK ACTION / UNDO QA**

---

## BUG-M10-011 — Selection Review y confirmación QA-BULK-04

**Estado: AUTOMATED PASS / MANUAL SELECTION REVIEW QA PENDING.**

- `#detail-panel` reutiliza la misma región derecha en modo Inspector (una
  selección) o Selection Review (más de una). No existe una segunda selección:
  todo deriva de `appState.selectedWorkspaces`.
- El panel presenta `displayLocation`, persona, estado efectivo, equipo, roseta,
  referencia y zona con fallbacks humanos. Permite centrar un puesto, quitarlo
  individualmente o ejecutar la misma acción central `Limpiar selección`.
- Cada cambio de selección recalcula pins, contador, resumen bulk, safe subset y
  elegibilidad Planner mediante los helpers existentes. Las transiciones 2→1 y
  1→0 restauran Inspector o estado vacío sin selección fantasma.
- `selection-review-harness.js`: **25/25**, incluida selección grande, orden,
  responsive estático, delegación de eventos, determinismo e inmutabilidad.
- QA-BULK-04 automatizado: selected `[F1,F2,R1,O1,F3]`; eligible/sent/changed
  `[F1,F2,F3]`; unchanged `[R1,O1]`; History 1; Backup 1; Undo restaura los tres
  y conserva los dos excluidos. Si un target cambia a Occupied entre preview y
  Apply, backend rechaza todo y los hashes JSON antes/después son idénticos.
- Regresión completa: PASS. Build: 0 warnings / 0 errors.
- RC republicada con `networkRoot` restaurado a `qa-runtime-data`. Smoke lifecycle:
  PASS; cierre PID 41280, reapertura PID 47428, Validation, Analytics y SVG 5/5
  observados en ambos arranques.

**QA-BULK-04: AUTOMATED CONFIRMATION PASS**

**READY FOR MANUAL SELECTION REVIEW / BULK QA**
