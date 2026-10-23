param([string]$Solution='GoDental.sln',[string]$ReportPath='TestResults/dependency-audit.json')
$ErrorActionPreference='Stop'
function Read-Property($Object,[string]$Name) {
    if($null -eq $Object){return $null}
    $property=$Object.PSObject.Properties[$Name]
    if($null -ne $property){return $property.Value}
    return $null
}
$raw=& dotnet list $Solution package --vulnerable --include-transitive --format json
if($LASTEXITCODE -ne 0){throw 'No se pudo consultar el informe de vulnerabilidades; no se asume un resultado limpio.'}
$reportText=$raw -join [Environment]::NewLine
$report=$reportText | ConvertFrom-Json
if((Read-Property $report 'version') -ne 1){throw 'Formato de informe no reconocido.'}
if(@(Read-Property $report 'problems').Where({$null -ne $_}).Count -gt 0){throw 'La consulta de dependencias contiene problemas; revisar el proveedor antes de aceptar el resultado.'}
$projects=@(Read-Property $report 'projects').Where({$null -ne $_})
if($projects.Count -eq 0){throw 'El informe no contiene proyectos.'}
$parent=Split-Path -Parent $ReportPath
if($parent){New-Item -ItemType Directory -Path $parent -Force | Out-Null}
$reportText | Out-File -FilePath $ReportPath -Encoding utf8
$blocking=0
foreach($project in $projects){foreach($framework in @(Read-Property $project 'frameworks')){
    foreach($kind in @('topLevelPackages','transitivePackages')){foreach($package in @(Read-Property $framework $kind)){
        foreach($vulnerability in @(Read-Property $package 'vulnerabilities')){
            if($null -eq $vulnerability){continue}
            $severity=Read-Property $vulnerability 'severity'
            if($severity -notin @('Low','Moderate','High','Critical')){throw 'Severidad de aviso no reconocida; revisar el informe.'}
            Write-Host ('Aviso de dependencia: {0} {1} / {2}' -f (Read-Property $package 'id'),(Read-Property $package 'resolvedVersion'),$severity)
            if($severity -in @('High','Critical')){$blocking++}
        }
    }}
}}
Write-Host ('Proyectos consultados: {0}. Avisos altos/críticos: {1}.' -f $projects.Count,$blocking)
if($blocking -gt 0){throw 'Existen vulnerabilidades altas/críticas en dependencias. Corregir antes de integrar.'}
