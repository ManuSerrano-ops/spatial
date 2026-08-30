# Lecciones

Patrones de error observados en este proyecto y la regla que los evita. Se relee al empezar cada sesión y se amplía tras cada corrección.

**Mantenimiento:** consolidar una lección existente antes de añadir otra. El fichero debe poder leerse entero al empezar una sesión; no duplicar el mismo caso con redacciones distintas.

---

## L1 · Reportar solo las pruebas nuevas en lugar de la suite completa

**Observado:** tras A10/A11 y tras las dos correcciones P0 de restauración, se reportó el recuento de las pruebas añadidas (2 y 1) en lugar del total. En ambos casos el cambio tocaba rutas compartidas —`CommitReal`, `RestoreBackup`, el undo real— por las que pasan muchas pruebas anteriores.

**Regla:** ninguna tarea se cierra sin la salida de la suite completa, sin filtros ni categorías excluidas. El recuento de pruebas nuevas no sustituye al total.

---

## L2 · Responder a la corrección en lugar de a la petición

**Observado:** tres veces seguidas, ante un mensaje que señalaba un fallo *y además* pedía algo concreto —el recuento completo, el contenido literal del log—, se respondió con un resumen de lo ya corregido y la petición quedó sin atender.

**Regla:** cuando el mensaje pida algo además de corregir, la corrección no cierra la petición. Responder a ambas cosas, o decir explícitamente cuál queda pendiente.

---

## L3 · Una instrucción correcta en un contexto no vale en otro

**Observado:** «el backup debe cubrir el mismo conjunto de ficheros que la transacción» era correcto para la reversión automática de un commit fallido, y equivocado para la restauración iniciada por el usuario. Se aplicó a ambas, y produjo dos P0: `RestoreBackup` rebobinaba `events.json`, destruyendo el historial posterior, y después `scenarios.json`, destruyendo el trabajo de planificación. La suite seguía en verde.

**Regla:** al ampliar el alcance de un mecanismo, enumerar **todas** las operaciones que lo usan y comprobar si la razón original vale para cada una. Ampliar lo que un backup contiene no significa ampliar lo que una restauración repone.

**Regla derivada, específica del producto:** una restauración iniciada por el usuario repone el estado de la realidad. No revierte el registro de lo ocurrido ni el trabajo de planificación en curso.

---

## L4 · La suite prueba lo que alguien pensó en probar

**Observado:** A2/A3 se cerraron con 55 pruebas en verde y ninguna cubría los dos casos anteriores. Los defectos se detectaron leyendo el diseño, no ejecutando pruebas. Al añadir B2, la prueba dirigida detectó además una lectura de `assignments` en el nivel equivocado del estado compuesto antes de llegar a la suite completa.

**Regla:** las pruebas cubren regresiones, no huecos de concepción. Ante un cambio de mecanismo, razonar sobre los casos antes de confiar en el verde. Para documentos JSON anidados, una prueba dirigida debe atravesar la misma forma que usará la operación real.

---

## L5 · Un cambio de backend puede crear un defecto en la interfaz

**Observado:** A2 convirtió un guardado instantáneo en una operación que puede esperar hasta diez segundos y fallar. La interfaz no mostraba estado de espera ni deshabilitaba los botones, de modo que un doble clic podía producir dos commits, dos escenarios con el mismo nombre o dos puestos en la misma celda.

**Regla:** cuando un cambio introduzca una latencia o un modo de fallo nuevos, revisar qué ve el usuario durante ese tiempo y qué puede hacer mientras tanto.

**Regla derivada de interfaz:** un indicador persistente debe responder a una sola pregunta. El estado operativo temporal tiene prioridad durante una solicitud; en reposo, el indicador del visor muestra sólo el zoom y no mezcla contexto de escenarios ni contadores ajenos.

---

## L6 · Verificar contra el artefacto real, no contra el resumen

**Observado:** el logging se dio por completo hasta que se leyó un fichero de log real: faltaban el ciclo de vida, `createScenario`, `exportExcel` y la acción causante de cada transacción. También apareció un desfase horario entre el log (UTC) y los identificadores de backup (hora local) que habría desplazado las ventanas de retención de B6.

