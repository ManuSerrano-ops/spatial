# Checklist de QA visual y funcional manual

> Estado: **manual pendiente**. Esta lista no se sustituye por build, logs ni smoke test.

## Preparación

1. Cerrar instancias anteriores.
2. Usar una copia aislada de `runtime-data` para pruebas que guarden, muevan, borren o apliquen cambios.
3. Abrir Debug y el EXE publicado; comprobar en la barra de diagnóstico `Planos configurados: 5 · encontrados: 5 · cargados: 5`.

## Matriz responsive

| Viewport / DPI | Sidebar | Toolbar | Mapa SVG | Inspector | Lista | Resultado |
|---|---|---|---|---|---|---|
| 1280×720 @100 % | ☐ | ☐ | ☐ | ☐ | ☐ | Pendiente |
| 1366×768 @100 % | ☐ | ☐ | ☐ | ☐ | ☐ | Pendiente |
| 1440×900 @100 % | ☐ | ☐ | ☐ | ☐ | ☐ | Pendiente |
| 1920×1080 @100 % | ☐ | ☐ | ☐ | ☐ | ☐ | Pendiente |
| 1920×1080 @150 % | ☐ | ☐ | ☐ | ☐ | ☐ | Pendiente |
| 2560×1440 @125 % | ☐ | ☐ | ☐ | ☐ | ☐ | Pendiente |
| 3440×1440 @100 % | ☐ | ☐ | ☐ | ☐ | ☐ | Pendiente |
| 3840×2160 @150 % | ☐ | ☐ | ☐ | ☐ | ☐ | Pendiente |

Para cada fila comprobar: sin solapamientos, textos completos, sin scroll horizontal global, hitboxes accesibles, mapa no deformado, inspector utilizable y toolbar sin desborde. Registrar si el DPI fue físico o espacio CSS simulado.

## Interacciones fundamentales

- [ ] Abrir Norte, Nivel 3, Sur, I+D y QC; cambiar de plano y volver.
- [ ] Pan, zoom, selección de puesto, inspector, arrastre y acción Mover.
- [ ] Mapa → Lista → Mapa: la selección y plano se conservan.
- [ ] Búsqueda por persona, puesto, equipo y roseta; Escape cierra resultados.
- [ ] Filtros rápidos y avanzados, chips, limpiar filtros y Solo coincidencias.
- [ ] Ctrl+clic, selección rectangular, Ctrl/Shift/Ctrl+Shift desde lista.
- [ ] Reserva masiva, preview, undo, historial y escenario.
- [ ] Capas de puestos, rejilla, etiquetas, personas, equipos y rosetas en zoom global/operativo/detalle.
- [ ] Teclado: `/`, `F`, `Esc`, `E`, Ctrl+Z, Ctrl+Y, Enter y flechas en mapa.
- [ ] Modales: Escape, foco inicial, foco atrapado y foco restaurado al cerrar.
- [ ] Cerrar, reabrir y confirmar persistencia de los cambios de prueba.

## Regresión operativa

- [ ] Historial, backups, restauración y exportación Excel.
- [ ] Escenario: crear, editar sin afectar realidad, diff, aplicar selección y undo.
- [ ] Revisar logs: sin `JavaScript error`, sin fallo SVG y cinco `SVG cargado`.

## Centro de Problemas

### Vista y filtros

- [ ] Abrir **Problemas** desde la sidebar y comprobar contadores críticos/advertencias/información/total.
- [ ] Pulsar **Revalidar**, comprobar estado ligero y fecha de última validación.
- [ ] Filtrar por severidad, regla, plano, entidad y texto; limpiar filtros.
- [ ] Seleccionar un problema y comprobar título, regla, mensaje, campo, entidad, plano, relacionados y acción sugerida.
- [ ] Verificar el estado vacío cuando el contexto no tenga resultados.
- [ ] Forzar en una copia aislada un error del bridge y comprobar estado de error/reintento sin presentar resultados como actuales.

### Navegación y contexto

- [ ] Navegar desde cada entidad relacionada a su puesto y volver a Problemas.
- [ ] Usar **Ver en plano** y comprobar plano, selección, centrado, zoom, inspector y highlight de problema.
- [ ] Cambiar Reality → Escenario → Reality y confirmar que los resultados corresponden solo al contexto activo.
- [ ] Seleccionar un problema, cambiar contexto y comprobar que se limpia si ya no existe.

### Mapa, inspector y lista

