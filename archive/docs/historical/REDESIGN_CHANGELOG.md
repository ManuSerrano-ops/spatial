# REDESIGN_CHANGELOG.md — Plano Open Space IT UX Redesign

## Fase 0 — Seguridad (2026-08-22)

- **Copia:** `uifigmastyle` → `uifigmastyle_UX_REDESIGN`
- **Commit inicial:** `3edb7fa`
- **Verificación:** compilación limpia, 104/104 tests
- **Documentación:** `REDESIGN_PLAN.md` creado con arquitectura, funcionalidades y plan de fases

---

## Fase 1 — BASE UX

### 1.1 Búsqueda global
- **Archivos:** `Resources/index.html`, `Resources/app.css`, `Resources/app.js`
- **Qué:** Command palette (`/`), búsqueda en tiempo real, resultados agrupados, navegación al puesto
- **Riesgos:** Ninguno — solo añade UI, no modifica datos ni backend

### 1.2 Panel lateral mejorado
- **Archivos:** `Resources/index.html`, `Resources/app.css`, `Resources/app.js`
- **Qué:** Muestra estado, completitud, todos los campos; persistente
- **Riesgos:** Ninguno — solo modifica presentación

### 1.3 Filtros rápidos
- **Archivos:** `Resources/index.html`, `Resources/app.css`, `Resources/app.js`
- **Qué:** Botones de filtro (Todos, Ocupados, Libres, Reservados, Incompletos, Conflictos)
- **Riesgos:** Ninguno — solo añade clases CSS

### 1.4 Vista Lista
- **Archivos:** `Resources/index.html`, `Resources/app.css`, `Resources/app.js`
- **Qué:** Tabla sincronizada con mapa
- **Riesgos:** Medio — nueva vista que comparte estado con el mapa

### 1.5 Navegación por teclado
- **Archivos:** `Resources/app.js`
- **Qué:** `/`, `←↑↓→`, `Enter`, `Esc`, `E`
- **Riesgos:** Bajo — solo añade event listeners

### 1.6 Foco visible
- **Archivos:** `Resources/app.css`
- **Qué:** Estados visuales para hover, seleccionado, foco teclado, búsqueda, modificado, conflicto
- **Riesgos:** Ninguno — solo CSS

---

## Fase 1.9 — Estabilidad de carga de planos (2026-08-22)

- **Qué:** Corregido el error JavaScript que detenía el render antes de crear los pines; se ha añadido verificación de recursos SVG desde el registro de `maps.json` y un contador de planos cargados/error visible en la interfaz.
- **Archivos:** `Resources/app.js`, `Resources/index.html`, `Resources/app.css`.
- **Motivo:** La UI podía parecer un plano vacío porque `render()` referenciaba `pin` sin inicializarlo. El extractor también podía reutilizar una caché local incompleta si existían solo `index.html` y el marcador de versión.
- **Caché:** `EmbeddedResourceExtractor.cs` valida ahora todos los recursos embebidos antes de reutilizar una extracción; `DataStore.cs` y `WebViewBridge.cs` registran diagnósticos de recursos sin información personal.
- **Riesgo:** Bajo. No modifica el formato de datos ni el flujo transaccional. La extracción se repetirá una vez si detecta un recurso ausente.
- **Pruebas:** `node --check Resources/app.js`; `dotnet build PlanoOpenSpaceIT.Windows.csproj --no-restore` (0 advertencias, 0 errores); `dotnet publish` autocontenido en `publish-ux-validation`; manifiesto de Release con los cinco SVG; smoke test temporizado de Debug y EXE publicado sin salida de error.
- **Pendiente de QA visual:** confirmar `Planos: 5/5 cargados` en desarrollo y en el EXE publicado con un `runtime-data` aislado.

---

## Fase 1.10 — Shell 2.0 (2026-08-22)

- **Qué:** Nueva shell de escritorio con barra superior, navegación lateral, selector de zona, contexto realidad/escenario, región de mapa/lista, inspector contextual y barra de estado.
- **Archivos:** `Resources/index.html`, `Resources/app.css`, `Resources/app.js`, `REDESIGN_PLAN.md`.
- **Datos reales:** La búsqueda y la tabla usan los mapas, asignaciones y catálogos ya cargados; no se han introducido datos simulados ni modificado JSON/persistencia.
- **Operaciones reutilizadas:** Guardar, mover y borrar puestos siguen enviando las operaciones existentes del bridge; el nuevo botón Mover reutiliza `saveSeatPosition` mediante selección de destino.
- **Responsive:** Grid/Flexbox y breakpoints para sidebar compacta, inspector drawer y reducción progresiva de columnas.
- **Pruebas:** `node --check`; compilación Debug correcta; smoke test Debug y publicado completan `loadInitialData` y cargan los 5 SVG sin error JavaScript.
- **Riesgo pendiente:** Falta QA manual interactivo de pan, zoom, selección, lista y breakpoints/DPI; los smoke tests no sustituyen esa comprobación visual.

