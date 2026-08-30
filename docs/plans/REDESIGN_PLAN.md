# REDESIGN_PLAN.md — Plano Open Space IT UX Redesign

## Estado actual

**Snapshot:** commit `3edb7fa` — copia exacta de `uifigmastyle` funcional (104 tests, compilación limpia).

### Arquitectura

```
WPF (.NET 8) → WebView2 → HTML/CSS/JS → SVG 2D
                    ↕ (JSON messages)
              DataStore.cs → JSON files (shared folder)
```

### Capas

| Capa | Archivos | Responsabilidad |
|------|----------|-----------------|
| Shell WPF | `MainWindow.xaml`, `MainWindow.xaml.cs` | Ventana, WebView2, puente nativo |
| Puente | `WebViewBridge.cs` | Dispatch de acciones frontend → backend |
| Datos | `DataStore.cs` (~1068 líneas) | CRUD, transacciones, backups, lock, logging |
| Estado | `SeatState.cs` | Derivación de estado y completitud |
| Exportación | `XlsxExporter.cs` | Excel desde plantilla |
| Integridad | `IntegrityReport.cs` | Detección de problemas |
| UI | `Resources/index.html`, `app.js`, `app.css` | Interfaz completa |
| Temas | `UiTheme.cs` | 4 temas (claro, oscuro, contraste, proyector) |
| Preferencias | `ExportFolderPreferences.cs` | Carpeta exportación + tema por usuario |
| Logging | `SafeLogger.cs` | Logs JSON rotados |
| Backups | `BackupRetention.cs`, `BackupRetentionReport.cs` | ZIP + retención |
| Recursos | `EmbeddedResourceExtractor.cs` | Extracción de recursos embebidos |

### Funcionalidades existentes (checklist de regresión)

- [x] Visualizar 5 planos SVG con zoom/pan
- [x] Cuadrícula lógica 24×18 con etiquetas A-X / 01-18
- [x] Pines circulares con estado (free/occupied/reserved) + completitud (complete/partial)
- [x] Selección de puesto → panel lateral con edición
- [x] Crear puesto (clic en plano o menú contextual)
- [x] Mover puesto (drag & drop)
- [x] Eliminar puesto
- [x] Guardar asignación (persona, dispositivo, ubicación, roseta, notas, status)
- [x] Quitar asignación
- [x] Selector de status (Automático / Reservado)
- [x] Texto libre para persona, dispositivo, ubicación
- [x] Sugerencias para roseta
- [x] Validación: roseta duplicada (error), dispositivo duplicado (error), persona repetida (warning)
- [x] Escenarios (crear, editar, diff, aplicar seleccionados, eliminar)
- [x] Historial de eventos
- [x] Backups (listar, restaurar)
- [x] Deshacer (escenario y realidad)
- [x] Exportar Excel (3 hojas, basado en plantilla fija)
- [x] Informe de integridad (rosetas duplicadas, marcas históricas, huérfanas)
- [x] Informe de retención de backups
- [x] Bloqueo de concurrencia (`.lock`, `commit.pending`, `state.json`)
- [x] Recuperación de transacciones interrumpidas
- [x] Perfil `readOnly`
- [x] 4 temas visuales (claro profesional, oscuro, alto contraste, proyector)
- [x] Iconos Lucide locales (sin CDN)
- [x] Leyenda de pines (estado + completitud)
- [x] Menú contextual (añadir puesto, copiar celda, restablecer vista)
- [x] Búsqueda básica por ID/nombre
- [x] Atajos de teclado básicos (ninguno documentado)
- [x] Despliegue (script PowerShell)
- [x] Selector de carpeta de exportación con preferencia local
- [x] Logo PharmaMar

---

## Fase 1 — BASE UX

### Tareas

- [x] **1.1 Búsqueda global** (`/` o Ctrl+K para enfocar, resultados agrupados, navegación al puesto)
- [x] **1.2 Panel lateral mejorado** (muestra estado textual: Libre/Ocupado/Reservado)
- [x] **1.3 Filtros rápidos** (Todos, Ocupados, Libres, Reservados, Incompletos, Conflictos)
- [x] **1.4 Vista Lista** (tabla sincronizada con mapa, ordenable, seleccionable, filas atenuadas por filtro)
- [x] **1.5 Navegación por teclado** (`/`, `←↑↓→`, `Enter`, `Esc`, `E`)
- [x] **1.6 Fix HTML roto** (doble `</head>`, meta fuera de head)
- [x] **1.7 Fix controles** (`#apply-dialog` deshabilitado cuando no hay cambios, `#undo` habilitado/deshabilitado correctamente)
- [x] **1.8 Switch mapa/lista** (toolbar con botones Mapa/Lista)

