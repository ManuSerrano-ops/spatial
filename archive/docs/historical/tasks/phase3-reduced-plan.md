# Plan reducido de cierre — Fase 3

## Alcance descartado

- **B1** queda descartado: las escrituras ordinarias releen el estado dentro de la sección crítica; sólo `applyScenario` valida `baseRevision` y su rechazo con recarga es suficiente.
- **A9** queda descartado: la comprobación de `runtime-data/data/scenarios.json` del 9 de agosto de 2026 encontró dos escenarios, ambos con `draft`.
- La Fase 4 queda fuera del cierre.

## Preflight de integridad

El informe informativo se ejecutó el 9 de agosto de 2026 bajo `DataStore.WithLock`, sin modificar datos operativos. Detectó 5 referencias de persona inexistentes, 5 de dispositivo, 4 de ubicación, 0 rosetas duplicadas, 202 marcas históricas `type: occupied` sin asignación, 0 asignaciones sin puesto y 0 posiciones huérfanas.

`custom-cb420da170c54504a8660f7c29a2df17` tenía valores `test` en los tres catálogos y roseta, con actualización del 30 de julio por `Manu`; no existía un evento que permitiera fechar la creación del puesto. Era un dato de prueba que debía eliminarse mediante la aplicación, sin exclusiones especiales.

## Entrega única B2, B4 y B5

1. **B2 (histórico, sustituido para esta variante):** `getIntegrityReport` permanece como acción de solo lectura y se expone en la interfaz. En la variante UI Figma, persona, dispositivo y ubicación pasaron a texto libre: no se validan ni se informan contra catálogos. Roseta conserva sugerencias; roseta y dispositivo se validan como únicos; persona repetida devuelve advertencia no bloqueante.
2. **B4 (histórico, sustituido para esta variante):** `SeatState.Derive` es la única función de dominio de estado. La asignación vigente es autoritativa; `type` es una marca heredada del dibujo. Sin asignación, el estado es `free`, incluso con `type: occupied`. `reserved` procede de `status: reserved`; el estado no depende de catálogos de persona, dispositivo ni ubicación.
3. **B5:** por debajo de 1100 px, Diff, Historial, Backups y Exportar se mueven al menú «Más», no se ocultan.
4. Las pruebas cubrirán edición parcial de referencias rotas, los cuatro estados derivados, reglas de unicidad, advertencia de persona repetida, informe desde el puente e interfaz responsive.
