# Spatial Git — Planos Windows

Visor WPF (.NET 8 + WebView2) con planos interactivos, historial auditable y escenarios aislados. La realidad confirmada y las simulaciones se mantienen separadas.

## Navegacion rapida

- `src/`: codigo C# organizado por Desktop, Application, Domain e Infrastructure.
- `Resources/`: frontend HTML, CSS, JavaScript y recursos embebidos del WebView.
- `tests/`: harnesses de regresion y proyectos de prueba.
- `docs/`: arquitectura, QA, decisiones, planes y runbooks.
- `archive/`: evidencia y material historico que no participa en compilacion.


## Estructura del proyecto

```text
src/                       Codigo C# por Desktop, Application, Domain e Infrastructure
Resources/                 Frontend y planos SVG usados por la aplicacion
  js/core/app.js           Coordinador WebView temporal
  js/features/             Funcionalidades de frontend por dominio
  js/shared/               Utilidades compartidas de frontend
  app.css                  Estilos
  index.html               Interfaz WebView2 y orden de scripts
  plano_*.svg              Planos activos: Norte, Nivel 3, Sur, I+D y QC
tests/                     Harnesses de regresion y proyectos de prueba
docs/                      Arquitectura, QA, decisiones y runbooks
archive/                   Recursos, releases y documentos historicos
tools/                     Build, verificacion y generadores de soporte
deployment/                Generador de paquete e instalador PowerShell
ParcheoCampoTemplate.xlsx  Plantilla Excel embebida de inventario fijo
```

`bin/`, `obj/` y `publish/` son salidas regenerables de compilación. El comando de publicación reconstruye `publish/` desde un directorio vacío a partir de los archivos fuente, incluidas las configuraciones de raíz.

## Datos operativos

`config.json` define `networkRoot` y `dataFolder`. Dentro de la carpeta de datos, la aplicación usa:

```text
maps.json         Planos, puestos y recursos visuales
people.json       Personas
devices.json      Dispositivos
locations.json    Ubicaciones
assignments.json  Asignaciones reales
positions.json    Posiciones reales
events.json       Historial de cambios confirmados
scenarios.json    Borradores de escenarios aislados
state.json        Revisión global monótona de las transacciones
.lock             Bloqueo exclusivo de acceso a los datos
commit.pending    Marcador de recuperación de una transacción interrumpida
```

Los backups se guardan en:

```text
<networkRoot>/<backupFolder>/spatial-git
```

Los backups nuevos son ZIP con los ocho ficheros operativos, `state.origin.json`
y un manifiesto UTC. Los directorios heredados de tres ficheros y los directorios
A3 con `files` declarados siguen pudiendo listarse y restaurarse.

El registro de auditoría seguro se escribe en
`<networkRoot>/<logsFolder>/audit-<usuario>-<equipo>-<pid>.log`. Sólo contiene
metadatos operativos (acción, resultado, usuario, equipo, revisiones, duración,
backup, ficheros y contadores), nunca el contenido de asignaciones ni payloads.
Las exportaciones registran la ruta de salida y el número de filas de Rosetas
realmente rellenadas. `logMaxFileSizeBytes` limita cada archivo; al alcanzarlo,
el anterior pasa a `.1`. Un error al escribir el log no bloquea la operación
principal.

## Trabajo concurrente y recuperación

Cada operación toma `<networkRoot>/<dataFolder>/.lock` de forma exclusiva. Dos
cambios ordinarios se serializan y cada uno relee el estado dentro de la sección
crítica; no se pierde un cambio por trabajar desde dos ventanas. Si el bloqueo
no se obtiene tras diez segundos, se muestra un error y el formulario conserva
lo introducido.

Las operaciones reales avanzan `state.json` de forma monótona y usan
`commit.pending` para recuperarse de una interrupción. Al iniciar, la aplicación
confirma, descarta o revierte la transacción pendiente según el estado de los
ficheros. Un perfil `readOnly` no puede completar esa recuperación.