### Decisiones técnicas

- La búsqueda se implementa en el frontend (JS) sobre los datos ya cargados → instantánea, offline.
- La vista lista comparte el mismo `ui.state` que el mapa → sincronización natural.
- Los filtros manipulan clases CSS (`.dim`) sin re-renderizar → rendimiento.
- La navegación por teclado usa `findAdjacentSeat` para navegación espacial.

### Estabilidad de recursos SVG — completado

- [x] Corregido el fallo de render que detenía la creación de marcadores (`pin` no inicializado).
- [x] `maps.json` es el registro único: el diagnóstico no hardcodea IDs ni nombres de planos.
- [x] La caché extraída valida todos los recursos embebidos antes de reutilizarse.
- [x] La interfaz precarga los SVG definidos por el registro y muestra `cargados/esperados`.
- [x] Los fallos de carga se muestran y registran como diagnóstico sin PII.

**Bloqueo resuelto:** no avanzar con nuevas vistas hasta verificar visualmente el diagnóstico `5/5` en desarrollo y en un EXE publicado sobre datos aislados.

---

## Fase 1.10 — Shell 2.0 (en curso)

- [x] Shell responsive: navegación lateral, toolbar global, región principal, inspector contextual y barra de estado.
- [x] Registro de planos en selector de zona sin nombres hardcodeados.
- [x] Estado central ligero para mapa, escenario, selección, filtro, búsqueda, modo de vista, zoom y pan.
- [x] Mapa y lista sincronizados por el mismo estado.
- [x] Búsqueda offline sobre puestos, personas, equipos, rosetas y zona.
- [x] Filtros rápidos con atenuación de marcadores y lista.
- [x] Acción alternativa de mover: seleccionar destino con la acción contextual, además del arrastre existente.
- [ ] QA visual manual de densidad, focus y transiciones en la matriz DPI/resolución.

## Fase 2 — PRODUCTIVIDAD

- [x] Capas visuales iniciales: puestos, rejilla y detalles.
- [x] Zoom semántico inicial: etiquetas de puesto activables y detalle a zoom alto.
- [x] Selección múltiple: Ctrl+clic, selección rectangular y Ctrl desde lista.
- [x] Edición masiva segura de reserva mediante una transacción y un único evento/undo.
- [ ] Selección por rango Shift en lista y edición masiva de otros campos compatibles.
- [x] Centro de problemas / validaciones (motor central, estado efectivo Reality/Scenario, filtros, detalle y navegación al plano)
- [x] Motor de validaciones (puro, determinista y sin persistencia)

## Milestone 4 — Calidad de datos y Problemas

- [x] Reglas auditadas y documentadas en `../architecture/VALIDATION_RULES.md`.
- [x] Único `ValidationEngine` para realidad y estado efectivo de escenario.
- [x] Bridge `runValidation`, contrato serializable, conteos y duración sin PII en logs.
- [x] Estado central de validación y revalidación tras carga, mutaciones persistidas y cambio de contexto.
- [x] Centro de Problemas: resumen, filtros, búsqueda, detalle, relacionados y navegación compartida al plano.
- [x] Capa Calidad/Problemas, indicador de inspector, columna de lista y badge de navegación.
- [x] Harnesses aislados de reglas, determinismo, escenario y helpers.
- [ ] QA visual manual, responsive, DPI y recorridos de teclado.
- [ ] Production Ready.

---

## Milestone 5 — Scenario Diff / Compare

**Estado técnico: COMPLETE**

- [x] Implementation: `ScenarioDiffEngine` puro con ADDED / REMOVED / MOVED / MODIFIED y `changedFields` before/after.
- [x] Programmatic tests: determinismo, Impact Summary, Validation Impact y compatibilidad de IDs.
- [x] Regression smoke: Debug y publicado, Validation OK y SVG 5/5.
- [x] Compare: filtros, lista, detalle, navegación al plano e impacto de calidad.
- [x] Aplicación parcial existente preservada mediante IDs de cambio estables.
- [ ] Manual visual / interactive QA: Compare, navegación y aplicación parcial sobre datos aislados.
- [ ] Production Ready.
- [ ] Comparación visual lado a lado avanzada.

