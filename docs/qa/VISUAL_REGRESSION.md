# Regresión visual

La regresión visual usa Chromium de Playwright contra `Resources/` servido en
loopback. No inicia WPF, WebView2 ni usa `runtime-data`. Las respuestas del
bridge provienen de `WebViewBridge.Dispatch` sobre datos temporales,
deterministas y aislados.

## Fixtures y procedencia

Las fixtures versionadas viven en `tests/visual-fixtures/`:

- `general.json`: mapa general, detalle, cursor de teclado y un escenario con
  diff largo.
- `sur-denso.json`: plano Sur concentrado, con pares de puestos que reproducen
  la separación mínima observada de aproximadamente 7,39 px.

Cada una contiene las salidas reales de `loadInitialData`, `runValidation` y
`runSpatialAnalytics`; la general incluye además el diff real de un escenario
estático. El job visual ejecuta `tools/check-visual-fixtures.py`: si el JSON
versionado deja de coincidir con el que produce `WebViewBridge`, falla antes de
capturar imágenes.

## Matriz

Los baselines cubren estos estados deliberadamente pequeños y diagnosticables:

1. mapa general en tema claro;
2. mapa general con `forced-colors` y alto contraste;
3. panel de detalle abierto;
4. cursor de colocación mediante teclado;
5. diálogo Diff con al menos 50 cambios;
6. viewport compacto de `900×460` en alto contraste;
7. plano Sur denso.

No es un producto cartesiano de temas y estados: cada imagen cubre un riesgo
concreto.

## Verificación normal

Tras restaurar dependencias visuales e instalar Chromium:

```powershell
python .\tools\check-visual-fixtures.py
python .\tools\visual-regression.py
```

El segundo comando compara contra `tests/visual-baselines/`. Ignora únicamente
variaciones de antialiasing inferiores a 8 niveles en cada canal RGBA y hasta
cinco píxeles aislados; todo cambio visible por encima de ese umbral falla. Ante una diferencia guarda los
tres elementos revisables en `tests/visual-artifacts/`:

- PNG esperado versionado;
- PNG actual;
- PNG diff.

El artefacto de CI conserva la misma estructura junto con el nombre del caso;
ese nombre codifica fixture, viewport, tema y estado.

## Árbol de accesibilidad

El mismo job visual ejecuta `tools/verify-accessibility-tree.py`. Usa CDP para
correlacionar, nodo a nodo, el selector DOM con su `backendNodeId` y el nodo AX
parcial correspondiente; no hace recuentos globales ni recorre subárboles de
modales.

Comprueba tres categorías:

1. controles expuestos al cargar (`#map-select`, `#add-seat`);
2. controles expuestos tras enfocar y activar su abridor real (el cierre de
   Historial), incluyendo el retorno de foco al abridor;
3. controles declarados pero inalcanzables (`#cluster-shape-dialog`), que deben
   permanecer ignorados sin retirar `hidden` ni abrirlos artificialmente.

## Actualizar un baseline legítimo

Nunca se actualizan baselines en CI. Cuando un cambio visual sea intencional:

```powershell
python .\tools\update-visual-baselines.py
```

El comando reconstruye las fixtures desde el bridge real y regenera todas las
imágenes. Antes de aceptar el resultado, revisar para cada caso el PNG esperado,
el actual y el diff, comprobando viewport, tema y estado. No se copian PNG a
ciegas.

La actualización de `tests/visual-baselines/` y, si procede, de
`tests/visual-fixtures/` va en **un commit propio**, separado del cambio de
interfaz que provocó la diferencia. Así el revisor ve el cambio de código y el
diff de imagen como decisiones independientes.
