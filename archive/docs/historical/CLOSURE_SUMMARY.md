# Resumen de cierre — Spatial Git aislado

## Entregado

### Fase 1 — aislamiento y operación base

- Variante aislada con recursos, datos operativos y WebView2 propios.
- Publicación autocontenida y limpieza del destino de publicación.
- DTOs con campos permitidos y preservación explícita de campos heredados no enviados.
- Exportación Excel y perfiles `readOnly` estabilizados.

### Fase 2 — seguridad multiusuario

- Bloqueo exclusivo distribuido en `data/.lock` y revisión global monótona en `state.json`.
- Transacciones con `commit.pending`, backup preventivo y recuperación de interrupciones.
- Separación permanente entre conjunto transaccional y conjunto de restauración de usuario.
- Logging JSON seguro, rotado y sin contenidos personales.
- Backups ZIP B6, compatibilidad con directorios heredados/A3 e informe de retención sin borrado.

### Fase 3 reducida — integridad y usabilidad

- Informe de integridad bajo bloqueo, visible desde la interfaz.
- Unicidad de roseta y dispositivo, y advertencia de persona repetida. El rechazo de roseta duplicada identifica la asignación que ya la usa. En la variante UI Figma, persona, dispositivo y ubicación son texto libre; sólo roseta conserva sugerencias.
- Estado derivado `free`, `occupied`, `reserved` e `inconsistent`: libre sin campos, ocupado con persona, dispositivo, ubicación y roseta completos, inconsistente si falta alguno y reservado por `status`. `type` histórico no es autoritativo.
- Menú **Más** para conservar Diff, Historial, Backups y Exportar en ventanas estrechas.
- Selector nativo de carpeta para Excel, preferencia por usuario en AppData y generación fuera del bloqueo de datos.

### Variante UI Figma — rediseño visual aislado

- Copia con `runtime-data`, recursos extraídos, perfil WebView2 y preferencias de exportación propios bajo la identidad `PlanoOpenSpaceITUiFigma`.
- Rediseño visual de barra, ficha, diálogos, menús, listas, controles, foco visible y estados de espera, sin cambiar las acciones del puente ni los SVG o el lienzo de los planos.
- Marcadores de puesto circulares y coherentes, sin deformarse al cambiar de zoom; su color indica el estado y el anillo simple, relleno, doble anillo o borde discontinuo lo refuerzan sin marcas centrales. La selección usa un anillo independiente.
- Persona, dispositivo y ubicación pasan a texto libre; roseta mantiene sugerencias y unicidad. El informe de integridad deja fuera los catálogos.
- Cuatro temas locales persistentes: Claro profesional, Penpot oscuro, Alto contraste y Proyector; todos conservan el plano y los SVG sin variación.
- Sprite local de Lucide 0.468.0 con licencia ISC incluida, iconos `currentColor`, sin red en tiempo de ejecución y sin alterar los pines.
- **P1 resuelto durante el rediseño:** `#apply` tenía el texto fuera del botón. Se restauró la etiqueta dentro del elemento sin cambiar su ID ni su manejador `applySelected`.
- Desplegador seguro: genera un paquete mínimo con EXE, licencia y semilla, e instala la estructura compartida `data`/`backups`/`logs` sin sobrescribir datos existentes. Diseño y operación en `tasks/deployment-design.md`.

## Alcance descartado

- **B1:** conflicto por entidad/campo. Las escrituras ordinarias se serializan y releen el estado bajo bloqueo; no aporta valor proporcional.
- **A9:** migración de escenarios heredados. Los dos escenarios reales ya usan `draft`.
- **Fase 4:** fuera del objetivo de cierre, salvo B5 ya entregado.
- **Retención `delete`:** no activada; el beneficio actual es nulo y requiere revisión posterior.

## Pendientes deliberados

1. **Prueba manual de concurrencia de Fase 2:** espera de bloqueo exitosa, dos instancias reales y conflicto de escenario con recarga posterior.
2. **Revisión de retención alrededor de noviembre de 2026:** ejecutar `report` con backups de edad suficiente antes de considerar `delete`.
3. **Incidencia menor de interfaz no tratada:** el restablecimiento de vista no es suficientemente evidente en la interfaz. Se mantuvo fuera del cierre para no ampliar alcance.

## Evidencia final

- Suite completa de la variante UI Figma: 103 pruebas superadas, 0 errores y 0 omitidas.
- Publicación: `publish/PlanoOpenSpaceIT.Windows.exe`.
- Informe de retención: `runtime-data/logs/backup-retention-20260809T1501397696014Z.json`.
- Informe de integridad: se regenera con **Más → Verificar integridad** o `--integrity-report`.
