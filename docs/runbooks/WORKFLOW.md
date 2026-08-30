# Normas de trabajo — Spatial Git

Documento permanente. Se relee al empezar cada sesión.

---

## 1. Planificar antes de construir

- Cualquier tarea no trivial —tres pasos o más, o que implique una decisión de diseño— empieza con un plan escrito, no con código.
- Las decisiones que afectan a la persistencia, al bloqueo, al formato de los datos o al despliegue requieren **documento de diseño aprobado** antes de tocar nada.
- Si algo se tuerce a mitad, **detente y replantea**. No sigas empujando.
- Escribe la especificación completa desde el principio; la ambigüedad se paga después.

---

## 2. Sin subagentes para el trabajo de este proyecto

Los subagentes no comparten el contexto acumulado de decisiones: la opción A de permisos, la frontera entre datos e infraestructura, la separación entre conjunto transaccional y de restauración, y por qué se descartó cada alternativa. Nada de eso está enteramente en el código.

Delegar trabajo sobre `DataStore` con contexto parcial es la forma más rápida de reintroducir un defecto ya cerrado. Además, ya ha ocurrido: una delegación aplicó cambios sin devolver confirmación verificable.

- **No delegues implementación, ni edición de ficheros, ni decisiones.**
- Si usas ayuda auxiliar, que sea **solo lectura**: buscar, leer, localizar. El resultado lo verificas tú antes de usarlo.

---

## 3. Registrar las lecciones

Después de **cualquier** corrección, actualiza `../../tasks/lessons.md` con el patrón del error y la regla que lo evita. Relee ese fichero al empezar cada sesión.

No es burocracia: los errores de este proyecto se han repetido, y una regla escrita cuesta menos que la tercera repetición.

---

## 4. Verificar antes de dar por hecho

- **Ninguna tarea se cierra sin la salida de la suite completa, sin filtros ni categorías excluidas.** El recuento de las pruebas nuevas no sustituye al total.
- Nada se marca como terminado sin demostrar que funciona. Ejecuta, lee los logs, enseña la evidencia.
- Cuando el usuario pida algo **además** de corregir un fallo, la corrección no cierra la petición. Responde a ambas cosas, o di explícitamente cuál queda pendiente.
- Lo que no se puede verificar automáticamente —interfaz, arranque, concurrencia real— se prueba a mano y se dice claramente que se hizo así.

---

## 5. Elegancia dentro del alcance, nunca fuera

Busca la solución simple y limpia **para la tarea que tienes entre manos**.

Pero: **sin reescrituras, y sin refactorizar lo que la tarea no toca.** Estamos en estabilización sobre datos reales de producción; un refactor de paso convierte un cambio verificable en uno que no lo es.

Si ves una mejora fuera del alcance, la anotas. No la haces.

---

## 6. Los defectos nuevos se anotan, no se arreglan

**Al encontrar un defecto que no es el de la tarea en curso: anótalo con su severidad y sigue.**

Esta regla es deliberada y va en contra del instinto. Existe porque cada cambio debe ser una tarea con su prueba: cuando algo se pone rojo, hay que poder saber qué lo causó. Mezclar arreglos oportunistas destruye esa propiedad.

Excepción única: un defecto que impida continuar con la tarea actual. Entonces se para, se comunica y se decide.

---

## 7. Decisiones de producto

Si una decisión es de negocio y no técnica —permisos, semántica de una opción, qué se conserva y qué se borra—, **se pregunta**. Con las alternativas y sus consecuencias, y se espera respuesta.

---

## 8. Restricciones permanentes del proyecto

- Nada de SQLite ni ninguna base de datos. El almacenamiento son ficheros JSON sobre el recurso compartido.
- Sin servidor ni servicio nuevo. Un EXE autocontenido con `config.json` al lado.
- Compatibilidad hacia atrás: un `runtime-data` existente se abre sin migración manual.
- El sobre del puente WebView2 no cambia. Añadir acciones o campos opcionales sí está permitido.
- Publicación sin pasos manuales. **Nunca se cierra un proceso en ejecución**: si el fichero está bloqueado, falla y avisa.
- Ni un solo test contra `runtime-data` real. Todo contra fixtures temporales.
- Las copias de seguridad futuras incluyen `runtime-data` y código/documentación, pero excluyen salidas regenerables: `bin/`, `obj/`, `publish/`, `TestResults/` y cualquier salida de compilación o prueba. La copia se crea fuera del árbol de trabajo.

---

## 9. Gestión de tareas

- El plan vive en `../../tasks/todo.md`, con elementos verificables.
- Se marca el progreso a medida que avanza.
- Cada entrega lleva un resumen de alto nivel de lo que cambió.
- `../../tasks/lessons.md` se actualiza tras cada corrección.

---

## Principios

**Simplicidad primero.** Cada cambio, lo más simple posible; el mínimo código afectado.

**Causa raíz.** Nada de parches temporales que dejen el problema debajo.

**Impacto mínimo.** Tocar solo lo necesario.

**Honestidad.** Si algo no se ha verificado, se dice. Si algo no se sabe, se pregunta. Un resumen optimista de trabajo no comprobado cuesta más que un «esto está sin probar».
