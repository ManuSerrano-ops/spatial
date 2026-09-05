[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $projectRoot 'publish'
$seed = Join-Path $projectRoot 'runtime-data\data'
$requiredDataFiles = @('maps.json', 'assignments.json', 'positions.json', 'events.json', 'scenarios.json', 'people.json', 'devices.json', 'locations.json', 'state.json')
$revision = (& git -C $projectRoot rev-parse --short HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($revision)) { throw 'No se pudo obtener la revisión Git para el paquete.' }
$informationalVersion = "1.0.0+$revision"

if (Test-Path $OutputPath) { throw "El destino ya existe: $OutputPath" }
foreach ($name in $requiredDataFiles) {
    $source = Join-Path $seed $name
    if (-not (Test-Path -LiteralPath $source)) { throw "Falta el fichero semilla: $source" }
    try { Get-Content -LiteralPath $source -Raw | ConvertFrom-Json | Out-Null }
    catch { throw "El fichero semilla no contiene JSON válido: $source" }
}

& dotnet publish (Join-Path $projectRoot 'PlanoOpenSpaceIT.Windows.csproj') --no-restore -c Release -o $publish "-p:InformationalVersion=$informationalVersion"
if ($LASTEXITCODE -ne 0) { throw 'La publicación falló.' }
$requiredPublishFiles = @(
    (Join-Path $publish 'PlanoOpenSpaceIT.Windows.exe'),
    (Join-Path $publish 'THIRD_PARTY_NOTICES.md'),
    (Join-Path $publish 'LICENSES\LUCIDE-ISC.txt')
)
foreach ($file in $requiredPublishFiles) {
    if (-not (Test-Path -LiteralPath $file)) { throw "La publicación no contiene: $file" }
}

New-Item -ItemType Directory -Path (Join-Path $OutputPath 'payload\LICENSES'), (Join-Path $OutputPath 'seed-data') -Force | Out-Null
$payloadExe = Join-Path $OutputPath 'payload\PlanoOpenSpaceIT.Windows.exe'
Copy-Item -LiteralPath (Join-Path $publish 'PlanoOpenSpaceIT.Windows.exe') -Destination $payloadExe
$exeHash = (Get-FileHash -LiteralPath $payloadExe -Algorithm SHA256).Hash.ToLowerInvariant()
@(
    "Revision: $revision"
    "InformationalVersion: $informationalVersion"
    "GeneratedUtc: $([DateTime]::UtcNow.ToString('O'))"
    'Executable: PlanoOpenSpaceIT.Windows.exe'
    "ExecutableSha256: $exeHash"
) | Set-Content -LiteralPath (Join-Path $OutputPath 'payload\VERSION.txt') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $publish 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $OutputPath 'payload\THIRD_PARTY_NOTICES.md')
Copy-Item -LiteralPath (Join-Path $publish 'LICENSES\LUCIDE-ISC.txt') -Destination (Join-Path $OutputPath 'payload\LICENSES\LUCIDE-ISC.txt')
foreach ($name in $requiredDataFiles) { Copy-Item -LiteralPath (Join-Path $seed $name) -Destination (Join-Path $OutputPath "seed-data\$name") }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-PlanoOpenSpaceIT.ps1') -Destination (Join-Path $OutputPath 'Install-PlanoOpenSpaceIT.ps1')
Write-Host "Paquete creado: $OutputPath"
