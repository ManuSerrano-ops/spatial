[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'PlanoOpenSpaceIT.Windows.csproj'

Push-Location $projectRoot
try {
    dotnet restore $project
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet build $project --no-restore -p:UseSharedCompilation=false
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
