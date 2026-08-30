# Plan — Controles de clusters y mapa

## Objetivo

Hacer visibles las acciones indispensables de los clusters, separar las capas visuales de los filtros de datos y simplificar el selector claro/oscuro del plano.

## Alcance y decisiones

- Cada tarjeta de cluster expondrá acciones directas para **Renombrar** y **Ajustar tarjeta**. Ajustar abre el editor existente de tamaño libre, posición y contenido; su configuración continúa siendo local al usuario y no cambia los datos compartidos del cluster.
- El control **Capas** se traslada como panel flotante contextual en la esquina superior derecha del lienzo. Conserva los mismos IDs y estado para no cambiar el contrato del frontend.
- El selector de apariencia del plano mostrará únicamente las alternativas `Oscuro` y `Claro`; se elimina la etiqueta estática `Plano`.

## Entrega guiada por pruebas

1. **RED** — Añadir un harness que exija las dos acciones accesibles de la tarjeta, el panel Capas dentro del lienzo, la protección contra pan/zoom accidental y la ausencia de la etiqueta redundante.
2. **GREEN** — Renderizar las acciones directas, reutilizar los flujos existentes de renombrado y ajuste libre, y reubicar el control sin modificar los IDs de las capas.
3. **GREEN** — Ajustar CSS para que el panel sea legible, no intercepte el plano y siga siendo usable en tamaños compactos.
4. **Verificación** — Ejecutar el harness nuevo, la suite completa `tools/verify.ps1`, compilación y revisión manual del flujo en `qa-runtime-data`.

## Criterios de aceptación

- Un usuario puede abrir Renombrar y Ajustar tarjeta desde la propia tarjeta, sin menú contextual.
- Ajustar conserva el redimensionado libre, el desplazamiento, Guardar y Cancelar existentes.
- Capas permanece disponible en vista Mapa, junto al lienzo, y sus controles no activan pan, selección rectangular, zoom ni menú contextual.
- El selector muestra sólo `Oscuro` y `Claro`.