**Regla:** para funciones cuyo valor está en su salida —logs, exportaciones, informes—, la verificación es leer la salida real, no describir lo que el código registra.

---

## L7 · Un valor por defecto puede aparentar trazabilidad sin darla

**Observado:** el log registraba `applicationVersion: "1.0.0.0"`, el valor por defecto de .NET, idéntico en todas las publicaciones. El campo parecía dar trazabilidad de compilación y no daba ninguna.

**Regla:** un campo de diagnóstico cuyo valor no cambia entre compilaciones es ruido. Usar el MVID u otro identificador que varíe por sí solo, sin depender de que alguien recuerde actualizarlo.

---

## L8 · La delegación puede aplicar cambios sin confirmarlos

**Observado:** una delegación no devolvió confirmación verificable de las modificaciones que había aplicado; hubo que inspeccionar el código para saber si estaban.

**Regla:** no delegar implementación ni edición de ficheros en este proyecto. La ayuda auxiliar se limita a lectura, y su resultado se verifica antes de usarlo.

---

## L10 · Una copia de código no debe arrastrar resultados de prueba

**Observado:** la primera copia final incluyó `TestResults/` y archivos históricos del árbol superior, elevando su tamaño a cientos de MB aunque la aplicación fuente sólo necesitaba recursos, documentos y pruebas fuente.

**Regla:** una copia de cierre se hace desde los subárboles de producto y pruebas fuente, no desde un directorio padre con material histórico. Excluir siempre `bin/`, `obj/`, `publish/`, `TestResults/` y cualquier otro resultado regenerable.

---

## L9 · La ruta de llegada es parte del mecanismo

**Observado:** al añadir ZIP a los backups, el lector sabía abrirlo, pero `GetBackups` agrupaba primero directorios y después ZIP. Un backup existente podía quedar mal ordenado en la interfaz, induciendo a restaurar una copia equivocada. La recuperación de `commit.pending` también seguía comprobando rutas de directorio aunque la transacción ya creaba ZIP.

**Regla:** cuando se añade un formato o mecanismo nuevo, enumerar todos sus recorridos: descubrimiento, listado y orden global entre formatos, restauración y recuperación automática. No basta con probar la lectura directa.

---

## L11 · Un elemento visual puede conservar gestos nativos

**Observado:** el plano ya tenía `draggable="false"` y reglas CSS para impedir su arrastre, pero el navegador seguía pudiendo iniciar el gesto nativo de arrastrar una imagen al desplazar el plano. Además, el controlador de desplazamiento sólo aceptaba como origen la imagen: las zonas vacías del visor quedaban fuera de la interacción y sin cancelar el gesto.

**Regla:** cuando un elemento multimedia participa en una interacción propia, cancelar explícitamente el evento nativo (`dragstart`) y el gesto por puntero que inicia la interacción (`pointerdown`) en el contenedor completo del visor. Excluir sólo los controles que tienen su propio arrastre, como los marcadores de puestos. Probar ambas protecciones en el recurso embebido.

---

## L12 · La compilación no valida la estructura interactiva del HTML

**Observado:** `#apply` conservaba su ID y el manejador de `app.js`, pero su etiqueta estaba fuera del botón por un cierre prematuro. La aplicación compilaba y el control quedaba vacío en la interfaz.

**Regla:** al modificar una interfaz basada en selectores DOM, comprobar el HTML renderizable además de compilar: el elemento debe conservar su anclaje, contenido, semántica y manejador. Una prueba de recurso embebido protege este contrato.

---

## L13 · Accesible no significa visualmente heterogéneo

**Observado:** para no depender sólo del color, los marcadores se diseñaron inicialmente con formas no relacionadas —cuadrado, rombo y triángulo— que parecían pictogramas distintos en vez de partes de un mismo sistema.

**Regla:** la redundancia accesible se integra en una gramática visual única: conservar la forma base del control y diferenciar estados con relleno, borde o símbolo interior. Validar la percepción en la interfaz real, no sólo la presencia de selectores CSS.

---

## L14 · Un recurso visual local necesita el mismo contrato que una acción

