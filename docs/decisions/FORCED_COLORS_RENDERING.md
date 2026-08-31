# Renderizado en `forced-colors`

## Restricción observada

Chromium suprime `box-shadow` con `forced-colors: active`, incluso cuando la
regla declara `forced-color-adjust: none` y `!important`. Una sombra no es un
canal fiable para un indicador de estado o de foco en este modo.

## Decisión

Cuando un componente necesite conservar una señal visual semántica en alto
contraste, se usarán pseudo-elementos con `border` y colores de sistema (por
ejemplo, `CanvasText`) en lugar de sombras. Los colores generales deben seguir
dejándose al sistema salvo donde el color transporte significado.

## Consecuencia

Ningún indicador de estado o foco de esta aplicación debe depender de
`box-shadow` para seguir siendo visible con `forced-colors: active`.
