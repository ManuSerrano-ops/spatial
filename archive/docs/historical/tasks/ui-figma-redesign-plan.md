# Plan — rediseño visual UI Figma

## Objetivo

Modernizar exclusivamente la presentación de la aplicación de trabajo sin cambiar el contrato del puente, las acciones ni el comportamiento operativo.

## Fase 0 · Aislamiento

- Mantener `runtime-data` dentro de `uifigmastyle` y dirigir tanto desarrollo como la publicación actual a esa ruta.
- Usar `PlanoOpenSpaceITUiFigma` para recursos extraídos, perfil WebView2 y preferencias de usuario.

## Fase 1 · Contrato y propuesta

- Inventariar todos los selectores DOM que consume `Resources/app.js`: IDs, clases, atributos `data-*` y eventos.
- Proponer disposición y sistema visual antes de modificar el frontend.
- Esperar aprobación del usuario.

## Fase 2 · Implementación visual

- Rediseñar primero `Resources/app.css`, con variables de color, espaciado, radios, bordes y sombras.
- Modificar `Resources/index.html` sólo si la disposición aprobada lo exige, sin alterar los anclajes inventariados.
- No modificar `Resources/app.js` salvo autorización expresa.

## Fase 3 · Verificación

- Ejecutar la suite completa sin filtros.
- Publicar sin cerrar procesos.
- Entregar una lista de comprobación manual que cubra todas las acciones y estados indicados por el usuario.