Aplicar un escenario valida la revisión con la que fue creado. Si la realidad
cambió, se rechaza sin borrar el borrador; recarga, revisa el Diff y vuelve a
aplicar. Las restauraciones iniciadas por usuario sólo reponen `maps.json`,
`assignments.json` y `positions.json`: nunca rebobinan historial, escenarios,
estado ni catálogos.

## Informe de retención

`backupRetentionMode` está en `disabled` de forma predeterminada. Para generar
un informe sin abrir la interfaz, configura temporalmente `report` junto al
Ejecutable y ejecútalo así:

```powershell
.\PlanoOpenSpaceIT.Windows.exe --backup-retention-report
```

El modo toma el mismo bloqueo exclusivo de `data/.lock` que las operaciones
normales, lee los backups y `events.json`, y escribe un informe JSON en:

```text
<networkRoot>/<logsFolder>/backup-retention-<timestamp>.json
```

También deja una línea `backup.retention.report` en el log de auditoría. El
informe no borra ni convierte backups. Tras obtenerlo, vuelve a dejar
`backupRetentionMode` en `disabled`. El valor `delete` aún no ejecuta purgas:
requiere una entrega posterior y aprobación explícita del operador.

## Integridad y estado de puestos

La opción **Más → Verificar integridad** genera un informe de solo lectura sobre
rosetas duplicadas, marcas históricas de ocupación sin asignación, puestos
inexistentes y posiciones huérfanas. No corrige datos automáticamente. El estado mostrado en el plano se
deriva de la asignación vigente: `free`, `occupied`, `reserved` o
`inconsistent`. Sin campos de asignación el puesto es `free`; con persona,
dispositivo, ubicación y roseta rellenados es `occupied`; si falta alguno es
`inconsistent`; `status: reserved` es `reserved`. El campo histórico `type`
del dibujo no decide ese estado; una marca heredada `occupied` sin asignación
se muestra como `free` y se informa por separado.

Una guía visible de chinchetas explica el estado: el color identifica `free`, `occupied`, `reserved` o `inconsistent` y el relleno o contorno lo refuerza sin marcas centrales. Libre usa un anillo simple, ocupado un círculo relleno, reservado un doble anillo e inconsistente un borde discontinuo; así el estado no depende exclusivamente del color.

Persona, dispositivo y ubicación son texto libre y no se validan contra catálogos. Roseta conserva sugerencias y, junto con dispositivo, es única entre puestos; una persona repetida se guarda con advertencia. Si una roseta ya está asignada, el error identifica la roseta y el puesto, posición, persona, equipo y ubicación de la asignación existente. El informe de integridad no informa referencias de persona, dispositivo ni ubicación.

## Apariencia e iconos

El selector **Apariencia** guarda por usuario uno de cuatro temas: **Claro profesional**, **Penpot oscuro**, **Alto contraste** y **Proyector**. La preferencia se conserva junto a las preferencias de exportación en:

```text
%AppData%\PlanoOpenSpaceITUiFigma\user-preferences.json
```

No afecta a `config.json` ni a los datos compartidos. Todos los temas mantienen el lienzo y los SVG originales; los pines usan los colores de estado de cada apariencia y conservan su patrón accesible (anillo simple, relleno, doble anillo o borde discontinuo) sin marcas centrales.

Los iconos son un subconjunto de Lucide 0.468.0 incorporado como sprite SVG local. Funcionan sin conexión, usan `currentColor` y no requieren CDN, npm ni fuentes externas. La licencia ISC y el listado de iconos están en `THIRD_PARTY_NOTICES.md` y `LICENSES/LUCIDE-ISC.txt`.

## Escenarios

1. Crea un escenario desde la realidad confirmada.
2. Edita puestos, posiciones y asignaciones sin modificar los datos reales.
3. Revisa `Diff` y marca los cambios que quieras aplicar.
4. `Aplicar seleccionados` crea backup, comprueba conflictos básicos y registra un evento.
5. Un escenario puede eliminarse; la realidad confirmada no es borrable.

`Deshacer` revierte el último cambio del escenario o el último cambio real que tenga backup asociado.

## Compilar y publicar

