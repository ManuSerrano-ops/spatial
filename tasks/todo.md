# Pendientes

## Rediseño visual UI Figma

- [x] Aislado `runtime-data` y la identidad local de la copia.
- [x] Inventario del contrato DOM documentado en `docs/reference/UI_DOM_CONTRACT.md`.
- [x] Propuesta de disposición aprobada.
- [x] Implementación visual, verificación automática y publicación completadas.
- [x] Temas locales, sprite de Lucide sin dependencias en red y checklist único de validación añadidos.
- [x] Pines circulares a cualquier zoom, color por estado y selector vertical de planos; guía de chinchetas restaurada y ayuda de rueda/arrastre retirada del lienzo.
- [x] Persona, dispositivo y ubicación convertidos a texto libre; sólo roseta conserva sugerencias. Se retiraron la validación, el informe y el estado visual basados en esos catálogos, conservando unicidad de roseta y dispositivo.
- [x] Indicador del visor simplificado: en reposo muestra únicamente el porcentaje de zoom; los mensajes temporales de operación mantienen prioridad.
- [x] Mensaje de roseta duplicada: identifica roseta, puesto, posición, persona, equipo y ubicación de la asignación existente.
- [x] Correcciones de interacción: cerrar ficha tras borrar puesto, cerrar menú contextual al cancelar alta y derivar el color de estado de los cuatro campos de la asignación.
- [x] Checklist manual breve de caza de bugs creada en `docs/qa/BUG_HUNT_MANUAL_CHECKLIST.md`.
- [x] Desbordamiento horizontal en resoluciones intermedias corregido: la cabecera reduce y trunca sus controles antes de expandir la página, sin ocultar acciones.

## Exportación Excel desde el plano

- [x] Exportación ajustada al inventario fijo de `inventarionormal.xlsx`: mantiene filas, estructura, estilos y orden; limpia ocupación heredada y rellena sólo desde el plano. Las rosetas sólo presentes en el plano no crean filas y las duplicadas bloquean la exportación.

## Defectos resueltos durante el rediseño

- [x] **P1 · Botón Aplicar seleccionados mal formado en `Resources/index.html`.** La etiqueta vuelve a estar dentro del botón `#apply`; el contrato de `app.js` conserva el manejador `applySelected` y queda cubierto por la prueba de interfaz embebida.

## Defectos anotados

