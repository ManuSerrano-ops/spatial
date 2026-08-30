# Checklist manual de caza de bugs

Usar contra `uifigmastyle/publish/PlanoOpenSpaceIT.Windows.exe`.

> Esta es una pasada rápida de regresión para encontrar fallos de interfaz y
> estado. Trabaja solo sobre `uifigmastyle/runtime-data`: crea datos de prueba y
> elimínalos al terminar.

## 1. Arranque y navegación

- [ ] La aplicación abre sin panel de error.
- [ ] Cambia por los cinco planos: el plano, pines y selección se actualizan.
- [ ] El panel izquierdo distingue claramente el plano activo.
- [ ] Reduce la ventana por debajo de 1100 px: los cinco planos siguen accesibles mediante abreviaturas.
- [ ] El indicador del visor muestra únicamente el porcentaje de zoom en reposo.
- [ ] No aparece texto de rueda, arrastre ni referencias de celda sobre el plano.
- [ ] La guía visible de chinchetas muestra Libre, Ocupado, Reservado e Inconsistente.

## 2. Plano, zoom y desplazamiento

- [ ] La rueda acerca y aleja alrededor del puntero; el porcentaje de zoom cambia.
- [ ] Arrastrar sobre el dibujo desplaza el plano sin iniciar el arrastre nativo de la imagen.
- [ ] Arrastrar sobre una zona vacía también desplaza el plano.
- [ ] El menú contextual aparece con botón derecho y se cierra al pulsar fuera.
- [ ] Copiar referencia de celda devuelve una referencia; Restablecer vista vuelve a 100 %.
- [ ] Todos los pines siguen siendo círculos a varios niveles de zoom y en varios planos.

## 3. Estados de chinchetas

Para un puesto de prueba, guarda y recarga entre cada caso.

- [ ] Sin persona, dispositivo, ubicación ni roseta: se muestra **Libre** (anillo simple).
- [ ] Solo algunos de los cuatro campos rellenos: se muestra **Inconsistente** (borde discontinuo).
- [ ] Persona, dispositivo, ubicación y roseta rellenos: se muestra **Ocupado** (círculo relleno, verde según el tema).
- [ ] Si existe un puesto reservado en los datos, se muestra **Reservado** (doble anillo).
- [ ] Cambia entre los cuatro temas: los pines conservan el significado y se leen sobre el plano.
- [ ] Selecciona un pin: aparece el anillo de selección sin alterar su color o patrón de estado.

## 4. Ficha y guardado

- [ ] Seleccionar un puesto abre la ficha derecha con los valores correctos.
- [ ] Cerrar la ficha o pulsar sobre el plano la oculta.
- [ ] Persona, dispositivo y ubicación aceptan texto libre; solo roseta ofrece sugerencias.
- [ ] Guardar muestra «Guardando asignación…» y después una confirmación; al recargar, los valores persisten.
- [ ] Vaciar roseta o notas, guardar y recargar mantiene el campo vacío.
- [ ] Roseta duplicada: bloquea el guardado, mantiene lo escrito y muestra roseta, puesto, posición, persona, equipo y ubicación ya asignados.
- [ ] Dispositivo duplicado: bloquea el guardado y conserva lo escrito.
- [ ] Persona repetida: permite guardar y muestra advertencia.

## 5. Crear, mover y borrar puestos

- [ ] Activa **Añadir puesto**: el botón cambia a «Haz clic en el plano».
- [ ] Vuelve a pulsar **Añadir puesto**: cancela el modo y no queda abierto ningún menú contextual.
- [ ] Crea un puesto de prueba: aparece en la posición pulsada y se abre su ficha tras guardar.
- [ ] Mueve el puesto; recarga y confirma que su posición persiste.
- [ ] Elimina el puesto de prueba: desaparece del plano y la ficha derecha se cierra.
- [ ] Si cancelas la confirmación de borrado, el puesto y su ficha permanecen intactos.

## 6. Operaciones lentas y fallos

- [ ] Al guardar, el control de origen muestra ocupado y los demás controles quedan deshabilitados.
- [ ] Al terminar correctamente, todos los controles se reactivan.
- [ ] Si una operación falla, aparece un error persistente y el formulario conserva sus valores.
- [ ] Tras dos segundos de espera, el mensaje indica que está esperando respuesta; no parece que la aplicación se haya bloqueado.

## 7. Escenarios y utilidades

- [ ] Crea un escenario, cambia una asignación y mueve un puesto: la realidad no cambia antes de aplicar.
- [ ] Diff muestra cambios seleccionables; Aplicar seleccionados conserva su etiqueta y funciona.
- [ ] Borra el escenario de prueba sin alterar la realidad.
- [ ] Historial, Backups, Verificar integridad y Exportar abren y terminan sin romper la interfaz.
- [ ] En ventana estrecha, **Más** contiene Diff, Historial, Backups, Exportar y Verificar integridad.

## 8. Cierre y reporte

- [ ] Elimina los puestos, asignaciones y escenarios de prueba creados durante la pasada.
- [ ] Cierra la aplicación normalmente.

Para cada fallo, anota:

```text
Fecha y hora:
Versión/EXE probado:
Tema y tamaño de ventana:
Plano y puesto afectados:
Pasos exactos:
Resultado esperado:
Resultado observado:
Captura de pantalla:
```
