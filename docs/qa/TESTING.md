# Plan de pruebas — Plano Open Space IT 2.0

## Principio de seguridad

Ejecutar operaciones de escritura sobre `runtime-data` crea logs, backups, eventos o cambios de datos. Las pruebas destructivas deben usar una copia aislada del directorio `runtime-data`, nunca datos operativos compartidos.

## Estado de evidencia

- **Automático:** sintaxis JavaScript, compilación .NET y validación de recursos embebidos.
- **Logs / smoke test:** inicio WPF, `loadInitialData` y carga de los cinco SVG.
- **Manual pendiente:** layout, DPI, puntero, foco, interacciones y accesibilidad visual. Consultar `MANUAL_QA_CHECKLIST.md`.

## Cierre técnico Core UX

| Prueba | Resultado |
|---|---|
| Core UX harness | 14/14 PASS, 0 failed |
| Sintaxis Node | PASS |
| Build .NET | PASS, 0 warnings / 0 errors |
| Debug smoke | PASS — `audit-Manu-DESKTOP-NARHLHP-31372.log`, SVG 5/5, 0 errores JS/rechazos no controlados |
| Published smoke | PASS — `audit-Manu-DESKTOP-NARHLHP-26940.log`, SVG 5/5, 0 errores JS/rechazos no controlados |
| Clean extraction | PASS — `audit-Manu-DESKTOP-NARHLHP-2724.log`, SVG 5/5, 0 errores JS/rechazos no controlados |
| Rendimiento | `loadInitialData` Debug: 32 ms; resto no medido |
| QA visual manual | PENDIENTE |

El intento publicado `audit-Manu-DESKTOP-NARHLHP-37244.log` fue rechazado como evidencia insuficiente (4/5); no se usó para el cierre.

## Milestone 4 — Validation Engine y Problemas

| Comprobación | Resultado |
|---|---|
| Validation harness JS (fixtures, contrato, Reality/Scenario, filtros/helpers) | **17/17 PASS, 0 failed** |
| ValidationEngine harness C# (motor real) | **9/9 PASS, 0 failed** |
| Core UX harness | **14/14 PASS, 0 failed** |
| Determinismo | PASS (Reality y escenario) |
| Contexto Reality | PASS (fixture válido sin conflicto introducido) |
| Contexto Scenario | PASS (conflicto sobre estado efectivo y desaparición al corregirlo) |
| Node (`app.js`, `validation-helpers.js`) | PASS |
| Build .NET | PASS, **0 warnings / 0 errors** |
| Debug validation smoke | PASS — `audit-Manu-DESKTOP-NARHLHP-30884.log`; `validation.finished`, 202 resultados, 0 critical, 0 warning, 202 info, 22 ms |
| Debug SVG | PASS, **5/5** |
| Published validation smoke | PASS — `audit-Manu-DESKTOP-NARHLHP-37780.log`; `validation.finished`, 202 resultados, 0 critical, 0 warning, 202 info, 18 ms |
| Published SVG | PASS, **5/5** |
| JavaScript errors / unhandled rejections (ambos smokes) | 0 / 0 en los logs auditados |
| Clean extraction | Baseline anterior PASS — `audit-Manu-DESKTOP-NARHLHP-2724.log`, SVG 5/5; no repetida porque no cambió extractor ni SVG/mapas |
| QA manual visual | **PENDIENTE** |

La duración se registra por el backend en `validation.finished` (`durationMs`) sin payload ni datos personales. Los resultados de validación son resultados de calidad; no son un error del motor.

## Milestone 5 — Scenario Diff Engine y Compare

| Comprobación | Resultado |
|---|---|
| ScenarioDiffEngine harness C# | **10/10 PASS, 0 failed** |
| Cobertura | estado sin cambios; assignment/workspace ADDED y REMOVED; assignment MODIFIED; workspace MOVED; movimiento + modificación; changedFields; orden determinista; Impact Summary; Validation Impact |
| ValidationEngine harness C# | 9/9 PASS, 0 failed |
| Validation harness JS | 17/17 PASS, 0 failed |
| Core UX harness | 14/14 PASS, 0 failed |
| Node `Resources/js/core/app.js` | PASS |
| Build .NET | PASS, 0 warnings / 0 errors |
| Debug smoke de regresión | PASS — `audit-Manu-DESKTOP-NARHLHP-37164.log`; Validation PASS (28 ms), SVG 5/5, 0 JS errors/rejections |
| Published smoke de regresión | PASS — `audit-Manu-DESKTOP-NARHLHP-21736.log`; Validation PASS (21 ms), SVG 5/5, 0 JS errors/rejections |
| Manual Scenario Apply QA | **PENDIENTE** — no ejecutado contra `runtime-data` operativo |
| Manual responsive QA | **PENDIENTE** |
| Manual keyboard QA | **PENDIENTE** |