**Observado:** un sprite SVG con una referencia rota no detiene el puente ni la compilación: deja un hueco silencioso en el control. Los temas pueden ocultar además un icono con un color fijo que parecía correcto en otro fondo.

**Regla:** para recursos visuales embebidos, probar que cada `use` y cada icono dinámico apuntan a un símbolo existente, que el trazo usa `currentColor` y que los temas definen todos los tokens necesarios. Incluir licencia, versión y lista exacta del subconjunto incorporado.

---

## L15 · Las reglas globales de controles pueden deformar controles especializados

**Observado:** el `min-height` global de los botones se aplicó a los pines del plano. Aunque `.pin` declaraba `height: 20px`, el mínimo global elevó solo su altura a 34–40 px y los convirtió en óvalos.

**Regla:** cuando un componente reutiliza un elemento nativo con geometría propia, revisar también los mínimos, flex y tamaño heredados, no solo `width` y `height`. Probar la forma efectiva bajo todos los temas y escalas.

---

## L16 · Una política de datos debe actualizar todos sus consumidores

**Observado:** al convertir persona, dispositivo y ubicación en texto libre, el formulario dejó de usar catálogos pero el estado derivado y el informe de integridad aún conservaban dependencias de esos catálogos. Habrían seguido mostrando contradicciones que la nueva política ya no define como errores.

**Regla:** al cambiar la autoridad o validación de un dato, localizar y actualizar de forma conjunta la escritura, lectura, estado derivado, informes, leyendas, documentación y pruebas. Un consumidor residual convierte una regla retirada en un comportamiento oculto.

---

## L17 · Una validación bloqueante debe identificar el dato que bloquea

**Observado:** el rechazo de roseta duplicada decía que existía otro puesto, pero no indicaba cuál ni qué asignación había que revisar. El usuario tenía que buscar manualmente entre los planos antes de poder corregir la situación.

**Regla:** cuando una regla de unicidad rechace un cambio, el error debe incluir el valor en conflicto y el contexto operativo disponible del registro existente. La búsqueda de ese contexto se realiza dentro de la misma instantánea protegida que toma la decisión.

---

## L18 · Cada canal visual necesita un único significado operativo

**Observado:** los pines combinaban la geometría de estado con un color de completitud. Aunque era accesible, no respondía a la pregunta principal de la vista y dejó sin uso los colores de estado definidos por las apariencias.

**Regla:** antes de añadir una dimensión visual, decidir qué pregunta operativa responde. Si el color expresa el estado, debe usar los tokens de cada tema y no mezclar completitud; el patrón de relleno o contorno lo refuerza sin añadir marcas centrales innecesarias. Conservar una guía superpuesta cuando explique una codificación específica de la aplicación; retirar en cambio instrucciones genéricas de navegación que no necesitan ocupar el lienzo.

---

## L19 · El estado visible debe derivarse de las entradas que el usuario reconoce

**Observado:** los pines verdes dependían de que hubiera persona, aunque faltaran dispositivo, ubicación o roseta. El usuario interpretaba correctamente que una asignación completa debía ser la ocupada, pero la función de dominio seguía usando una regla más antigua.

**Regla:** cuando un estado de pantalla representa completitud operativa, derivarlo en un único sitio de los mismos campos que edita el usuario. Revisar los informes que filtran por ese estado para que una asignación parcial no desaparezca de los controles de integridad.

---

## L22 · Un estado existente necesita una transición de usuario

**Observado:** el dominio reconocía `status: reserved`, pero la ficha no permitía crearlo ni retirarlo. Un estado visible sin una acción equivalente convierte una capacidad de datos en una sorpresa para el usuario.

**Regla:** todo estado que se muestre y pueda persistir necesita una transición explícita en la interfaz, con una semántica definida al volver al modo automático.

---

## L21 · La plantilla de exportación no debe seguir siendo fuente de negocio

**Observado:** el Excel previo solo rellenaba filas ya presentes y conservaba ocupación heredada de la plantilla, por lo que ocultaba rosetas creadas en el plano y podía exportar información que el plano ya no confirmaba.

