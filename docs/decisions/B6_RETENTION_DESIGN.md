# B6 — Retención de backups

## Estado inicial

La purga automática se implementará desactivada por defecto. Una actualización nunca borra backups existentes sin activación explícita.

Los manifiestos nuevos incluyen `createdAtUtc` en formato ISO 8601 con offset UTC; B6 calculará la antigüedad exclusivamente desde ese campo. Los manifiestos heredados que sólo tienen `createdAt` se interpretarán como hora local antes de convertirlos a UTC, por compatibilidad. Esta versión no ejecuta todavía ninguna retención.

## Configuración y política propuesta

`backupRetentionMode` tendrá tres valores: `disabled` (predeterminado), `report` y `delete`. El informe se escribe tanto en el log como en `<networkRoot>/<logsFolder>/backup-retention-<timestamp>.json`.

En `report`, el informe se solicita con la acción de puente `getBackupRetentionReport` o, para la ejecución operativa sin abrir la interfaz, iniciando el ejecutable con `--backup-retention-report`. Ambas rutas son de solo lectura y requieren que la configuración esté en `report`; `disabled` y `delete` rechazan la solicitud.

La eliminación no se habilita en esta entrega: `delete` se reconoce como valor de configuración para preservar el contrato, pero no elimina nada hasta que el operador apruebe el informe real y se implemente y pruebe esa activación por separado.

Las ventanas se calculan en UTC, sin solapamiento, respecto de `now`:

1. **Recientes:** `createdAt >= now - 7 días`; se conservan todos.
2. **Diarias:** `now - 37 días <= createdAt < now - 7 días`; se conserva el **último** backup de cada fecha UTC.
3. **Mensuales:** `now - 12 meses <= createdAt < now - 37 días`; se conserva el **último** backup de cada mes UTC.
4. **Caducados:** `createdAt < now - 12 meses`; son candidatos a purga.
5. Se conserva siempre el backup más reciente, aunque no encaje en ninguna ventana.
6. Se conserva cualquier backup referenciado por un evento no deshecho dentro de los 50 eventos más recientes, aunque no encaje en ninguna ventana.
7. Los backups heredados sin comprimir no se convierten ni se purgan automáticamente.

La retención sólo se aplica al conjunto restante después de proteger los backups de la cadena de undo. `report` nunca borra; `delete` elimina sólo los candidatos resultantes.

## Ejecución y seguridad

- La purga es posterior a un commit correcto, fuera de la transacción que protegió el commit.
- Un perfil `readOnly` nunca ejecuta purga.
- Un fallo de purga no falla la operación del usuario; se registra y continúa.
- El log registra cada backup borrado, la ventana de retención por la que quedó fuera y bytes liberados.

## Implementación por etapas

1. Definir una lista explícita de los ocho ficheros operativos: `maps.json`, `assignments.json`, `positions.json`, `events.json`, `scenarios.json`, `people.json`, `devices.json` y `locations.json`. Los archivos de `data/_archive/` nunca entran.
2. Los backups nuevos se almacenan como ZIP con `manifest.json` ampliado y `state.origin.json`. El ZIP conserva los ocho ficheros; `state.origin.json` es informativo y no restaurable.
3. La restauración iniciada por usuario sigue limitada a mapas, asignaciones y posiciones. Los backups heredados de directorio y tres/cuatro ficheros siguen restaurándose sin conversión.
4. Añadir `backupRetentionMode`, inicialmente `disabled`, y un informe de clasificación independiente del borrado.
5. Ejecutar el informe real en `report`; no activar `delete` hasta aprobación explícita.

## Primera activación

Antes de activar borrado real, la política se ejecutará contra `runtime-data` real en modo informe. El informe debe contener:

- backups candidatos a borrar, con id, fecha, tamaño y ventana de exclusión;
- total protegido por referencias de los últimos 50 eventos no deshechos;
- total que permanece retenido;
- espacio potencialmente liberado.

La eliminación real requiere confirmación posterior del operador.