## Milestone 6 — Movement Planner

| Comprobación | Resultado |
|---|---|
| MovementPlanner harness C# | **11/11 PASS, 0 failed** |
| Cobertura C# | ubicación espacial, propuestas, IDs técnicos, bloqueos, Validation/Diff relacionados, determinismo, bridge y creación aislada de escenario |
| Creación aislada | PASS — REALIDAD sin cambios, draft efectivo con movimiento, diff de asignación y `RunValidation(scenarioId)` |
| Movement Planner frontend harness | **10/10 PASS, 0 failed** |
| Cobertura frontend | estado, fuentes, pairing estable, sin destino, exclusión, override, resumen, serialización e identidad técnica/display location |
| ScenarioDiffEngine harness | 10/10 PASS, 0 failed |
| ValidationEngine harness | 9/9 PASS, 0 failed |
| Validation JS harness | 17/17 PASS, 0 failed |
| Core UX harness | 14/14 PASS, 0 failed |
| Node (`app.js`, helper Planner) | PASS |
| Build .NET | PASS, 0 warnings / 0 errors |
| Debug smoke Milestone 6 | PASS — `audit-Manu-DESKTOP-NARHLHP-4408.log`; `loadInitialData` 95 ms, `runValidation` 25 ms, `SVG 5/5`, sin errores/rechazos JS registrados |
| Published smoke Milestone 6 | PASS — `audit-Manu-DESKTOP-NARHLHP-35292.log`; `loadInitialData` 110 ms, `runValidation` 18 ms, `SVG 5/5`, sin errores/rechazos JS registrados |
| Manual Planner / responsive / keyboard QA | **PENDIENTE** — solo sobre copia aislada |

## Milestone 7 — Analítica espacial y Heatmaps

| Comprobación | Resultado |
|---|---|
| SpatialAnalytics harness C# | **9/9 PASS, 0 failed** |
| Cobertura | vacío, estados/tasas, mapas, validación, Reality/Scenario, determinismo, puntos, coordenadas inválidas y densidad de diff |
| Spatial analytics frontend harness | **7/7 PASS, 0 failed** |
| Cobertura frontend | métrica, leyenda accesible, mapa, escala compartida, capa, resumen y delta pp |
| Node `Resources/js/core/app.js` / helpers | PASS |
| Build .NET | PASS, 0 warnings / 0 errors |
| Debug smoke Milestone 7 | PASS — `audit-Manu-DESKTOP-NARHLHP-32204.log`; `analytics.finished` 37 ms, Validation OK, módulo Heatmap inicializado y `SVG 5/5` |
| Published smoke Milestone 7 | PASS — `audit-Manu-DESKTOP-NARHLHP-12316.log`; `analytics.finished` 22 ms, Validation OK, módulo Heatmap inicializado y `SVG 5/5` |
| QA manual heatmap / responsive / keyboard | **PENDIENTE** |

## Milestone 8 — Dashboard operativo