```powershell
.	oolsuild.ps1
.	oolserify.ps1
dotnet publish PlanoOpenSpaceIT.Windows.csproj -c Release -r win-x64 --self-contained true -o publish
```

## Pruebas

La variante aislada se prueba desde `../uifigmastyle-tests`, para que sus fuentes no entren en el ensamblado WPF ni en `publish`.

```powershell
dotnet test ../uifigmastyle-tests/PlanoOpenSpaceIT.UiFigma.Tests.csproj
```

La suite incluye la regresión de revisión global, recuperación de transacciones, selección de carpeta de exportación, persistencia de temas, contrato de iconos locales y el helper de consola de bloqueo. No forma parte del ensamblado WPF ni del paquete de publicación.

## Exportar Excel

Al pulsar **Exportar**, la aplicación abre un selector nativo de carpeta. Empieza
por la última carpeta usada o, si no existe, por `Documentos` del usuario. Tras
seleccionarla se puede elegir usarla siempre sin volver a preguntar.

El plano confirmado es la única fuente de ocupación. La plantilla aporta solo
infraestructura y orden físico: se conservan sus filas y puertos sin añadir ni
mover rosetas. Los datos de persona, dispositivo, ubicación y fecha se limpian
y se sobrescriben exclusivamente desde la asignación real. Una roseta del plano
que no exista en la plantilla no crea fila y se registra; una roseta duplicada
bloquea la exportación para no elegir una asignación arbitrariamente.

La preferencia es local al usuario y se guarda en:

```text
%AppData%\PlanoOpenSpaceITUiFigma\user-preferences.json
```

con `exportFolder` y `skipExportFolderPrompt`; no se escribe en el `config.json`
compartido. Cancelar no exporta ni se considera error. Si la carpeta no es
escribible, se informa y se vuelve a solicitar otra. La instantánea de datos se
lee bajo bloqueo, pero el XLSX se genera después de soltarlo. El resultado sigue
mostrando la ruta y abre el Explorador como conveniencia.

## Despliegue en otros equipos

No copies únicamente el EXE ni crees `config.json` a mano: el programa necesita
una carpeta compartida con `data`, `backups` y `logs`. Genera un paquete nuevo
con el script incluido en el proyecto:

```powershell
.\deployment\New-DeploymentPackage.ps1 -OutputPath "G:\PlanoOpenSpaceIT-instalar"
```

En el equipo destino, desde ese paquete, ejecuta:

```powershell
.\Install-PlanoOpenSpaceIT.ps1 -InstallPath "C:\Plano Open Space IT" -NetworkRoot "G:\" -Launch
```

El instalador inicializa los nueve JSON requeridos sólo si `G:\data` todavía no
existe. Si ya existe, comprueba que los JSON son válidos y los conserva sin
sobrescribirlos. La especificación, contenido exacto del paquete y pasos de
verificación están en `docs/reference/DEPLOYMENT_DESIGN.md` y `deployment/README.md`.

## Actualizaciones

Tras actualizar la aplicación, cierra cualquier instancia de la versión anterior antes de abrir la nueva. No se admite la convivencia de ambas durante la sustitución de los recursos locales.

## Requisitos

- Windows 10/11 x64.
- WebView2 Runtime instalado (normalmente incluido con Microsoft Edge).
- Acceso a la ruta definida en `config.json` y permiso NTFS de modificación para todos los usuarios, incluidos perfiles configurados con `readOnly: true`, para poder adquirir el bloqueo de concurrencia del recurso compartido.
- No es necesario instalar el runtime de .NET para ejecutar el `.exe` publicado.


## Where to find application code

Open these folders from the project root in File Explorer:

```text
src/                  C# application, domain and infrastructure code
Resources/js/core/    temporary frontend coordinator
Resources/js/features/ frontend behavior grouped by feature
Resources/js/shared/  frontend utilities shared by features
tests/                automated harnesses
docs/                 active architecture and QA documentation
archive/              historical material, not active source
```

`bin/` and `obj/` are generated by .NET builds. They are not source code and can be ignored while browsing.
