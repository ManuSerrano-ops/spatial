# Código fuente

La aplicación se mantiene temporalmente en un único ensamblado para preservar el comportamiento durante la reorganización. La estructura física ya separa responsabilidades; la futura división en proyectos podrá hacerse sin mezclar la lógica por funcionalidad.

## Capas

- `Desktop/`: host WPF, WebView2, recursos extraídos, preferencias locales y temas.
- `Application/`: operaciones y orquestación de casos de uso.
- `Domain/`: reglas puras de clusters, puestos, validación, escenarios y analítica.
- `Infrastructure/`: persistencia JSON, logging y exportación Excel.
- `Properties/`: metadatos internos del ensamblado.

No se deben añadir clases de producto en la raíz del repositorio: deben pertenecer a una de estas capas o a una funcionalidad específica dentro de ellas.
