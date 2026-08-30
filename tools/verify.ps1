[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'PlanoOpenSpaceIT.Windows.csproj'

Push-Location $projectRoot
try {
    Get-ChildItem -Path 'Resources/js' -Recurse -Filter '*.js' | Sort-Object FullName | ForEach-Object {
        & node --check $_.FullName
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    Get-ChildItem -Path 'tests' -Filter '*harness.js' | Sort-Object Name | ForEach-Object {
        & node $_.FullName
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    & python 'tests/test_light_maps.py'
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Get-ChildItem -Path 'tests' -Recurse -Filter '*.csproj' | Sort-Object FullName | ForEach-Object {
        dotnet restore $_.FullName
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        dotnet run --project $_.FullName --no-restore -p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    dotnet build $project --no-restore -p:UseSharedCompilation=false
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
