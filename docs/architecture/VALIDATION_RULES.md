# Reglas de validación

| Rule ID | Severidad | Entidad / campos | Condición comprobada | Contexto | Evidencia | Acción sugerida | Auto-fix / revisión |
|---|---|---|---|---|---|---|---|
| duplicate-network-outlet | CRITICAL | assignment / `roseta` | Una roseta no vacía está en más de un puesto | Realidad/Escenario | `DataStore.ValidateAssignment`, `IntegrityReport` | Revisar y corregir una asignación | No / humana |
| duplicate-device | CRITICAL | assignment / `deviceId` | Un `deviceId` está en más de un puesto | Realidad/Escenario | `DataStore.ValidateAssignment` | Revisar equipo duplicado | No / humana |
| duplicate-person | WARNING | assignment / `personId` | Un `personId` está en más de un puesto | Realidad/Escenario | `DataStore.ValidateAssignment` guarda con advertencia | Revisar asignación humana | No / humana |
| assignment-missing-workspace | CRITICAL | assignment / `workstationId` | `workstationId` no existe en los puestos | Realidad/Escenario | `IntegrityReport` | Revisar referencia rota | No / humana |
| historical-occupied-without-assignment | INFO | workspace / `type` | Marca histórica `type: occupied` sin asignación vigente | Realidad/Escenario | `IntegrityReport`, `SeatStates` | Revisar dibujo heredado | No / humana |
| invalid-coordinate | CRITICAL | workspace / `x`,`y` | x/y no son numéricos normalizados 0..1 | Realidad/Escenario | `DataStore.Coordinate` | Corregir coordenada | No / humana |

## Contrato del resultado

Cada resultado contiene ID determinista, regla, severidad, entidad, mapa cuando existe, campo afectado, título, mensaje, detalle opcional, puestos relacionados y acción sugerida. El bridge devuelve además `summary` (`total`, `critical`, `warning`, `info`) y `durationMs`; no registra los valores de entidades en los logs.

## Reglas candidatas — no activadas

- `orphan-position`: `IntegrityReport` lo informa, pero el motor actual recibe el estado efectivo de mapas y asignaciones; no se activa hasta que posiciones participe de forma explícita en el contexto puro.
- Persona sin equipo: el modelo admite asignaciones parciales.
- Puesto ocupado sin persona: `SeatStates` deriva ocupado desde la persona, por lo que no puede ocurrir como estado vivo.
- Departamento/fabricante/modelo duplicado: no son identificadores únicos en el modelo actual.
