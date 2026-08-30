# Plan — temas e iconos locales

## Alcance aprobado

- Reajustar el lenguaje visual hacia una herramienta tipo Penpot: lienzo dominante, inspector lateral compacto y controles sobrios.
- Añadir cuatro temas predefinidos: claro profesional, Penpot oscuro, alto contraste y proyector.
- Integrar iconos Lucide locales, sin red ni dependencias en ejecución.

## Restricciones

- El lienzo mantiene `#313131`; los SVG y la cuadrícula no se modifican.
- Los pines siguen siendo circulares y se distinguen por vacío, punto, barra y `!`; el color es secundario.
- No se usan morados ni púrpuras.
- IDs, clases y atributos ya documentados se conservan.

## Arquitectura

1. Extender la preferencia local existente en AppData con una clave `theme`, compatible con el JSON de preferencias ya creado.
2. Exponer la lectura y escritura de tema mediante dos acciones locales del host WebView2, fuera del almacén de datos compartido y del registro de auditoría.
3. Aplicar el tema con `data-theme` en el elemento raíz y cuatro conjuntos completos de variables CSS.
4. Incluir un sprite SVG de los iconos Lucide usados dentro de `index.html`. Los símbolos emplean `currentColor`, tamaño CSS de 16/20/24 px y trazo uniforme.
5. Mantener texto en acciones no universales. Las acciones universales sin texto conservan `aria-label` y `title`.

## Verificación

- Pruebas de persistencia y normalización de la preferencia de tema.
- Contrato de recurso embebido: todos los iconos usados referencian símbolos presentes; no hay colores fijos en el sprite; el contrato DOM existente sigue disponible.
- Suite completa contra `uifigmastyle` y publicación limpia.
- Checklist manual único para temas, iconos y acciones existentes.
