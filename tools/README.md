# Herramientas de desarrollo

- `build.ps1`: restaura y compila la aplicacion WPF.
- `verify.ps1`: verifica sintaxis JavaScript, ejecuta todos los harnesses, valida los mapas claros y compila el proyecto.
- `build-light-maps.py` y `generate-light-map-assets.js`: generan y validan activos claros de planos. No editan los SVG canonicos directamente.

Desde PowerShell, en la raiz del proyecto:

```powershell
.\tools\build.ps1
.\tools\verify.ps1
```