- [ ] Activar/desactivar capa **Problemas** y verificar símbolo/borde por severidad máxima.
- [ ] Seleccionar puesto con problemas, revisar sección Problemas del inspector y pulsar **Ver problemas**.
- [ ] Revisar columna **Calidad** de Lista, tooltip/texto accesible y severidad máxima.
- [ ] Comprobar badge compacto de Problemas en sidebar, incluidos críticos.

### Corrección, teclado y responsive

- [ ] En una copia aislada, corregir una roseta duplicada, guardar y confirmar que la revalidación elimina el problema sin recargar.
- [ ] Lista de Problemas: ArrowUp/ArrowDown, Enter y Escape; comprobar foco coherente.
- [ ] Compacto: detalle como drawer. Normal: lista + detalle. Amplio: filtros + lista + detalle.

## Escenarios / Compare

- [ ] Abrir Escenarios sin contexto activo y comprobar el estado vacío.
- [ ] Crear escenario, abrirlo y editar el draft en una copia aislada; actualizar la comparación.
- [ ] Cambiar REALIDAD ↔ ESCENARIO y volver a Realidad sin conservar cambios del contexto anterior.
- [ ] Comprobar contadores ADDED, REMOVED, MOVED, MODIFIED y campos afectados.
- [ ] Filtrar por tipo, plano y texto; limpiar filtros. Comprobar Differences Only si se incorpora en una versión posterior.
- [ ] Abrir cada detalle y comprobar before/after, `changedFields`, celdas origen/destino y navegación al plano.
- [ ] Revisar impacto de validación: introducidos, resueltos y persistentes; abrir un problema introducido.
- [ ] Marcar/desmarcar cambios y comprobar que la selección para aplicación parcial se conserva.
- [ ] Revisar confirmación previa y aplicar parcialmente solo en datos aislados; verificar historial, backup y undo cuando corresponda.
- [ ] Teclado de lista y controles Compare: ArrowUp/ArrowDown, Enter, Escape y foco.
- [ ] Responsive: 1280×720, 1366×768, 1440×900, 1920×1080, ultrawide, DPI 125 %/150 %, overflow, drawers y paneles.

## Requisitos globales de interfaz / UX

> Obligatorio para Movement Planner y para toda pantalla modificada. Estado: **manual pendiente**; no sustituible por pruebas automáticas o smoke tests.

### UI general

- [ ] Ninguna palabra cortada.
- [ ] Ningún botón cortado.
- [ ] Ningún menú fuera de viewport.
- [ ] Ningún dropdown cortado.
- [ ] Ningún panel solapado.
- [ ] Ningún badge solapado.
- [ ] Sidebar usable.
- [ ] Inspector usable.
- [ ] Planner usable cuando esté activo.
- [ ] Contexto siempre visible: Reality/Escenario, escenario activo, plano, modo y selección.

### Responsive

- [ ] 1280×720.
- [ ] 1366×768.
- [ ] 1440×900.
- [ ] 1600×900.
- [ ] 1920×1080.
- [ ] 1920×1200.
- [ ] 2560×1440.
- [ ] Ultrawide 3440×1440.
- [ ] 4K 3840×2160.

### Windows DPI

- [ ] 100 %.
- [ ] 125 %.
- [ ] 150 %.
- [ ] 175 %.
- [ ] 200 %.

### Accesibilidad visual

- [ ] Estados distinguibles sin depender exclusivamente del color.
- [ ] Libre identificable.
- [ ] Ocupado identificable.
- [ ] Reservado identificable.
- [ ] Critical identificable.
- [ ] Warning identificable.
- [ ] Info identificable.
- [ ] Source identificable.
- [ ] Destination identificable.
- [ ] Reality identificable.
- [ ] Scenario identificable.
- [ ] Contraste suficiente entre texto, fondo, selección y marcadores.

### Solapamientos y densidad de mapa

- [ ] Zoom global.
- [ ] Zoom operativo.
- [ ] Zoom detalle.
- [ ] Badges.
- [ ] Labels.
- [ ] Problemas.
- [ ] Planner.
- [ ] Diff.
- [ ] El mapa conserva área útil cuando el Planner sustituye el inspector.
- [ ] La información por marcador respeta el zoom semántico y no acumula indicadores incompatibles.

## Movement Planner

> Estado: **manual pendiente**. Crear y aplicar escenarios de prueba únicamente sobre una copia aislada; nunca sobre `runtime-data` operativo.

