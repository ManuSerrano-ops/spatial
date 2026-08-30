# Diseño aprobado — Excel generado desde el plano

## Objetivo

El fichero generado conserva la estructura, cabeceras, orden de columnas y estilos de `ParcheoCampoTemplate.xlsx` —sustituida por `Inventario parcheo de campo - 2026-08-11T22-41-15-935.xlsx` como base vigente—, pero el plano confirmado es la única fuente de ocupación. La plantilla aporta únicamente infraestructura e inventario.

## Inventario de plantilla observado

- `Rosetas` contiene 1.644 filas físicas de datos, 1.627 rosetas no vacías y únicas, y 17 filas completamente vacías (dos sólo contienen espacios). Las vacías no se emiten.
- El orden de `Rosetas` no es alfabético y representa el orden físico de infraestructura; es el orden maestro.
- `Repartidores` contiene puertos de infraestructura con roseta `-`; se conservan como filas de infraestructura sin roseta.
- `Parcheo de campo (Solo Lectura)` contiene filas equivalentes con roseta `-`; se conservan con la roseta vacía.
- Las filas con `#N/A` no se emiten y se contabilizan en el log como residuo de plantilla.

## Fuentes y normalización

### Infraestructura

Se leen únicamente estos campos de la plantilla:

```text
Repartidor · Stack · Switch · Puerto · Latiguillo · VLAN · Panel · Roseta
```

Cada hoja conserva sus filas de infraestructura significativas, sus estilos y su orden. Los campos de ocupación existentes en la plantilla se limpian e ignoran como entrada.

### Ocupación

Se lee sólo de las asignaciones de la realidad confirmada cuyo puesto exista en un plano. Se usan únicamente:

```text
Roseta · Persona · Dispositivo · Ubicación · updatedAt
```

No hay respaldos desde datos heredados del puesto, nombre de puesto ni nombre de plano. Persona, dispositivo y ubicación se muestran mediante el nombre legible del catálogo cuando el valor coincide con un ID; en caso contrario se conserva el texto libre.

Una fecha se escribe como número Excel sólo cuando `updatedAt` es válido; si no existe, queda vacía.

## Filas y orden

1. Se conserva la secuencia de filas válidas de infraestructura de la plantilla.
2. Las rosetas existentes solo en el plano no crean fila: se registran como no presentes en la plantilla.
3. Los puestos con la misma roseta bloquean la exportación e identifican los puestos implicados; no se añade ni se elige una fila arbitrariamente.
4. Los puestos sin roseta no generan cambios en el Excel.
5. Las filas de infraestructura sin roseta se conservan, con ocupación vacía.

Las tres hojas mantienen sus cabeceras y columnas. `Repartidores` no recibe ocupación; `Rosetas` y `Parcheo de campo (Solo Lectura)` reciben los valores de asignación en sus columnas de ocupación correspondientes.

## Resultado y auditoría

`XlsxExportResult`, la respuesta del puente y el log `export.excel` incluirán como mínimo:

```text
rosetasTotal
rosetasFromTemplate
rosetasFromPlan
rosetasInBoth
rosetasOnlyFromPlan
renderedRows
duplicateRosetas
templateRowsWithoutRoseta
templateRowsSkippedInvalidRoseta
```

El log de duplicados incluye los IDs de puestos causantes, no datos personales.

## Interfaz

La respuesta de `exportExcel` muestra la ruta completa del XLSX. Si no puede abrirse el Explorador, muestra que el archivo se creó e incluye el motivo, sin convertir una exportación exitosa en fallo.

## Verificación

- Roseta en ambas fuentes.
- Roseta sólo en plantilla.
- Roseta sólo en plano, añadida al final.
- Ocupación ausente: celdas vacías.
- Roseta duplicada: una fila por puesto y log sin datos personales.
- Filas vacías, `-` de infraestructura y `#N/A` de plantilla.
- Fecha numérica de Excel.
- Generación fuera del bloqueo.
- Comprobación manual del XLSX real: filas totales frente a 1.644 y rosetas nuevas desde el plano.
