# Auditoría funcional — Spatial Git aislado

## Alcance y despliegue

`PlanosV3-windows-spatial-git-isolated` es una aplicación WPF (.NET 8 + WebView2) autocontenida. Usa la ruta indicada por el `config.json` situado junto al ejecutable; la instalación actual apunta a `runtime-data` de esta copia y no accede a las variantes macOS ni a sus datos compartidos.

Todos los usuarios, incluidos los perfiles configurados con `readOnly`, requieren permiso NTFS de modificación sobre `<networkRoot>/<dataFolder>` para adquirir el bloqueo de concurrencia. Un perfil `readOnly` puede leer y adquirir el bloqueo, pero no guardar, recuperar una transacción pendiente ni ejecutar cambios reales.

## Datos operativos y concurrencia

```text
maps.json         Planos, puestos y recursos visuales
assignments.json  Asignaciones reales
positions.json    Posiciones reales
people.json       Catálogo de personas
devices.json      Catálogo de dispositivos
locations.json    Catálogo de ubicaciones
scenarios.json    Borradores aislados
events.json       Historial append-only de cambios confirmados
state.json        Revisión global monótona
.lock             Bloqueo exclusivo transitorio
commit.pending    Marcador transitorio de recuperación
```

Cada operación pública toma `data/.lock` en modo exclusivo. Dos guardados ordinarios se serializan: la segunda operación relee el estado al entrar en la sección crítica y confirma sobre la revisión vigente, sin pérdida silenciosa. Si el bloqueo no se adquiere tras diez segundos, la operación falla con un mensaje visible y conserva el formulario.

Las transacciones reales crean un backup, escriben `commit.pending`, publican los documentos, avanzan `state.json` y eliminan el marcador. Al arrancar, una transacción pendiente se confirma, descarta o revierte según el estado observado. La recuperación se bloquea en perfiles `readOnly` para no modificar datos desde esos perfiles.

## Escenarios, historial y deshacer

Los escenarios almacenan un estado base y un borrador; no modifican la realidad hasta aplicar cambios seleccionados desde Diff. `applyScenario` valida su `baseRevision`: si alguien modificó la realidad desde que se creó el escenario, rechaza la aplicación sin borrar el borrador. El operador debe recargar, revisar el Diff y volver a aplicar.

El historial es append-only. Deshacer y restaurar crean sus eventos correspondientes. Una restauración iniciada por usuario repone únicamente el estado de realidad:

```text
maps.json
assignments.json
positions.json
```

No rebobina `events.json`, `scenarios.json`, `state.json` ni los catálogos.

## Backups y retención

Los backups nuevos son ZIP y contienen explícitamente:

```text
maps.json          assignments.json   positions.json
events.json        scenarios.json     people.json
devices.json       locations.json     state.origin.json
manifest.json
```

El manifiesto incluye marca UTC, revisión de origen, usuario, motivo y el conjunto transaccional. La aplicación sigue listando y restaurando tres formatos: directorios heredados de tres ficheros, directorios A3 con `files` declarados y ZIP B6.

`backupRetentionMode` está en `disabled` por defecto. `report` clasifica sin borrar y escribe `backup-retention-<timestamp>.json` en la carpeta de logs. `delete` no está activado. El informe operativo puede generarse así, tras configurar temporalmente `backupRetentionMode: "report"`:

```powershell
.\PlanoOpenSpaceIT.Windows.exe --backup-retention-report
```

La política protege los backups referenciados por eventos no deshechos dentro de los últimos 50 eventos, conserva los heredados y usa marcas UTC del manifiesto para los nuevos backups.

## Logging operativo

Los logs JSON por línea se escriben en:

```text
<networkRoot>/<logsFolder>/audit-<usuario>-<equipo>-<pid>.log
```

Incluyen ciclo de vida, acción del puente, resultado, duración, revisiones, backup, transacción, espera o agotamiento del bloqueo, bootstrap de estado y rama de recuperación. No incluyen nombres de personas, rosetas, ubicaciones, dispositivos, notas ni payloads. Los informes de retención e integridad se guardan también en esa carpeta. La rotación usa `logMaxFileSizeBytes` y `logMaxHistoryFiles`; un fallo de logging no bloquea el trabajo.

## Integridad y estado visual

**Más → Verificar integridad** ejecuta un informe de solo lectura bajo bloqueo. Informa de referencias de catálogo inexistentes, rosetas duplicadas, marcas históricas de ocupación sin asignación, asignaciones sin puesto y posiciones huérfanas. No corrige datos.

El estado del plano se deriva únicamente de la asignación vigente:

| Estado | Regla |
|---|---|
| `free` | No existe asignación activa con persona |
| `occupied` | Asignación válida con persona |
| `reserved` | Asignación válida con `status: reserved` |
| `inconsistent` | Alguna referencia de catálogo de la asignación no existe |

`maps[].seats[].type` es una marca heredada del dibujo, no estado vivo. Una marca histórica `occupied` sin asignación se muestra como `free` y se informa por separado.

Al guardar, persona, dispositivo y ubicación cambiados deben existir en sus catálogos. Roseta y dispositivo son únicos y bloquean duplicados. Una persona repetida se guarda con advertencia. Las referencias heredadas inválidas que no se editan se preservan y avisan, para permitir corregirlas por partes.

## Interfaz

La aplicación incluye cinco planos, zoom, pan, cuadrícula lógica 24×18, creación/movimiento/borrado de puestos, asignaciones, escenarios, Diff, historial, backups, deshacer y exportación Excel. Por debajo de 1100 px, Diff, Historial, Backups y Exportar permanecen accesibles en el menú **Más**.

La exportación Excel abre un selector nativo antes de tomar el bloqueo. Empieza por la última carpeta local del usuario o `Documentos`; la preferencia se guarda en `%AppData%\PlanoOpenSpaceITSpatialGitIsolated\user-preferences.json`, nunca en `config.json`. La instantánea se toma bajo bloqueo y el XLSX se genera tras liberarlo. Cancelar no crea fichero ni se registra como error.

## Verificación y publicación

```powershell
dotnet test ..\tests\PlanoOpenSpaceIT.Windows.Tests\PlanoOpenSpaceIT.Windows.Tests.csproj
dotnet publish PlanoOpenSpaceIT.Windows.csproj -c Release -r win-x64 --self-contained true -o publish
```

`bin/`, `obj/` y `publish/` son salidas regenerables. La publicación parte de un directorio de destino limpio y no cierra procesos: si un archivo está bloqueado, falla visiblemente.