---

## Fase 2 — Selección y productividad inicial (2026-08-22)

- **Qué:** Selección primaria/múltiple compartida por mapa y lista, Ctrl+clic, selección rectangular, acciones de reserva masiva, capas visuales y navegación espacial por flechas.
- **Archivos:** `Resources/index.html`, `Resources/app.css`, `Resources/app.js`, `DataStore.cs`, `WebViewBridge.cs`.
- **Persistencia:** `bulkUpdateAssignments` ejecuta una única transacción real o una única mutación de escenario; conserva backup, historial y undo existentes.
- **Riesgo pendiente:** La edición masiva inicial solo cambia reserva/estado automático. No se han añadido mutaciones masivas destructivas ni se ha modificado el esquema JSON.
- **Pruebas:** sintaxis JavaScript y compilación Debug sin advertencias ni errores.

---

## Milestone 4 — Validation Engine global y Centro de Problemas (2026-08-22)

- **Motor:** se añade `ValidationEngine.cs`, una capa pura y determinista sin DOM, persistencia ni autocorrección. Las reglas activadas son únicamente las auditadas: roseta/equipo/persona duplicados, asignación sin puesto, marca histórica ocupada y coordenadas inválidas.
- **Contextos:** `DataStore.RunValidation(scenarioId)` reutiliza `LoadUnlocked`, por lo que analiza realidad o el estado efectivo `draft` de un escenario con el mismo motor; no analiza solo el diff.
- **Contrato y diagnóstico:** bridge `runValidation` devuelve resultados, resumen y duración. Los eventos `validation.started` y `validation.finished` registran solo conteos, contexto y duración, sin valores personales o de equipos.
- **UI:** nueva sección Problemas con resumen, filtros, búsqueda normalizada, lista accesible, detalle, relacionados y navegación compartida al plano. Mapa, lista, inspector y sidebar consumen el mismo estado de validación e índice por puesto.
- **Revalidación:** se produce tras la carga/reload efectiva que sigue a guardar, mover, borrar, bulk, undo y cambio de contexto; no se ejecuta por interacción visual.
- **Riesgo pendiente:** no existen quick fixes ni cambios automáticos. QA visual/manual de responsive, teclado y flujo de corrección sigue pendiente.

---

## Milestone 5 — Scenario Diff Engine y Compare

- **Auditoría:** `SCENARIO_ARCHITECTURE.md` documenta `base`, `draft`, revisión de origen, undo por snapshots, estado efectivo y la aplicación parcial existente.
- **Motor:** `ScenarioDiffEngine.cs` compara las entidades reales del modelo — asignaciones por `workstationId` y puestos por `mapId|seatId` — y clasifica los cambios en ADDED, REMOVED, MOVED o MODIFIED. Expone campos changedFields con before/after, IDs estables y orden determinista.
- **Impacto:** el bridge devuelve resumen por tipo, entidad, plano y campos; además contrasta las validaciones de base y borrador para identificar problemas introducidos, resueltos y persistentes.
- **UI:** Escenarios deja de ser placeholder y muestra Compare con resumen, filtros, lista, detalle de campos, navegación al plano y acceso a problemas introducidos. La selección para aplicar permanece en el flujo transaccional existente.
- **Riesgo pendiente:** falta QA manual sobre comparación real, responsive y aplicación parcial usando datos aislados. No se añade diff visual avanzado ni autocorrección.

---

## Milestone 6 — Movement Planner

