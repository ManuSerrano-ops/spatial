[CmdletBinding()]
param(
    [string]$InstallPath,
    [string]$NetworkRoot,
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path -Parent $PSCommandPath
$payload = Join-Path $packageRoot 'payload'
$seed = Join-Path $packageRoot 'seed-data'
$requiredDataFiles = @('maps.json', 'assignments.json', 'positions.json', 'events.json', 'scenarios.json', 'people.json', 'devices.json', 'locations.json', 'state.json')

function Assert-WritableDirectory([string]$Path) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
    $probe = Join-Path $Path ('.plano-open-space-install-' + [guid]::NewGuid().ToString('N') + '.tmp')
    [System.IO.File]::WriteAllText($probe, '')
    Remove-Item -Force $probe
}

function Assert-ValidJsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Falta el fichero de datos: $Path" }
    try { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json | Out-Null }
    catch { throw "El fichero JSON no es válido: $Path" }
}

$requiredPayloadFiles = @(
    (Join-Path $payload 'PlanoOpenSpaceIT.Windows.exe'),
    (Join-Path $payload 'VERSION.txt'),
    (Join-Path $payload 'THIRD_PARTY_NOTICES.md'),
    (Join-Path $payload 'LICENSES\LUCIDE-ISC.txt')
)
foreach ($file in $requiredPayloadFiles) {
    if (-not (Test-Path -LiteralPath $file)) { throw "El paquete no contiene: $file" }
}
foreach ($name in $requiredDataFiles) { Assert-ValidJsonFile (Join-Path $seed $name) }

if (-not $NetworkRoot) { $NetworkRoot = Read-Host 'Ruta compartida de datos (por ejemplo G:\\)' }
if (-not $InstallPath) { $InstallPath = Read-Host 'Carpeta donde instalar la aplicación' }
if ([string]::IsNullOrWhiteSpace($NetworkRoot) -or [string]::IsNullOrWhiteSpace($InstallPath)) { throw 'La ruta compartida y la carpeta de instalación son obligatorias.' }

$NetworkRoot = [System.IO.Path]::GetFullPath($NetworkRoot)
Assert-WritableDirectory $NetworkRoot
$dataPath = Join-Path $NetworkRoot 'data'
$backupsPath = Join-Path $NetworkRoot 'backups'
$logsPath = Join-Path $NetworkRoot 'logs'

if (-not (Test-Path $dataPath)) {
    $stagingPath = Join-Path $NetworkRoot ('.plano-open-space-seed-' + [guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Path $stagingPath | Out-Null
        foreach ($name in $requiredDataFiles) {
            Copy-Item -LiteralPath (Join-Path $seed $name) -Destination (Join-Path $stagingPath $name)
        }
        foreach ($name in $requiredDataFiles) { Assert-ValidJsonFile (Join-Path $stagingPath $name) }
        Move-Item -LiteralPath $stagingPath -Destination $dataPath
        Write-Host "Datos compartidos inicializados en $dataPath"
    } finally {
        if (Test-Path -LiteralPath $stagingPath) { Remove-Item -LiteralPath $stagingPath -Recurse -Force }
    }
} else {
    foreach ($name in $requiredDataFiles) { Assert-ValidJsonFile (Join-Path $dataPath $name) }
    Write-Host "Se conservan los datos compartidos existentes en $dataPath"
}

New-Item -ItemType Directory -Force -Path $backupsPath, $logsPath, $InstallPath | Out-Null
Copy-Item -LiteralPath (Join-Path $payload 'PlanoOpenSpaceIT.Windows.exe') -Destination (Join-Path $InstallPath 'PlanoOpenSpaceIT.Windows.exe') -Force
Copy-Item -LiteralPath (Join-Path $payload 'VERSION.txt') -Destination (Join-Path $InstallPath 'VERSION.txt') -Force
Copy-Item -LiteralPath (Join-Path $payload 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $InstallPath 'THIRD_PARTY_NOTICES.md') -Force
Copy-Item -LiteralPath (Join-Path $payload 'LICENSES') -Destination (Join-Path $InstallPath 'LICENSES') -Recurse -Force

$config = [ordered]@{
    networkRoot = $NetworkRoot
    dataFolder = 'data'
    backupFolder = 'backups'
    logsFolder = 'logs'
    logMaxFileSizeBytes = 1048576
    logMaxHistoryFiles = 5
    backupRetentionMode = 'disabled'
    readOnly = $false
}
$config | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $InstallPath 'config.json') -Encoding UTF8
Write-Host "Instalación completada: $InstallPath"
Write-Host "Datos compartidos: $NetworkRoot"
if ($Launch) { Start-Process (Join-Path $InstallPath 'PlanoOpenSpaceIT.Windows.exe') }