- [ ] **P3 · Diálogo de forma de cluster inalcanzable.** `#cluster-shape-dialog` está declarado en `Resources/index.html`, pero no tiene ninguna ruta de apertura con `showModal()`, `show()` ni `open`; `#apply-dialog` no es un diálogo, sino el botón de aplicar dentro de `#diff-dialog`. No eliminar ni conectar este código fuera de una tarea dedicada.
- [ ] **P2 · Falso verde en `release-readiness-harness.js`.** Informa 62 colisiones visibles de `displayLocation`, pero solo comprueba que el informe se genere (`Array.isArray(collisions)`). La prueba pasa diga lo que diga el informe, por lo que no prueba la condición de readiness que su nombre promete. Las 62 son celdas de la rejilla humana `A–X / 01–18` con más de un puesto tras discretizar coordenadas: 5 en Nivel 3 y 57 en Sur; no son colisiones de IDs. Definir y asertar el criterio operativo aceptable en una tarea específica, sin cambiarlo durante la migración a `node:test`.
- [ ] **P3 · Asimetría de auditoría entre validación y analítica.** `RunValidation` registra `validation.started` y `validation.finished`; `RunSpatialAnalytics` sólo registra `analytics.finished`. La caracterización 6.3a fija el comportamiento actual. Decidir si la ausencia de inicio es intencional como diseño de telemetría; no añadir `analytics.started` durante la extracción de `ReportService`.
- [ ] **P2 · El fallo de escritura de integridad no deja auditoría si `logs/` está inaccesible.** `GetIntegrityReport` elimina `reportPath` e intenta `integrity.report.failed`, pero `SafeLogger` usa la misma carpeta fallida y no puede persistir el evento. La caracterización 6.3a lo demuestra con `logs` como fichero. Diseñar un canal de degradación independiente si se necesita trazabilidad de fallos de logging; no cambiarlo durante 6.3.
- [ ] **P1 · Integridad de backups: `GetBackups()` publica manifiestos estructuralmente incompletos.** La caracterización 6.2a demuestra que un ZIP legible cuyo `manifest.json` es `{}` se lista como backup, aunque no tenga `id` ni fecha y no pueda restaurarse; en cambio, un ZIP ilegible sí se descarta. Un backup corrupto puede aparecer como restaurable. Validar la estructura mínima del manifiesto y decidir cómo informar los archivos inválidos en una tarea separada. No corregir durante la extracción de `BackupService`.
- [x] **Deuda de pruebas · Quinta aserción textual retirada en el bloque 6.** `managed-area-create-workspace-harness.js` buscaba la forma interna `ExecuteTransactionUnlocked`; tras 6.1, se retiró en vez de cambiarla por `_transactions.Execute`. La garantía funcional ya está cubierta por `ManagedAreasTests.CreateWorkspaceInAreaIsAtomicAndUndoable`: crea un puesto dentro del área, comprueba membresía, backup, historial y undo conjunto. El baseline de migración conserva 1.005 aserciones; el árbol actual tiene 1.003 por la retirada justificada de esas dos aserciones de forma. Regla para 6.2–6.5: ante cada arnés roto por un refactor, decidir primero si comprueba comportamiento o forma.
- [ ] **Investigación pendiente · Compatibilidad de backups heredados en producción.** Las copias locales `Portable/runtime-data` y `qa-runtime-data` contienen 63 ZIP cada una, todos con `createdAtUtc`, y cero directorios heredados; no son evidencia del despliegue real. Quien tenga acceso a los datos de producción debe comprobar si existen directorios de backup heredados o manifiestos sin `createdAtUtc`. Si tampoco existen allí, planificar la retirada de esa compatibilidad para adelgazar `BackupService`; no declararla código muerto ni eliminarla antes de esa comprobación.
- [ ] **Mantenimiento diferido · Evaluar Git LFS para planos SVG grandes.** El job visual descarga los SVG completos versionados: `plano_norte_limpio.svg` (~2,30 MB) y `plano_sur_limpio.svg` (~6,47 MB). Ese coste se paga en cada ejecución del job visual, además del clon inicial. No migrar sin comprobar la compatibilidad de GitHub Actions, la publicación y la extracción de recursos embebidos.
- [ ] **Mantenimiento diferido · Definir finales de línea con `.gitattributes`.** El repositorio no incluye `.gitattributes`; en Windows Git avisa que varios arneses LF se convertirán a CRLF según la configuración local. Definir la política al tocar la higiene del repositorio para evitar diffs espurios entre clones, sin renormalizar ficheros como cambio aislado.
- [ ] **P2 · `bulk-dialog` no limita estructuralmente la lista de exclusiones.** `#bulk-exclusions` solo declara margen, sangría y color en `Resources/app.css`; a diferencia de `.diff-list` y `.activity-list`, no tiene `max-height` ni `overflow`. La medición a 900×460 en alto contraste verificó que el flujo actual agrupa exclusiones por motivo: 5 y 100 puestos ocupados generan una sola fila («N: Puesto ocupado.»), un diálogo de 650×235 px y acciones dentro del viewport. Por ello no existe un umbral actual por cantidad de exclusiones. Mantener la tarea preventiva: si aumentan las categorías de motivo, la lista puede crecer sin límite y ocultar Cancelar/Aplicar; añadir región desplazable en una tarea independiente posterior al bloque 4.
- [ ] **4.4 · Barra de acciones masivas solapada por el panel de detalle por debajo de 1100 px de ancho.** La cabecera de `#detail-panel` mantiene aproximadamente 279 px de ancho desde el borde derecho y la barra `#bulk-apply` se posiciona sin reservar ese espacio. Matriz medida con selección masiva real: 900×460 claro, Aplicar `(592,105)–(684,140)` y cabecera `(621,50)–(900,155)`, solapan; 900×460 alto contraste, `(629,110)–(733,149)` frente a `(621,50)–(900,155)`, solapan; 1100×560 claro y alto contraste, no solapan; 1400×900 claro y alto contraste, no solapan. Es consecuencia del mínimo propuesto, no defecto preexistente. Antes de bajar el mínimo, decidir si la barra debe reubicarse o conocer el ancho del panel abierto: ambos compiten hoy por el mismo espacio sin coordinarse. No corregir dentro de 4.3.
- [ ] **Bloque 6 · Tipos de dominio expuestos por firmas del ejecutable.** `MovementPlannerTests` necesita acceso de amigo a `PlanoOpenSpaceIT.Domain` porque prueba métodos del ejecutable cuyas firmas usan `ValidationResult` y `ScenarioDiffChange`. No es un problema operativo actual. Al extraer los servicios de `DataStore`, comprobar si `PlanoOpenSpaceIT.Windows.Tests` puede dejar de ser amigo del dominio: si ocurre, la nueva frontera estará aislando correctamente Desktop de Domain. Resultado de 6.1: `TransactionCoordinator` compila aislado en `net8.0`, sin WPF, pero las diez pruebas transaccionales siguen en `Desktop.Tests` porque instancian la fachada `DataStore` del ensamblado Windows. Al completar `BackupService` y los servicios restantes, comprobar si pueden bajar a un proyecto sin WPF; mientras sigan allí, la frontera de infraestructura seguirá incompleta.
- [ ] **Mantenimiento diferido · `InternalsVisibleTo` duplicado en `src/Properties`.** `InternalsVisibleTo.cs` y `AssemblyInfo.cs` declaran ambos acceso para `PlanoOpenSpaceIT.Windows.Tests`. Es inocuo, pero confuso; consolidarlo al tocar esa zona por otro motivo, no como cambio aislado.
- [ ] **P2 · Coordenadas de puestos extremadamente próximas en Sur.** A 88,67 % de zoom inicial, la mediana de distancia al vecino más próximo es 22,90 px y hay 29 pares únicos por debajo de 20 px. Los cinco mínimos (7,38–13,34 px) son una cola extrema y sus IDs saltan uno o dos números, patrón que apunta a coordenadas de captura erróneas más que a mesas consecutivas. Contrastar con el plano físico y añadir validación de separación si se confirman anomalías; no corregirlos desde CSS.

  | Puesto A | Coordenadas A | Puesto B | Coordenadas B | Distancia |
  |---|---|---|---|---:|
  | `S-E19` | `(0,56907105, 0,53004736)` | `S-E21` | `(0,57587355, 0,53034848)` | 7,38 px |
  | `S-O23` | `(0,63909388, 0,51705474)` | `S-O26` | `(0,64229321, 0,50725216)` | 8,28 px |
  | `S-O40` | `(0,76605028, 0,45604771)` | `S-O42` | `(0,76574999, 0,47045505)` | 11,06 px |
  | `S-O19` | `(0,61407042, 0,46503136)` | `S-O22` | `(0,62147075, 0,45162579)` | 13,05 px |
  | `S-O37` | `(0,74313742, 0,46604976)` | `S-O39` | `(0,74383202, 0,44868491)` | 13,34 px |