- **Identidad:** `SpatialLocation` deriva A-01…X-18 desde coordenadas normalizadas 24×18. Los IDs técnicos heredados (`N-01`, etc.) se mantienen en asignaciones, escenarios, diff, Apply, historial y backups; la UI los oculta como referencia principal.
- **Motor:** `MovementPlanner.cs` es puro, determinista y read-only. Recibe pares explícitos, bloquea únicamente orígenes/destinos inválidos o no disponibles y enlaza Validation/Diff existentes sin reimplementarlos.
- **UI:** la multiselección abre un panel Planner que reemplaza el inspector. El flujo visible es origen → destino → propuesta → crear escenario; usa ● origen, ◎ destino y × no disponible, con texto accesible y densidad semántica del mapa.
- **Seguridad:** `createScenarioFromMovementPlan` revalida el plan, crea base inmutable y draft en una única escritura de `scenarios.json`. No hay Apply a REALIDAD desde Planner; al terminar se activa el escenario y se abre Compare.
- **Pruebas:** harness C# aislado de 11 casos, harness frontend de 10 casos, y regresión de Validation, ScenarioDiff y Core UX. QA manual y smokes de la iteración quedan pendientes hasta su ejecución.

---

## Milestone 7 — Analítica espacial y Heatmaps

- **Auditoría:** solo se habilitan métricas derivables de puestos, estado vivo, validaciones, coordenadas normalizadas y cambios Scenario Diff. Las subzonas, superficies, distancias, capacidad, presencia temporal y costes quedan excluidos por falta de modelo.
- **Motor:** `SpatialAnalyticsEngine.cs` es puro y determinista; genera totales, ocupación/disponibilidad, problemas, desglose por mapa y puntos de capa para el estado efectivo.
- **Contexto:** `runSpatialAnalytics` reutiliza REALIDAD/draft efectivo y construye baseline de `base` en escenarios. No duplica Validation ni Diff.
- **UI:** Analítica muestra resumen numérico y tabla navegable; el heatmap SVG vive bajo los pines, no recibe clics y dispone de selector, leyenda y escala accesible. Compare usa escala compartida y delta en pp.
- **Compatibilidad:** no se modifica `runtime-data`, el esquema de escenarios ni la exportación Excel. No hay métricas especulativas.

---

## Milestone 8 — Dashboard operativo

- **ViewModel:** `dashboard-helpers.js` convierte contratos ya derivados de Spatial Analytics, Validation y Scenario Diff en un modelo puro, determinista e inmutable. No vuelve a recorrer JSON ni recalcula tasas, validación, asignaciones o cambios.
- **UI:** Dashboard pasa a ser la entrada operativa con contexto explícito REALIDAD/ESCENARIO, seis KPIs máximos, problemas con símbolos, ocupación por plano, disponibilidad, atención operativa y estados vacíos.
- **Escenarios:** el impacto muestra únicamente el resumen oficial Added/Removed/Moved/Modified y ValidationImpact. Antes de llegar el diff se indica actualización, sin deducir que no existen cambios.
- **Navegación y accesibilidad:** cards, problemas y planos reutilizan filtros, `setViewMode` y `focusSeat`; todos son botones nativos. Barras de ocupación incluyen patrón, porcentaje y nombre ARIA; severidades usan ×/!/i además de color.
- **Límites:** no se añaden historial reciente, tendencias, costes, capacidad, presencia, predicción, scores arbitrarios ni modificación del Excel. `DASHBOARD_ARCHITECTURE.md` documenta evidencia y candidatos.

---

## Milestone 9 — Consolidación / QA / hardening

- **UI:** la navegación compacta sustituye puntos indistinguibles por abreviaturas únicas con nombres accesibles; Selección, Filtros y Capas permanecen disponibles en compacto. Search se posiciona desde su control, los popovers respetan el stacking de drawers y `Escape` cierra overlays transitorios.
- **Mapa y accesibilidad:** mapa y disclosures muestran foco visible; la precarga SVG ignora callbacks de una carga anterior. Se reportan 62 celdas de `displayLocation` compartidas sin cambiar automáticamente la rejilla ni los IDs técnicos.
- **Seguridad y ciclo de vida:** el bridge acepta únicamente `https://plano.local`; cierre desuscribe mensajes y evita respuestas tardías. Recursos `.orig` locales se excluyen del paquete publicado.
- **Persistencia y privacidad:** backups ZIP temporales se validan antes de publicar, backups corruptos no bloquean el listado y `WriteAtomic` hace flush antes del rename. Logs dejan de incluir usuario, máquina y rutas absolutas.
- **Evidencia:** se añaden harnesses aislado E2E y estático de release; Release Candidate y extracción limpia completan Dashboard, Validation, Analytics y SVG 5/5. `RELEASE_READINESS.md` y `QA_EXECUTION_REPORT.md` distinguen pruebas automáticas de QA manual pendiente.
