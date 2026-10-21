[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServerInstance,

    [switch]$TrustServerCertificate
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$databaseDir = Join-Path $repoRoot 'database'

$scripts = @(
    '001_Core.sql',
    '002_Inventario.sql',
    '003_Compras.sql',
    '004_ClientesVentas.sql',
    '005_Fiscal_Base.sql',
    '006_Comercial_Base.sql',
    '007_Seed.sql',
    '008_ViewsProcedures.sql',
    '009_Seguridad_Parametros.sql',
    '010_Inventario_Operativo.sql',
    '011_Conteos_Inventario.sql',
    '012_MVP_Operativo.sql',
    '013_Pedidos_MVP.sql',
    '014_Codigos_Automaticos.sql',
    '015_Resolucion_Operaciones.sql',
    '016_Parametros_Comerciales.sql'
)

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    throw 'No se encontró sqlcmd en PATH. Instala SQL Server command-line utilities o ejecuta database/001-016 manualmente desde SSMS.'
}

foreach ($scriptName in $scripts) {
    $scriptPath = Join-Path $databaseDir $scriptName
    if (-not (Test-Path $scriptPath)) {
        throw "No se encontró el script requerido: $scriptPath"
    }
}

Write-Host "Servidor: $ServerInstance"
Write-Host 'Autenticación: Windows integrada'
Write-Host 'Ejecutando baseline y migraciones en orden 001 -> 015...'

foreach ($scriptName in $scripts) {
    $scriptPath = Join-Path $databaseDir $scriptName
    Write-Host "`n==> $scriptName"

    $arguments = @('-S', $ServerInstance, '-E', '-b', '-i', $scriptPath)
    if ($TrustServerCertificate) {
        $arguments += '-C'
    }

    & $sqlcmd.Source @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Falló $scriptName con código de salida $LASTEXITCODE. La secuencia se detuvo."
    }
}

Write-Host "`nBaseline SQL v0.1 instalada correctamente en $ServerInstance."
Write-Host 'Siguiente paso: abre Conexión y apariencia en GoDental y configura servidor/base.'