**Regla:** cuando una plantilla aporta formato e inventario pero el sistema operativo es la fuente de verdad, separar infraestructura de ocupación. Construir la unión explícita, preservar el orden físico útil y hacer visibles los duplicados en vez de sobrescribirlos.

---

## L20 · Una acción confirmada debe limpiar toda su interfaz asociada

**Observado:** borrar un puesto quitaba el dato y reiniciaba la selección, pero dejaba visible la ficha con los valores ya inexistentes. Cancelar el modo de alta también dejaba el menú contextual que lo había iniciado.

**Regla:** tras una acción exitosa, limpiar explícitamente la selección, paneles y menús que representen el elemento o modo ya terminado. Hacerlo después de la confirmación para que un error preserve la ficha y los valores del usuario.

---

## L23 · Un paquete ejecutable no es un despliegue operativo

**Observado:** copiar el EXE y un `config.json` que señalaba a `G:\data` dejaba la aplicación sin poder iniciar porque la raíz compartida no incluía los JSON operativos ni las carpetas de logs y backups.

**Regla:** un instalador que configure una ruta compartida debe validar primero su propio paquete, comprobar permisos, inicializar el conjunto mínimo de datos de forma atómica y conservar íntegros los datos existentes. Nunca pedir al usuario que cree manualmente la estructura que el programa necesita para arrancar.

---

## L24 · Un encabezado flexible necesita poder encogerse

**Observado:** al sustituir una marca compacta por el logotipo corporativo, la barra superior acumuló anchos mínimos de marca y controles en resoluciones intermedias. El documento se ensanchaba y dejaba una zona blanca visible al desplazarse horizontalmente.

**Regla:** en una cabecera `flex`, cada grupo que pueda competir por espacio debe declarar `min-width: 0` y una política explícita de reducción: ocultar la marca antes de desbordar, limitar selectores y truncar etiquetas. El lienzo puede tener un tamaño interno mínimo para pan y zoom, pero nunca debe expandir el documento exterior.

---

## L25 · Una capacidad esencial oculta equivale a una capacidad ausente

**Observado:** los clusters ya podían renombrarse y ajustar libremente tamaño y posición, pero esas acciones estaban repartidas entre una ficha y un menú contextual. En el primer recorrido de usuario parecían inexistentes.

**Regla:** una acción necesaria para operar un elemento visible debe tener una entrada directa y etiquetada en su propia superficie o en su flujo primario. Los menús contextuales pueden complementar, nunca ser el único acceso. El contrato de interfaz debe comprobar tanto la acción como el aislamiento de los gestos del lienzo.

---

## L26 · La ruta de inicio WPF debe coincidir con el recurso compilado

**Observado:** `App.xaml` buscaba `MainWindow.xaml` en la raíz, aunque la página se compilaba bajo `src/Desktop/Host`. La compilación y las pruebas estáticas pasaban, pero el EXE terminaba al iniciar con «No se encuentra el recurso `mainwindow.xaml`».

**Regla:** cuando `ApplicationDefinition` y una página WPF viven fuera de la raíz, `StartupUri` debe usar la ruta del recurso compilado. Cubrir el URI y la inclusión de la página con un harness, y verificar al menos un arranque de una publicación real.

---

## L27 · Las rutas de recursos embebidos se verifican tras extraerlas

**Observado:** la ruta lógica de los JavaScript añadía `js/` a un `%(RecursiveDir)` que ya lo contenía. La publicación extraía `js/js/...`, mientras el HTML solicitaba `js/...`; la ventana se abría, pero ningún script enviaba `loadInitialData` y el visor quedaba en «Conectando…» sin datos.

**Regla:** para cada recurso embebido con ruta relativa, comprobar el contrato completo: nombre lógico, ruta extraída y referencia HTML. La compilación y la existencia del `index.html` no demuestran que el navegador pueda cargar sus scripts.

---

## L28 · `title` no sustituye una alternativa accesible

**Observado:** el selector de contexto conserva el nombre completo de un escenario truncado en `title`, útil al usar puntero, pero el tooltip nativo no aparece con teclado ni en táctil.

**Regla:** no tratar `title` como la única fuente de información necesaria. La tarea 3.4 debe clasificar y sustituir los `title` que no tengan una alternativa visible o accesible equivalente.

