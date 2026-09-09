$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot

try {
    Write-Host '==> GoDental: restore' -ForegroundColor Cyan
    dotnet restore GoDental.sln
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host '==> GoDental: build Release' -ForegroundColor Cyan
    dotnet build GoDental.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host '==> GoDental: tests + coverage' -ForegroundColor Cyan
    dotnet test GoDental.sln -c Release --no-build --no-restore `
        --collect:'XPlat Code Coverage' `
        --logger 'trx;LogFileName=godental-tests.trx' `
        --results-directory TestResults
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host '==> Todo correcto. La solución compila y los tests pasaron.' -ForegroundColor Green
    Write-Host "    Resultados: $repoRoot\TestResults"
}
finally {
    Pop-Location
}
