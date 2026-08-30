# Inventario funcional del original

> Fuente auditada en solo lectura: `../uifigmastyle`.
> Fecha de auditoría: 2026-08-22.
>
> Este documento describe comportamiento comprobado en código y datos. La UI 2.0 puede cambiar la presentación, pero no debe sustituir las operaciones de negocio aquí indicadas por implementaciones paralelas.

## Alcance y datos actuales

El original es una aplicación WPF .NET 8 con WebView2. Su fuente operativa de datos es JSON y su configuración de desarrollo apunta a `../uifigmastyle/runtime-data`.

- Planos: 5.
- Puestos: 270.
- Posiciones normalizadas: 270.
- Asignaciones vigentes al auditar: 11.
- Datos operativos: `maps.json`, `assignments.json`, `positions.json`, catálogos, `events.json`, `scenarios.json` y `state.json`.
- No usa servidores, CDN ni servicios cloud.

## Registro real de planos

`maps.json` es la configuración central de planos. No se debe duplicar este registro en HTML, JavaScript o C#.

| ID | Nombre | Recurso SVG | Puestos |
|---|---|---|---:|
| `norte` | Open Space Norte | `plano_norte_limpio.svg` | 33 |
| `nivel3` | Open Space Nivel 3 | `plano_nivel3_limpio.svg` | 45 |
| `sur` | Open Space Sur | `plano_sur_limpio.svg` | 192 |
| `id` | I+D | `plano_id.svg` | 0 |
| `qc` | Quality Control | `plano_qc_limpio.svg` | 0 |

Los SVG se sirven en WebView2 desde `https://plano.local/` tras extraerse de recursos embebidos a `%LocalAppData%/PlanoOpenSpaceITUiFigma/Resources`.

## Funcionalidades y clasificación

| Área | Operación real | Clasificación | Invariantes de compatibilidad |
|---|---|---|---|
| Inicio | Carga de configuración y recuperación de transacción pendiente | MANTENER | No mostrar datos antes de recuperar o bloquear una transacción pendiente. |
| Planos | Cargar y cambiar entre 5 SVG configurados por `maps.json` | REDISEÑAR | Mantener IDs, recursos y coordenadas `0..1`. |
| Mapa | Zoom de 10 % a 1.000 %, pan, cuadrícula 24×18 y tooltip | REDISEÑAR | El zoom y la presentación no modifican datos. |
| Puestos | Crear, mover, eliminar y seleccionar puestos | MANTENER / REDISEÑAR | Toda mutación real es transaccional; al borrar se retira la asignación asociada. |
| Asignaciones | Guardar o retirar persona, equipo, ubicación, roseta, notas y reserva | MANTENER / REDISEÑAR | Roseta y dispositivo son únicos; persona repetida es advertencia; referencias heredadas inválidas se preservan. |
| Estado | Derivar libre, ocupado o reservado desde asignación vigente | MEJORAR | No derivar el estado del campo histórico `seats[].type`. |
| Búsqueda | Buscar puesto, persona, dispositivo y roseta | MEJORAR | Debe operar sobre el mismo estado cargado por mapa y escenario. |
| Escenarios | Crear, seleccionar, editar y eliminar borradores aislados | MANTENER / REDISEÑAR | Un escenario nunca altera realidad hasta aplicar cambios. |
| Diff | Comparar base y borrador, seleccionar cambios parciales | MANTENER / MEJORAR | Conservar IDs `assignment|<workstationId>` y `seat|<mapId>|<seatId>`. |
| Aplicar escenario | Aplicar cambios seleccionados contra realidad | MANTENER / REDISEÑAR | Requiere `baseRevision == state.json.revision`; al fallar conserva el borrador. |
| Historial | Consultar eventos append-only | REDISEÑAR | Aceptar esquema heredado y actual de `events.json`. |
| Undo | Deshacer escenario o último cambio real reversible | MANTENER / MEJORAR | Undo real crea evento y una revisión nueva; no borra historial. |
| Redo | Rehacer | MEJORAR | No existe actualmente; no presentar un botón hasta contar con operación segura. |
| Backups | Listar, crear y restaurar copias | MANTENER / REDISEÑAR | Restaurar solo `maps.json`, `assignments.json` y `positions.json`; no rebobinar historial, escenarios, estado o catálogos. |
| Integridad | Informe de rosetas duplicadas, marcas históricas y registros huérfanos | MEJORAR | Es solo lectura de datos; no autocorregir. |
| Exportación | Excel desde plantilla local con selector nativo de carpeta | MANTENER / MEJORAR | Exporta solo realidad confirmada; captura bajo lock y genera XLSX fuera de él. |
| Apariencia | Cuatro temas locales y sprite Lucide local | REDISEÑAR | Sin CDN; preferencias no van a `config.json` compartido. |
| Concurrencia | Lock exclusivo, revisión global y recuperación `commit.pending` | MANTENER | No sobrescribir cambios externos silenciosamente. |
| Diagnóstico CLI | Informes de retención e integridad | MANTENER | Ambos generan log/informe; no son operaciones sin efectos de filesystem. |

No se ha clasificado ninguna funcionalidad comprobada como obsoleta.

## Operaciones del puente WebView2

El contrato actual es un mensaje `{ action, requestId, payload }` y una respuesta con `success`, `data` y `error`. Las acciones de negocio existentes son:

```text
loadInitialData, reloadData,
createScenario, deleteScenario,
saveAssignment, deleteAssignment,
saveSeatPosition, createSeat, deleteSeat,
getScenarioDiff, applyScenario,
getEvents, getBackups, getBackupRetentionReport,
getIntegrityReport, restoreBackup,
getUndoPreview, undoLastChange,
exportExcel
```

El rediseño debe usar estas operaciones o una capa de comandos que las concentre, nunca crear implementaciones de persistencia distintas por vista.

## Persistencia, backups y concurrencia

1. Cada operación pública adquiere `data/.lock` de forma exclusiva.
2. Una transacción real relee estado, crea backup previo y escribe `commit.pending` antes de publicar documentos.
3. Los documentos transaccionales llevan una revisión global monotónica. Undo, restauración y recuperación publican una revisión nueva.
4. El backup ZIP actual incluye ocho documentos operativos, `state.origin.json` y manifiesto. También se admiten formatos históricos de backup.
5. Escenarios guardan `base`, `draft`, `baseRevision` y hasta 50 snapshots de undo.

## Riesgos que condicionan el rediseño

- El extractor previo validaba únicamente `index.html` y su marcador de versión, por lo que una extracción parcial podía ocultar SVG ausentes.
- `maps.json` puede contener atributos históricos y asignaciones parciales. No deben eliminarse ni normalizarse automáticamente.
- Los eventos combinan un formato heredado con el formato actual.
- No existe redo real.
- Los backups no están cifrados ni firmados; los permisos NTFS/SMB siguen siendo la protección operativa.

## Prueba funcional original

La revisión visual y de código identifica el fallo de render que existía en el proyecto de rediseño, no en el original: `Resources/app.js` usaba `pin` antes de inicializarlo. El original se mantuvo en solo lectura y no se modificó durante esta auditoría.
