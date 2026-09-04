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

    $harnesses = @(Get-ChildItem -Path 'tests' -Filter '*harness.js' | Sort-Object Name)
    & node --test $harnesses.FullName
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & python 'tests/test_light_maps.py'
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    @(
        'tests/PlanoOpenSpaceIT.Domain.Tests/PlanoOpenSpaceIT.Domain.Tests.csproj',
        'tests/PlanoOpenSpaceIT.Desktop.Tests/PlanoOpenSpaceIT.Desktop.Tests.csproj'
    ) | ForEach-Object {
        dotnet restore $_
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        dotnet test $_ --no-restore -p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    dotnet build $project --no-restore -p:UseSharedCompilation=false
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
