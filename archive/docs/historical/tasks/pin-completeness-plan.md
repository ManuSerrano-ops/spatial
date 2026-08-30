# Plan — pines de estado y selector lateral

## Causa de los óvalos

Los pines son botones. La regla global `button { min-height: var(--control-height) }` se aplica también a `.pin`; su altura efectiva pasa de 20 px a 34–40 px, mientras su ancho sigue siendo 20 px. El escalado de `#stage` es uniforme y no es la causa.

La corrección fija el origen: `.pin` anula `min-width` y `min-height`. No depende de un nivel de zoom ni altera la transformación del plano.

## Estado del pin

- Color: estado existente (`free`, `occupied`, `reserved`, `inconsistent`) con tonos propios de cada apariencia.
- Patrón: anillo simple, círculo relleno, doble anillo y borde discontinuo. Refuerza el estado para no depender únicamente del color y no añade punto, barra ni `!` en el centro.

La clasificación sigue procediendo del estado ya derivado en la instantánea cargada: sin campos es libre, los cuatro campos de asignación dan ocupado, una asignación parcial es inconsistente y `status: reserved` es reservado. No modifica datos ni el contrato del puente.

## Selector lateral

- `#tabs` se conserva y pasa a un panel izquierdo compacto.
- En escritorio los cinco planos quedan en vertical.
- Bajo 1100 px el panel se reduce a abreviaturas accesibles generadas desde el nombre del plano; no desaparece ningún plano.
- La ficha sigue en el lateral derecho y el lienzo permanece entre ambos.

## Verificación

- Pruebas de contrato para pines sin `min-height` heredado, colores y patrones de estado, guía visible de chinchetas sin ayuda de navegación superpuesta, y panel lateral.
- Suite completa y publicación limpia.