- [ ] **P3 · I+D y Quality Control no tienen puestos definidos.** `runtime-data/data/maps.json` configura `id` y `qc`, pero ambos contienen cero `seats`. Confirmar si son planos previstos sin inventario o datos operativos incompletos.
- [x] **3.6 · WCAG 2.1.1: coordenadas operables con teclado.** Mover y Añadir puesto admiten cursor de rejilla `A–X` / `01–18`, flechas, Intro y Escape, sin sustituir el clic de precisión continua. Decisión explícita de granularidad: el ratón conserva coordenadas continuas; Mover con teclado ajusta al centro de la celda adyacente en la primera flecha y Añadir con teclado usa centros de celda. Así cursor, coordenada y anuncio coinciden; en los cuatro bordes no hay movimiento ni anuncio falso. `adjacentSeat()` sigue reservado a la navegación ordinaria, porque en Sur puede seleccionar un destino lateral al pulsar una flecha vertical.
- [x] **P2 · Arnés AX de nombres accesibles.** Resuelto en 5.4c con `tools/verify-accessibility-tree.py`: CDP correlaciona selector DOM → `backendNodeId` → nodo AX parcial, sin recuentos globales ni recorridos de subárbol modal. Cubre controles expuestos al cargar, el cierre de Historial tras enfocar y activar su abridor real —incluida la restauración de foco— y el diálogo inalcanzable `#cluster-shape-dialog`, que permanece cerrado e ignorado sin retirar `hidden`.
- [x] **P1 · Distribución incompleta.** Resuelto mediante `deployment/New-DeploymentPackage.ps1` e `Install-PlanoOpenSpaceIT.ps1`: generan un paquete mínimo, inicializan de forma segura los nueve JSON compartidos, validan instalaciones existentes y nunca sobrescriben datos. Diseño en `docs/reference/DEPLOYMENT_DESIGN.md`.


- [x] **P3 · Tokens CSS de estado de pin sin consumo.** Resuelto: las chinchetas usan `--pin-free`, `--pin-occupied`, `--pin-reserved` y `--pin-inconsistent`; se retiró el canal de completitud y las marcas centrales.
- [x] **P2 · Pines libre e inconsistente con contraste práctico insuficiente.** Resuelto: ambos usan relleno de estado y borde contrastado.
- [x] **P2 · No existe una acción de interfaz para crear o retirar una reserva.** Resuelto: selector `Automático` / `Reservado` en la ficha, usando el campo `status` existente.

## Fase 3 — alcance descartado

