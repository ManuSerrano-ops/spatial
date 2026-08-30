# Diseño aprobado — reservas y visibilidad de pines

## Reserva

La ficha incorpora `Estado` con dos opciones:

- `Automático`: sin campos es libre; con persona, dispositivo, ubicación y roseta es ocupado; con una combinación parcial es inconsistente.
- `Reservado`: escribe el campo existente `status: "reserved"` y permite conservar o dejar vacíos los cuatro campos de asignación.

Al volver a `Automático`, se conserva la información y `SeatStates.Derive` vuelve a calcular el estado. No se crea una asignación vacía al elegir automático sobre un puesto libre.

## Pines

El color sigue expresando estado y el patrón lo refuerza sin símbolos centrales:

- Libre: círculo neutro relleno con borde oscuro.
- Ocupado: círculo verde relleno.
- Reservado: círculo ámbar relleno con doble borde claro.
- Inconsistente: círculo rojo relleno con borde discontinuo claro.

El halo de selección se mantiene independiente.
