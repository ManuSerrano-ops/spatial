# Uso seguro de la copia modular

La copia modular es un arbol de codigo independiente, pero su `config.json` puede apuntar a datos de QA u operativos de otra carpeta.

La copia modular queda configurada para apuntar a:

```text
G:\Proyecto Planos\phm\phm\uifigmastyle_UX_REDESIGN_MODULAR\qa-runtime-data
```

Este destino esta aislado del proyecto original. Las pruebas destructivas siguen debiendo usar datos QA y nunca los datos operativos compartidos.

## Preparar datos aislados

1. Crear una copia independiente de `qa-runtime-data` fuera del arbol original.
2. Configurar en el `config.json` de la copia modular un `networkRoot` que apunte a esa copia.
3. Conservar `dataFolder`, `backupFolder` y `logsFolder` como rutas internas separadas.
4. Ejecutar `tools/verify.ps1` y la QA manual solo contra esa configuracion aislada.
5. Mantener `config.example.json` sin rutas reales ni secretos.

Esta separacion es de entorno y datos; no cambia el modelo `DataStore`, el bridge ni la persistencia compartida.