- [x] B1 — conflictos por entidad y campo: descartado. Las escrituras reales ordinarias releen el estado dentro de la sección crítica y no validan una revisión del cliente; cambios sobre puestos distintos se serializan sin pérdida ni recarga. El bloqueo global sólo rechaza `applyScenario` ante una `baseRevision` desactualizada, una operación ocasional cuyo flujo de recarga y revisión del diff es prudente y suficiente.
- [x] A9 — migración de escenarios heredados: descartada. `runtime-data/data/scenarios.json` contiene dos escenarios y ambos tienen `draft`; no hay formato heredado que migrar.

## Fase 3 reducida — preflight de integridad

- [x] Ejecutado `getIntegrityReport` contra `runtime-data` bajo bloqueo, sin modificaciones operativas. El puesto de prueba `custom-cb420da170c54504a8660f7c29a2df17` no tiene exclusión especial: se informa como cualquier otra referencia rota hasta eliminarlo mediante la aplicación.
- [x] B2, B4 y B5 entregados como una unidad: validación parcial por campos enviados, unicidad de roseta y dispositivo, advertencia de persona repetida, informe visible en interfaz, estado derivado y menú «Más» responsivo. Los datos actuales no se corrigen automáticamente.

## Exportación Excel — carpeta por usuario

- [x] Selector nativo, preferencia local en AppData, cancelación normal, comprobación de escritura y generación posterior a liberar el bloqueo.

## Interacción del plano

- [x] Corregido el arrastre del plano: el visor completo captura el gesto de desplazamiento y cancela `dragstart`; los marcadores de puestos conservan su propio arrastre.

## Cierre documental y punto de retorno

- [x] `README.md` y `../docs/reference/FUNCTIONALITY_AUDIT.md` actualizados al comportamiento final.
- [x] Resumen de cierre creado en `archive/docs/historical/CLOSURE_SUMMARY.md`.
- [x] Copia final creada fuera del árbol: `G:\Proyecto Planos\phm\phase2-safety-backups\state-final-2026-08-09-1920` (sin salidas regenerables).

## Fase 2 — B6: backups completos y retención

- [x] Backup nuevo ZIP con manifiesto, ocho ficheros explícitos y `state.origin.json`.
- [x] Lectura/restauración compatible de ZIP nuevo y directorios heredados.
- [x] `backupRetentionMode: disabled|report|delete`, desactivado por defecto. `delete` permanece intencionadamente sin activar hasta la aprobación del informe real.
- [x] Clasificación UTC, protección de últimos 50 eventos no deshechos e informe `report` sobre fixture.
- [x] Ejecutado informe sobre `runtime-data` real en modo `report`; `delete` no activado.

## Fase 2 — revisión diferida de retención

- [ ] Alrededor de noviembre de 2026, ejecutar de nuevo el informe real en modo `report` antes de considerar `delete`. Motivo: el informe del 9 de agosto sólo contenía backups de hasta dos días, con cero candidatos; validó ejecución segura, no las ventanas sobre antigüedad real.

## Fase 2 — prueba manual de concurrencia (aplazada)

La prueba se inició y confirmó la rama de agotamiento del bloqueo, sin `commit.pending` residual. Queda pendiente, no descartada.

Ramas por cubrir:

- Espera de bloqueo liberada antes de diez segundos y operación posterior exitosa.
- Dos instancias reales contra el mismo `runtime-data`, con cambios independientes persistidos y revisiones consecutivas.
- Conflicto de escenario: rechazo por `baseRevision`, borrador conservado, recarga y aplicación posterior exitosa.

Antes de retomarla, usar la copia de seguridad previa y posterior de Fase 2 y seguir la secuencia manual documentada en la conversación.

## Ajustes UX posteriores a M10

- [x] Acciones directas de `Renombrar` y `Ajustar` incorporadas en cada tarjeta de cluster; el ajuste reutiliza tamaño, posición y persistencia local existentes.
- [x] `Capas` trasladado al control flotante contextual del lienzo; no activa pan, selección, zoom ni el menú contextual del mapa.
- [x] Etiqueta redundante `Plano` retirada del selector `Oscuro` / `Claro`.
- [ ] Validar visualmente las acciones y el panel Capas con el EXE sobre `qa-runtime-data`.

## Corrección de arranque WPF

- [x] Corregido `StartupUri` para resolver la ventana compilada bajo `src/Desktop/Host`; el EXE ya no termina con «No se encuentra el recurso `mainwindow.xaml`».
- [x] Harness de contrato de recurso de inicio y publicación regenerada en `publish-current` sobre `qa-runtime-data`.
- [x] Corregida la ruta lógica de JavaScript embebido que duplicaba `js/` al extraer recursos y dejaba el visor en «Conectando…»; publicación corregida en `publish-fixed`.
