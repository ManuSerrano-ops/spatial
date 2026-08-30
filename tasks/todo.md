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
