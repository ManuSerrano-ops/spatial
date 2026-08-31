# WCAG 2.5.8 — excepción de control equivalente

## Decisión

Los pines del plano conservan su tamaño visual y su área activa actual de 20 × 20
CSS px. La aplicación cumple el criterio WCAG 2.5.8 mediante la excepción de
**control equivalente**: la vista **Puestos** ofrece las mismas acciones de
consulta y edición sobre cada puesto en filas de tabla de ancho completo,
alcanzables con teclado.

El control equivalente es descubrible desde el plano mediante el enlace visible
**Ver puestos de este plano**. El enlace restablece los filtros de puestos,
selecciona como zona el plano activo y mueve el foco a la lista resultante.

No se amplía el objetivo de los pines. Las separaciones reales hacen que esa
medida empeore los solapamientos ya existentes.

## Medición que justifica la decisión

Fuente: `runtime-data/data/maps.json`, medida con
`tools/measure-pin-spacing.py` en Chromium a 1400 × 900 CSS px, escala de
dispositivo 1.

El zoom inicial es el ajuste automático de la aplicación a partir de
`#mapwrap` y `#plan`. El mínimo y P5 son distancias en píxeles de pantalla al
vecino más próximo de cada pin; P5 no representa todos los pares posibles.

| Plano | Pines | Zoom inicial | Área visible | Plano renderizado | Mínimo | P5 |
|---|---:|---:|---:|---:|---:|---:|
| Open Space Norte (`norte`) | 33 | 88,67 % | 1224×767 | 1085×767 | 15,64 px | 15,64 px |
| Open Space Nivel 3 (`nivel3`) | 45 | 88,67 % | 1224×767 | 1085×767 | 12,76 px | 17,52 px |
| Open Space Sur (`sur`) | 192 | 88,67 % | 1224×767 | 1085×767 | 7,39 px | 13,34 px |
| I+D (`id`) | 0 | 44,31 % | 1224×767 | 542×767 | — | — |
| Quality Control (`qc`) | 0 | 44,28 % | 1224×767 | 542×767 | — | — |

Los tres planos con puestos están por debajo de 28 px entre centros. A su zoom
inicial, un pin de 20 px mide aproximadamente 17,7 px en pantalla; ampliar su
área de interacción provocaría más solapamiento entre objetivos.

Los pines son descendientes de `#stage`, que aplica el zoom mediante
`transform: scale(...)`. Por ello, una expansión con `.pin::after` también
escalaría al alejar el plano y no garantizaría un objetivo fijo de 24 px de
pantalla.

## Equivalencia funcional mediante teclado

| Acción iniciada desde un pin | Ruta equivalente desde Puestos con teclado | Resultado |
|---|---|---|
| Abrir detalle | `Tab` hasta una fila y `Enter` | Abre el mismo inspector del puesto. |
| Editar | Abrir detalle; `Tab` hasta **Editar** y `Enter` | Sitúa el foco en el formulario de la misma ficha. |
| Mover | Abrir detalle; `Tab` hasta **Mover** y `Enter` | Activa la misma selección de destino en el plano. |
| Ver historial | Abrir detalle; `Tab` hasta **Historial** y `Enter` | Abre el mismo diálogo de historial. |

## Incumplimiento conocido — WCAG 2.1.1 Teclado (nivel A)

La colocación final de **Mover** exige hoy un clic de coordenada sobre `#plan`.
La acción se alcanza con teclado desde la ficha, pero no se puede completar sin
puntero. Esto incumple WCAG 2.1.1, nivel A; no queda mitigado por la excepción
de control equivalente de WCAG 2.5.8.

El alcance no se limita a **Mover**: **Añadir puesto** también activa un modo
que termina exclusivamente con un clic de coordenada sobre `#plan`. La
selección de destinos del planificador no entra en este incumplimiento porque
sus pines son botones nativos y se pueden activar con teclado. No se implementa
la corrección en esta tarea; debe planificarse y resolverse de forma conjunta
para ambas operaciones espaciales.

## Verificación manual

1. En un plano con puestos, navegar con `Tab` al enlace **Ver puestos de este
   plano** y activarlo con `Enter`.
2. Confirmar que la vista cambia a **Puestos**, que el filtro de zona coincide
   con el plano anterior y que el foco queda en la lista.
3. Con `Tab` y `Enter`, abrir una fila y alcanzar **Editar**, **Mover** e
   **Historial** desde la ficha.
