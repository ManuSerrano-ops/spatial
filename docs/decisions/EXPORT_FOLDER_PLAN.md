# Exportación Excel — selector de carpeta por usuario

## Decisión

La carpeta de exportación es una preferencia local de usuario, no un dato de despliegue. Se guarda en:

```text
%AppData%\PlanoOpenSpaceITSpatialGitIsolated\user-preferences.json
```

con `exportFolder` y `skipExportFolderPrompt`.

## Secuencia

1. La ventana resuelve la carpeta en el hilo de interfaz, antes de enviar la exportación al puente.
2. Si no hay preferencia, el selector nativo empieza en `Documentos`.
3. Si se eligió «usar siempre», se usa la carpeta guardada sin abrir selector.
4. La carpeta se comprueba como escribible antes de tomar `data/.lock`; si no lo es, se informa y se vuelve a preguntar.
5. Cancelar devuelve una respuesta normal `cancelled`, sin generar XLSX ni `export.excel`.
6. `DataStore.ExportExcel(folder)` toma el bloqueo sólo para leer la instantánea y la revisión, lo libera y genera el XLSX después.

## Costuras de prueba

- `IExportFolderDialog` separa el selector nativo y su mensaje de carpeta no escribible.
- `UserPreferencesStore` encapsula AppData.
- El escritor XLSX se inyecta en `DataStore` para demostrar que se ejecuta después de liberar el bloqueo.

No se crea ni se usa `publish/exports`; una carpeta previa se deja intacta.
