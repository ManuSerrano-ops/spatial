# Despliegue de Plano Open Space IT

## Crear el paquete

Desde PowerShell, ejecutar:

```powershell
.\New-DeploymentPackage.ps1 -OutputPath "G:\PlanoOpenSpaceIT-instalar"
```

El generador publica la aplicación y crea un paquete con el EXE, `VERSION.txt`, avisos legales y una semilla de datos tomada de `runtime-data/data`. `VERSION.txt` identifica la revisión Git corta, la versión informativa, la fecha UTC de generación y el SHA-256 del EXE. No incluye fuentes, pruebas, `bin`, `obj`, `publish`, backups ni logs.

## Instalar en un equipo

Desde el paquete generado:

```powershell
.\Install-PlanoOpenSpaceIT.ps1 -InstallPath "C:\Plano Open Space IT" -NetworkRoot "G:\" -Launch
```

El instalador valida el EXE, `VERSION.txt`, los avisos legales y los nueve JSON de semilla antes de tocar la ruta compartida. Crea `data`, `backups` y `logs` bajo `NetworkRoot`. Si `data` no existe, copia la semilla una sola vez mediante un directorio temporal; si ya existe, valida sus JSON y nunca los sobrescribe. Una carpeta existente incompleta o con JSON inválido detiene la instalación para evitar mezclar datos.

Todos los usuarios necesitan permiso NTFS de modificación sobre la ruta compartida; de lo contrario no podrán usar el bloqueo, logs y transacciones. El EXE es autocontenido, pero el equipo necesita WebView2 Runtime.