- [ ] Iniciar Planner desde multiselección en REALIDAD; comprobar contador seleccionado/movible/no utilizable.
- [ ] Confirmar que un escenario activo no inicia un nuevo plan hasta volver a REALIDAD.
- [ ] Revisar orígenes y `displayLocation`; ningún `N-01`/ID técnico debe ser la etiqueta principal.
- [ ] Entrar en selección de destinos, elegir puestos libres y comprobar ● origen, ◎ destino y × no disponible sin depender solo de color.
- [ ] Seleccionar menos destinos: distinguir claramente Sin destino de Bloqueado.
- [ ] Generar propuesta, revisar lista, problemas relacionados y preview de un movimiento seleccionado en mapa.
- [ ] Usar Ver origen / Ver destino y confirmar que reutiliza navegación del mapa sin abrir inspector junto al Planner.
- [ ] Cambiar manualmente un destino, probar destino reservado/ocupado y revisar el bloqueo devuelto.
- [ ] Excluir un origen sin destino y confirmar que no se borra ni altera la asignación.
- [ ] Crear escenario sobre copia aislada, verificar que REALIDAD no cambia, que se activa el escenario y se abre Compare.
- [ ] Revisar Compare: movimientos, before/after, validation y selección parcial existente.
- [ ] Compacto: Planner como drawer; normal/amplio: Planner sustituye al inspector y el mapa conserva área útil.
- [ ] Planner: Enter, Escape, foco visible y controles alcanzables por teclado.
- [ ] Planner en 1280×720, 1366×768, 1920×1080, ultrawide y 4K; DPI 125 %, 150 % y 200 %.
- [ ] Sin texto, botón, menú, popover, badge o panel cortado/solapado durante el flujo.

## Analítica espacial y Heatmaps

> Estado: **manual pendiente**. La capa es derivada y no debe modificar datos, escenarios ni selección.

### Heatmap

- [ ] Activar/desactivar **Análisis espacial** y comprobar que no bloquea clics de puestos, pan ni zoom.
- [ ] Ocupación: validar leyenda, intensidad y equivalencia con los puestos ocupados del plano.
- [ ] Disponibilidad: validar leyenda, intensidad y equivalencia con puestos libres.
- [ ] Problemas: validar que Critical/Warning/Info mantienen prioridad respecto a los marcadores individuales.
- [ ] Cambios de escenario: activar un escenario de copia aislada y validar puntos contra Compare.
- [ ] Confirmar que Planner oculta visualmente la capa sin cambiar la preferencia del usuario.
- [ ] Zoom global, operativo y detalle: SVG reconocible, labels legibles y heatmap con menor protagonismo en detalle.

### Analítica y Compare

- [ ] Abrir Analítica: total, ocupados, libres, reservados, porcentajes y problemas coinciden con estado efectivo.
- [ ] Tabla por plano: navegar al plano y abrir Problemas filtrados desde cada fila.
- [ ] REALIDAD → ESCENARIO → REALIDAD: resumen y overlay cambian sin mezclar contextos.
- [ ] Compare: misma escala para Reality/Escenario y deltas de tasa expresados en pp.
- [ ] Verificar representación numérica y leyenda sin depender solo de color.

### Responsive y accesibilidad

- [ ] Analítica y selector heatmap en 1280×720, 1366×768, 1440×900, 1920×1080, ultrawide y 4K.
- [ ] DPI Windows 100 %, 125 %, 150 %, 175 % y 200 %.
- [ ] Selector accesible por teclado, foco visible, leyenda con nombre/unidad/escala y sin menús o paneles cortados.
- [ ] Problems layer y heatmap activos simultáneamente sin saturar ni ocultar indicadores críticos.

## Dashboard operativo

> Estado: **manual pendiente**. El Dashboard es una representación derivada: las comprobaciones se realizan sin alterar `runtime-data`.

### Contexto y datos

- [ ] Abrir Dashboard en **REALIDAD** y comprobar total, ocupados, libres, reservados, ocupación y disponibilidad contra Analítica espacial.
- [ ] Abrir un escenario de copia aislada y comprobar el contexto `ESCENARIO · nombre`, el estado efectivo y el retorno a REALIDAD sin mezcla de datos.
- [ ] Comprobar problemas total, × Critical, ! Warning e i Info contra el Centro de Problemas.
- [ ] Comprobar resumen por plano: ocupación, libres, problemas y barra con porcentaje textual.
- [ ] Revisar ranking **Más disponibilidad**: orden y etiqueta de porcentaje de puestos libres.
- [ ] Comprobar **Requiere atención** y su estado vacío ✓ cuando no haya problemas.
- [ ] En escenario, comprobar Added, Removed, Moved, Modified y ValidationImpact contra Compare.
- [ ] Abrir Dashboard mientras Scenario Diff carga: debe indicar actualización y no mostrar cero cambios como resultado confirmado.
- [ ] Revisar estados vacíos sin analítica, sin problemas y sin escenario activo.

### Navegación y accesibilidad

