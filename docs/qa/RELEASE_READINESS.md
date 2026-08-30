# Release readiness — Milestone 9

> Estado de este documento: evidencia técnica y procedimiento. **No sustituye
> QA humana visual, de DPI o de operación sobre datos reales.**

## Estado por módulo

| Módulo | Implemented | Programmatically tested | Debug smoke | Published smoke | Manual QA |
|---|---|---|---|---|---|
| Dashboard | Sí | `dashboard-harness` 13/13 | Sí, M9 | Sí, M9 RC | Pendiente |
| Plano / mapa | Sí | Core UX 14/14; resource audit | Sí, SVG 5/5 M9 | Sí, SVG 5/5 M9 RC | Pendiente |
| Lista / Puestos | Sí | Core UX / filtros | Arranque M9 | Arranque M9 RC | Pendiente |
| Personas | No: entrada deshabilitada | N/A | N/A | N/A | N/A |
| Equipos | No: entrada deshabilitada | N/A | N/A | N/A | N/A |
| Search / filtros / multiselect | Sí | Core UX 14/14 | Arranque M9 | Arranque M9 RC | Pendiente |
| Validation / Problems | Sí | C# 9/9; JS 17/17 | Sí, M9 | Sí, M9 RC | Pendiente |
| Escenarios / Compare | Sí | ScenarioDiff C# 10/10; E2E | Arranque M9 | Arranque M9 RC | Pendiente |
| Movement Planner | Sí, contextual | C# 11/11; JS 10/10; E2E | Arranque M9 | Arranque M9 RC | Pendiente |
| Analítica espacial | Sí | C# 9/9; JS 7/7 | Sí, M9 | Sí, M9 RC | Pendiente |
| Heatmap | Sí | JS 7/7; resource audit | Arranque M9 | Arranque M9 RC | Pendiente |
| Historial / Undo | Sí | E2E aislado | No específico M9 | No específico M9 | Pendiente |
| Export existente | Sí | Contrato existente; no modificado | No específico M9 | No específico M9 | Pendiente |

`Personas` y `Equipos` permanecen deshabilitados de manera explícita: no hay
una vista funcional autónoma en este release y no se presentan como disponibles.

## Revisión de navegación y responsive

- La sidebar conserva las entradas operativas y, debajo de 1200 px, muestra
  abreviaturas únicas (`D`, `M`, `P`, `Pe`, `E`, `!`, `Es`, `A`, `H`) con
  `title` y nombre accesible, en lugar de puntos indistinguibles.
- En ≤700 px se preservan Selección, Filtros y Capas como controles compactos;
  no se eliminan funciones por CSS.
- Los resultados de búsqueda se posicionan con la caja real del campo y se
  recalculan al redimensionar; no dependen de un desplazamiento fijo.
- El menú superior queda por delante de drawers compactos. `Escape` cierra
  búsqueda, menú contextual y menú Más antes de resolver la acción de pantalla.
- Mapa y `<summary>` tienen foco visible. Las comprobaciones de geometría,
  DPI real, solapamientos y menús al borde siguen siendo manuales.

## Display location

La referencia visual mantiene la rejilla 24×18 y no cambia IDs técnicos ni se
persiste. El harness estático encontró **62 celdas con más de un puesto**:

| Plano | Celdas con colisión | Puestos en esas celdas |
|---|---:|---:|
| Norte | 0 | 0 |
| Nivel 3 | 5 | 10 |
| Sur | 57 | 136 |
| I+D | 0 | 0 |
| QC | 0 | 0 |

El resultado se reporta; no se altera automáticamente la rejilla ni se cambia
`displayLocation`. La legibilidad efectiva en zoom DETAIL requiere QA manual,
con especial atención al plano Sur.

## Persistencia, escenarios y concurrencia

- Las operaciones propias usan `.lock`, revisión global, backup previo,
  temporales, `commit.pending` y recuperación antes de servir datos.
- Escenarios mantienen `baseRevision`; Apply rechaza escenarios cuya realidad
  base ya cambió. El E2E M9 verifica que Planner no modifica Reality antes de
  Apply, que Apply parcial produce backup/historial y que Undo restaura.
- Los JSON inválidos, archivos ausentes, bloqueo o `commit.pending` inválido
  fallan sin reemplazar datos por un conjunto vacío. La recuperación ambigua se
  bloquea y solicita intervención.
- Los ZIP de backup se crean como temporales, se validan por `manifest.json` y
  solo entonces se publican. Un ZIP corrupto se omite individualmente al listar
  los demás backups y queda auditado.
- La escritura JSON hace `Flush(true)` antes de publicar el temporal. Esto
  reduce riesgo de caché local; la garantía final ante corte eléctrico depende
  del sistema de archivos/SMB.
- La coordinación sigue siendo cooperativa: integraciones externas no deben
  leer/escribir JSON ignorando `.lock` o `commit.pending`. No se añadió una base
  de datos ni un protocolo nuevo de integración.

## Seguridad, privacidad y recursos

- El bridge WebView2 acepta solo mensajes con origen `https://plano.local`.
  Durante cierre deja de despachar y no responde a tareas completadas tarde.
- Los logs ya no incluyen usuario, nombre de equipo ni rutas absolutas de
  exportación/informes; conservan el nombre de fichero, acción, revisión y
  diagnóstico técnico.
- No hay CDN ni referencias remotas en los recursos activos. Todo CSS/JS/SVG
  referenciado por `index.html` existe localmente.
- `archive/ui/app.js.orig` permanece sin modificar en el workspace, pero queda
  excluido de recursos embebidos/publicados.
- WebView2 Runtime sigue siendo prerequisito de la máquina: el publish es
  self-contained para .NET, no incluye el runtime del navegador.

## Riesgos aceptados / pendientes

1. QA visual y operativa manual, incluida matriz de resolución/DPI.
2. Cierre WPF controlado mediante UI Automation: el runner actual usa timeout y
   no reproduce un cierre de usuario. Se añadieron guardas de cierre, pero la
   prueba de ciclo de vida real sigue pendiente.
3. Retención global de logs/backups: la configuración conservadora actual no
   borra backups automáticamente. Debe definirse una política operativa y ACL
   de la carpeta compartida antes de distribución amplia.
4. Control optimista `expectedRevision` para cada edición directa de Reality:
   existe lock y control de revisión para Apply de escenario, pero no se añadió
   un nuevo contrato de mutación al cliente en este hardening.
