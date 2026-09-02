# Pruebas y harnesses

Los harnesses JavaScript se mantienen temporalmente en la raiz de `tests/` para conservar sus rutas CommonJS y permitir que `tools/verify.ps1` los descubra de forma determinista.

## Indice logico

```text
tests/
├─ *cluster*                    clusters, cards, Area Focus y drag
├─ *selection* / *rectangle*    seleccion, Bulk y panel de seleccion
├─ *map* / *viewport*           plano, densidad, contexto y viewport
├─ *planner*                    Movement Planner
├─ *scenario*                   escenarios y Compare
├─ *analytics* / *dashboard*    analitica y Dashboard
├─ *validation*                 Validation Engine y frontend
├─ *workspace*                  estado y presentacion de puestos
└─ *feature-harness.js          factories extraidas de core/app.js
```

Las pruebas C# se agrupan por dependencia de plataforma:

- `PlanoOpenSpaceIT.Domain.Tests/`: `net8.0`, sin WPF; ejecutable también en Ubuntu.
- `PlanoOpenSpaceIT.Desktop.Tests/`: `net8.0-windows` con WPF; cubre WebView2, recursos embebidos, bridge y ciclo de vida nativo.

`tools/verify.ps1` ejecuta ambos proyectos con `dotnet test`. El contador manual `tools/count-csharp-assertions.py` sirve únicamente para migraciones de aserciones y no forma parte de la verificación continua.

No mover un harness solo por estetica: antes hay que actualizar sus `require(...)`, sus rutas a `Resources/` y el descubrimiento de `tools/verify.ps1`.