| Comprobación | Resultado |
|---|---|
| Dashboard ViewModel harness | **13/13 PASS, 0 failed** |
| Cobertura Dashboard | Reality/Scenario, KPIs derivados, problemas, orden por plano, navegación declarativa, impacto, vacío, determinismo, inmutabilidad, formato finito |
| SpatialAnalytics harness C# | **9/9 PASS, 0 failed** |
| MovementPlanner harness C# | **11/11 PASS, 0 failed** |
| ScenarioDiffEngine harness C# | **10/10 PASS, 0 failed** |
| ValidationEngine harness C# | **9/9 PASS, 0 failed** |
| Frontend Spatial Analytics / Planner / Validation / Core UX | **7/7**, **10/10**, **17/17**, **14/14 PASS** |
| Sintaxis Node (`app.js`, helpers) | PASS |
| Build .NET | PASS, **0 warnings / 0 errors** |
| Instrumentación | `buildDashboardModel` y `renderDashboard` usan `measureSync`; solo publican duración en modo diagnóstico, sin datos personales |
| Debug smoke | `audit-Manu-DESKTOP-NARHLHP-34716.log`: Dashboard, Analytics, Validation y **SVG 5/5**. `audit-Manu-DESKTOP-NARHLHP-16096.log` confirma el diagnóstico de Dashboard tras mover su registro al final de `load()`; el proceso fue forzado antes del último callback SVG |
| Published smoke | **PASS** — `audit-Manu-DESKTOP-NARHLHP-33440.log`: Dashboard, Analytics (25 ms), Validation (6 ms), **SVG 5/5**, sin `JavaScript error` ni `Unhandled rejection` registrados |
| QA manual Dashboard / responsive / teclado | **PENDIENTE** |

Los smokes no crean escenarios ni invocan Apply. Los logs y artefactos de publicación son evidencia local y no se incluyen en el commit.

## Milestone 9 — Consolidación, QA y hardening

| Comprobación | Resultado |
|---|---|
| ReleaseReadinessHarness C# | **7/7 PASS, 0 failed**; fixture temporal aislado: identidad del bridge, Reality, escenario, Planner, analítica, Apply parcial, backup, historial y Undo. |
| Release readiness estático | **6/6 PASS, 0 failed**; recursos locales/offline, navegación compacta, controles de mapa, búsqueda, Escape, foco y auditoría de ubicaciones. |
| Regresión frontend | Dashboard **13/13**, Spatial Analytics **7/7**, Movement Planner **10/10**, Validation **17/17**, Core UX **14/14 PASS**. |
| Regresión C# | SpatialAnalytics **9/9**, MovementPlanner **11/11**, ScenarioDiffEngine **10/10**, ValidationEngine **9/9 PASS**. |
| Sintaxis Node | PASS para `app.js`, helpers propios y `tests/release-readiness-harness.js`. |
| Build WPF | PASS, **0 warnings / 0 errors**. |
| Debug smoke M9 | PASS — `runtime-data/logs/audit-39256.log`; `loadInitialData` 41 ms, Validation 14 ms, Spatial Analytics 22 ms y **SVG 5/5**. |
| Published RC + extracción limpia M9 | PASS — `runtime-data/logs/audit-9804.log`; `loadInitialData` 44 ms, Validation 16 ms, Spatial Analytics 18 ms, Dashboard/Heatmap y **SVG 5/5**. Solo se eliminó previamente `%LocalAppData%\\PlanoOpenSpaceITUiFigma\\Resources`; no se tocaron datos, configuración ni backups. |
| Errores JS / rechazos no controlados en smokes M9 | 0 / 0 registrados. |
| Colisiones de `displayLocation` | **62** celdas con más de un asiento: Norte 0, Nivel 3 5 celdas/10 puestos, Sur 57 celdas/136 puestos, I+D 0, QC 0. Se reportan; no se altera la rejilla, la referencia humana ni los IDs técnicos. |
| QA visual, DPI, teclado, lector de pantalla y SMB | **PENDIENTE / no testeable aquí**; seguir `QA_EXECUTION_REPORT.md` y `MANUAL_QA_CHECKLIST.md`. |

### Hardening M9

- Las copias de backup se publican mediante archivo temporal validado; un ZIP corrupto se registra y no impide listar los backups válidos.
- Las escrituras atómicas realizan `Flush(true)` antes de reemplazar el destino. La garantía final continúa dependiendo del sistema de archivos/servidor compartido ante un corte eléctrico.
- La infraestructura actual conserva bloqueo `.lock`, revisión global, `commit.pending`, recuperación de transacciones propias y comprobación de `baseRevision` al aplicar escenarios. Los consumidores externos de JSON deben cooperar con esos artefactos.
- Las mutaciones directas de Reality aún no incluyen `expectedRevision` en el contrato de cliente. Se documenta como limitación aceptada para evitar alterar todos los contratos en un hito de hardening.
- El bridge acepta mensajes únicamente desde `https://plano.local`; durante el cierre se desuscribe y se desechan respuestas tardías. El `timeout` del runner puede dejar procesos WPF/WebView2 de smoke; el cierre accionado por usuario queda para QA manual/UI Automation.
- El logging no registra usuario, nombre de equipo ni rutas absolutas de exportación/informe. La rotación sigue siendo por archivo de proceso, no una política global de retención.

