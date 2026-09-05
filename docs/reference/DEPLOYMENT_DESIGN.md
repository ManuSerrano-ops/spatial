# Diseño aprobado — paquete e instalación operativa

## Objetivo

Entregar la variante UI Figma a otros equipos sin requerir la creación manual de
carpetas compartidas ni la edición de `config.json`.

## Decisiones confirmadas

- Los datos iniciales se toman de `runtime-data/data` de esta variante aislada.
- La instalación de prueba puede estar en una carpeta local cualquiera.
- La instalación final usará una raíz compartida de red; la raíz inicial prevista
  es `G:\`.
- El producto continúa siendo un EXE autocontenido, sin servicio ni base de datos.

## Paquete generado

`deployment/New-DeploymentPackage.ps1` publica primero la aplicación y crea un
directorio nuevo con únicamente:

```text
Install-PlanoOpenSpaceIT.ps1
payload/
  PlanoOpenSpaceIT.Windows.exe
  VERSION.txt
  THIRD_PARTY_NOTICES.md
  LICENSES/LUCIDE-ISC.txt
seed-data/
  maps.json
  assignments.json
  positions.json
  events.json
  scenarios.json
  people.json
  devices.json
  locations.json
  state.json
```

`payload/VERSION.txt` registra la revisión Git corta, versión informativa, fecha UTC y SHA-256 del EXE. No contiene código fuente, pruebas, `bin`, `obj`, `publish`, backups, logs ni archivos transitorios de la ejecución real.

## Instalación

`Install-PlanoOpenSpaceIT.ps1` recibe `InstallPath` y `NetworkRoot`. Antes de modificar la raíz compartida valida el EXE, `VERSION.txt`, los avisos, la licencia y los nueve JSON de semilla. Después:

1. Comprueba que la raíz compartida permite crear y borrar un fichero temporal.
2. Crea `data`, `backups` y `logs` bajo esa raíz.
3. Si `data` no existe, copia la semilla en un directorio temporal hermano,
   valida los JSON y lo mueve a `data`.
4. Si `data` ya existe, valida los nueve JSON y no modifica ninguno.
5. Copia el EXE, `VERSION.txt` y los avisos legales a `InstallPath`.
6. Escribe junto al EXE un `config.json` que apunta a la raíz indicada.

El instalador no borra datos compartidos existentes. Si encuentra una carpeta
`data` incompleta o JSON inválido, aborta y pide corregirla explícitamente.

## Operación

Ejemplo con la raíz compartida final:

```powershell
.\Install-PlanoOpenSpaceIT.ps1 -InstallPath "C:\Plano Open Space IT" -NetworkRoot "G:\" -Launch
```

Todos los usuarios necesitan permiso de modificación en la raíz compartida para
el bloqueo, los logs y las transacciones, incluso si su perfil usa `readOnly`.

## Verificación

- Paquete creado desde publicación satisfactoria.
- Primera instalación inicializa las tres carpetas y los nueve JSON.
- Segunda instalación conserva byte a byte los JSON compartidos existentes.
- El `config.json` instalado contiene la raíz seleccionada.
- Un paquete incompleto, una semilla inválida o una carpeta `data` existente
  incompleta o inválida detienen la instalación sin sustituir datos.
- El arranque del EXE instalado se comprueba manualmente en el equipo destino;
  requiere WebView2 Runtime.