- [ ] Pulsar Libres, Ocupados y Reservados; comprobar lista y filtro rápido correcto.
- [ ] Pulsar cada severidad y total de Problemas; comprobar filtros en Centro de Problemas.
- [ ] Pulsar cada plano y disponibilidad; comprobar mapa activo, sin crear otra navegación.
- [ ] Pulsar Analítica espacial, Escenarios y Comparar escenario.
- [ ] Recorrer tarjetas y widgets con Tab/Shift+Tab; activar acciones con Enter y Space; comprobar foco visible y orden lógico.
- [ ] Verificar que ×, !, i, ✓, texto, bordes y patrones permiten interpretar estados sin color.
- [ ] Verificar que no hay texto, botón, card, menú ni panel cortado/solapado.

### Responsive y DPI

- [ ] 1280×720.
- [ ] 1366×768.
- [ ] 1440×900.
- [ ] 1920×1080.
- [ ] Ultrawide.
- [ ] 4K.
- [ ] DPI 100 %, 125 %, 150 %, 175 % y 200 %.
- [ ] Ancho amplio: seis KPI y widgets en dos columnas; ancho normal: tres KPI; compacto: widgets en una columna sin compresión ilegible.

## Milestone 9 — Release hardening

> Estado: **manual pendiente**. Los resultados automáticos y de smoke están en `QA_EXECUTION_REPORT.md`; no sustituyen esta revisión.

### Navegación, overlays y foco

- [ ] Recorrer la sidebar completa: Dashboard, Plano, Puestos, Personas, Equipos, Problemas, Escenarios, Analítica espacial e Historial. Confirmar etiqueta completa en ancho normal, abreviatura única con tooltip en compacto y estado explícito para Personas/Equipos si siguen deshabilitados.
- [ ] En compacto, abrir Selección, Filtros y Capas desde el mapa: los controles siguen disponibles y el menú **Más** queda por encima de drawers/paneles.
- [ ] Abrir búsqueda, redimensionar la ventana y comprobar que el popup permanece anclado al campo y dentro del viewport. Pulsar Escape para cerrar búsqueda, menú contextual y menú Más.
- [ ] Navegar con Tab/Shift+Tab y confirmar foco visible en mapa, `summary`, botones, tarjetas y controles; no tabular cada pin individual del mapa.
- [ ] Abrir/cerrar inspector, drawer, diálogo, Planner y detalle de Problemas: el foco inicia en un control útil y vuelve al disparador cuando corresponde.

### Mapa, densidad y referencias

- [ ] En cada plano, revisar global/operativo/detalle con selección, búsqueda, Problemas, Diff, Planner y Heatmap de forma combinada. Debe predominar el estado de mayor prioridad sin ocultar los indicadores críticos.
- [ ] Confirmar que la referencia humana principal es `displayLocation` (`A-01`…`X-18`), no el ID técnico `N-01`.
- [ ] Revisar explícitamente Sur y Nivel 3: el informe automático detectó 62 celdas `displayLocation` compartidas (57 en Sur, 5 en Nivel 3). Registrar si las coincidencias afectan comprensión o selección; no cambiar la rejilla durante QA.
- [ ] Cambiar de plano, pan, zoom, maximizar/restaurar, Snap y cambiar de monitor; conservar aspecto, hitbox de pin, selección y contexto cuando la entidad exista.

### Persistencia y seguridad — solo copia aislada

- [ ] Simular error de acceso/archivo bloqueado o JSON inválido en una copia aislada. Confirmar mensaje claro y que la aplicación no reemplaza datos dañados por un conjunto vacío.
- [ ] Crear, editar y aplicar parcialmente un escenario; comprobar que Reality permanece intacta hasta Apply, que aparece backup/historial y que Undo restaura el estado esperado.
- [ ] Revisar listado de backups con un ZIP deliberadamente corrupto en copia aislada: los backups válidos siguen apareciendo y el error se registra sin bloquear la vista.
- [ ] Con dos instancias sobre datos de prueba compartidos, comprobar el comportamiento de `.lock`, `commit.pending` y conflicto de `baseRevision` al aplicar un escenario. Registrar el resultado; no probar sobre datos operativos.
- [ ] Cerrar por UI con y sin operación terminada y verificar al reabrir que no se muestran errores tardíos. Con herramientas del sistema, confirmar que no queda el proceso principal ni hijos WebView2 de esa instancia.

### Registro de resultado

Para cada fallo, anotar: versión/commit, Debug o Published RC, resolución/DPI, contexto Reality/Escenario, pasos exactos, resultado observado y captura si aporta evidencia. Mantener **PENDING** cualquier fila no ejecutada por una persona.