## Validaciones realizadas en este hito

| Comprobación | Resultado |
|---|---|
| Sintaxis de `Resources/js/core/app.js` con `node --check` | Correcta |
| Compilación WPF del rediseño con `dotnet build --no-restore` | Correcta, 0 advertencias y 0 errores |
| Registro de planos en `runtime-data/data/maps.json` | 5 mapas configurados y 5 SVG presentes en `Resources/` |
| Causa de plano vacío | Corregido `ReferenceError: pin is not defined` antes de crear pines |
| Caché de recursos | El extractor valida todos los recursos embebidos, no solo `index.html` |
| Recursos de Release | Manifiesto del ensamblado contiene los 5 SVG configurados |
| Smoke de Debug y EXE publicado | Proceso WPF iniciado sin salida de error durante 8 s; detenido por el límite de la prueba |

## Regresión funcional por hito

Después de cada hito probar en una copia aislada de datos:

1. Abrir y cerrar; volver a abrir.
2. Verificar que los cinco planos cargan y que el diagnóstico indica `5/5`.
3. Cambiar entre los cinco planos, aplicar zoom, pan y seleccionar un puesto.
4. Crear, editar, mover y borrar un puesto; volver a abrir y verificar persistencia.
5. Crear un escenario, editarlo, revisar Diff, aplicar solo parte de los cambios y comprobar el historial.
6. Ejecutar undo y comprobar que crea un evento sin borrar historial.
7. Listar y restaurar un backup; confirmar que no rebobina escenarios, catálogos, historial ni revisión global.
8. Generar Excel desde realidad confirmada y verificar que no usa el escenario activo.
9. Ejecutar informe de integridad y comprobar que no autocorrige datos.
10. Verificar funcionamiento sin red.

## Pruebas del diagnóstico SVG

- Confirmar que el arranque muestra `Planos: 5/5 cargados` con recursos válidos.
- En una copia de pruebas, renombrar temporalmente uno de los SVG configurados para confirmar que aparece el contador de error, el recurso esperado y una entrada `plan.resource.diagnostic` en logs.
- Restaurar el archivo y confirmar carga tras reiniciar. No realizar esta prueba en el proyecto original ni en datos de producción.
- Validar desarrollo y publicación: el extractor debe volver a extraer recursos si falta cualquiera de ellos aunque el marcador tenga el MVID actual.

## Matriz responsive y DPI pendiente de QA visual

| Resolución | Escalado Windows | Estados a comprobar |
|---:|---:|---|
| 1366×768 | 100 % | Barra compacta, plano prioritario, panel no inutiliza mapa |
| 1920×1080 | 100 % | Layout normal con paneles estables |
| 1920×1080 | 150 % | Texto, hitboxes y zoom SVG |
| 2560×1440 | 125 % | Densidad de lista y panel contextual |
| 3440×1440 | 100 % | Uso del ancho sin deformar SVG |
| 3840×2160 | 150 % | Legibilidad, no texto microscópico |

Para cada caso: maximizar, restaurar, media pantalla, 75 % de pantalla, redimensionado continuo y cambio de monitor/DPI. Verificar que se conservan selección, escenario, filtros, plano y viewport cuando corresponda.

## Pruebas de contexto

Con escenario activo, filtro activo y puesto seleccionado:

1. Cambiar mapa/lista.
2. Redimensionar la ventana.
3. Cambiar de plano y volver.
4. Abrir búsqueda, seleccionar un resultado y revisar panel contextual.

El contexto no debe perderse salvo que la entidad seleccionada deje de existir.

## Publicación

Antes de distribuir:

```powershell
dotnet publish PlanoOpenSpaceIT.Windows.csproj -c Release -r win-x64 --self-contained true -o publish
```

Probar el EXE generado junto a su `config.json` en un directorio de pruebas y con WebView2 Runtime disponible. El smoke test de publicación debe incluir el diagnóstico 5/5, un escenario de prueba y una exportación contra datos aislados.