---

## Milestone 6 — Movement Planner

**Estado técnico: en cierre**

- [x] `SpatialLocation` separa identificador técnico de ubicación visual A-01…X-18 sin cambiar persistencia.
- [x] `MovementPlanner` puro, determinista y de solo lectura para pares explícitos.
- [x] Multiselección → orígenes movibles → destinos libres en el mapa → propuesta revisable.
- [x] Emparejamiento estable, destinos insuficientes, exclusión no destructiva y override manual.
- [x] Panel Planner contextual que sustituye al inspector; símbolos ● / ◎ / × además de color.
- [x] Integración informativa de Validation y Scenario Diff; navegación compartida al mapa.
- [x] `createScenarioFromMovementPlan`: un escenario nuevo desde REALIDAD, base inmutable y draft con todos los movimientos; REALIDAD no recibe cambios.
- [x] Activación del escenario creado, revalidación y apertura automática de Compare.
- [x] Harnesses aislados para motor, creación segura y coordinación frontend.
- [ ] Smoke Debug/publicado y QA manual visual, responsive, DPI, teclado e interacción sobre copia aislada.
- [ ] Production Ready.

---

## Milestone 7 — Analítica espacial y Heatmaps

**Estado técnico: en cierre**

- [x] Auditoría de métricas respaldadas y límites documentados en `../architecture/SPATIAL_ANALYTICS_ARCHITECTURE.md`.
- [x] `SpatialAnalyticsEngine` puro: totales, estados, tasas, validación y desglose por plano.
- [x] Estado efectivo REALIDAD/ESCENARIO, baseline de escenario y cambios del único Scenario Diff.
- [x] Capas SVG de ocupación, disponibilidad, problemas y cambios de escenario; no bloquean marcadores.
- [x] Selector compacto, leyenda accesible, escala, resumen numérico y tabla navegable por plano.
- [x] Compare con escala compartida y deltas en puntos porcentuales.
- [x] Harness C# y helpers frontend; sin Excel, dashboard, métricas físicas ni subzonas inventadas.
- [ ] Smoke Debug/publicado y QA manual visual, responsive, DPI y teclado.
- [ ] Production Ready.

## Milestone 8 — Dashboard operativo

**Estado técnico: en cierre**

- [x] `buildDashboardModel` puro e inmutable sobre Analytics, Validation, escenario y Scenario Diff.
- [x] Contexto explícito REALIDAD/ESCENARIO, tarjetas de ocupación y disponibilidad, problemas y atención operativa sin scores artificiales.
- [x] Resumen navegable por plano, ranking de disponibilidad, estados vacíos y acciones que reutilizan navegación/filtros centrales.
- [x] Impacto oficial de escenario y ValidationImpact cuando el Scenario Diff está disponible; no se interpreta su ausencia como cero cambios.
- [x] Dashboard responsive (amplio/normal/compacto), botones nativos, barras con porcentaje/texto/ARIA y símbolos de severidad.
- [x] Harness frontend de ViewModel y contrato, sin motor, persistencia, Excel ni lectura adicional de JSON.
- [x] Smoke Debug/publicado de solo lectura y recursos SVG 5/5; evidencia en `../qa/TESTING.md`.
- [ ] QA manual visual, responsive, DPI y teclado.
- [ ] Production Ready.

## Milestone 9 — Consolidación / QA / hardening

**Estado técnico: en cierre**

- [x] Inventario de readiness, auditoría de navegación, responsive estructural, DPI estructural y recursos offline.
- [x] Correcciones verificables de sidebar compacta, controles móviles, popovers, foco, Escape y diagnóstico de recursos.
- [x] Hardening de bridge/origen, cierre tardío, backups temporales validados, logs minimizados y exclusión de `.orig` publicado.
- [x] E2E aislado de escenario, Planner, Diff, Validation, Analytics, Apply parcial, backup/historial y Undo.
- [x] Harness estático y reporte de colisiones `displayLocation` sin alterar la rejilla.
- [x] Regresiones, build, Release Candidate, extracción limpia y smokes Debug/Published con SVG 5/5.
- [ ] QA visual humana, DPI real, teclado completo, cierre por UI Automation y operación SMB real.
- [ ] Production Ready.

## Métricas futuras

- [ ] Métricas temporales solo cuando exista evidencia operacional