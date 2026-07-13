<#
.SYNOPSIS
  v2: adiciona a view TEB.ZETIQTARIFA (tabela NATIVA, alias TARIFA) ao
  TEB_ITM_ETIQx60.rpt original, sem tocar nas ligações existentes.
  A view tem de existir na BD alcançável pelo DSN de build (ex. TEST_TEB211).
  TEM DE CORRER EM POWERSHELL 32-BIT.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$RptPath,     # .rpt ORIGINAL (sem lógica de tarifa)
  [Parameter(Mandatory)][string]$OutPath,
  [string]$WorkDsn  = "TEST_TEB211",
  [string]$WorkDb   = "tebx3",
  [Parameter(Mandatory)][string]$WorkUser,
  [Parameter(Mandatory)][string]$WorkPass
)
$ErrorActionPreference='Stop'
if([Environment]::Is64BitProcess){ throw "Corre em PowerShell 32-BIT (SysWOW64)." }

$gac='C:\WINDOWS\Microsoft.Net\assembly\GAC_MSIL'
$v='v4.0_13.0.4000.0__692fbea5521e1304'
$refs=@(
 "$gac\CrystalDecisions.CrystalReports.Engine\$v\CrystalDecisions.CrystalReports.Engine.dll"
 "$gac\CrystalDecisions.Shared\$v\CrystalDecisions.Shared.dll"
 "$gac\CrystalDecisions.ReportAppServer.ClientDoc\$v\CrystalDecisions.ReportAppServer.ClientDoc.dll"
 "$gac\CrystalDecisions.ReportAppServer.DataDefModel\$v\CrystalDecisions.ReportAppServer.DataDefModel.dll"
 "$gac\CrystalDecisions.ReportAppServer.ReportDefModel\$v\CrystalDecisions.ReportAppServer.ReportDefModel.dll"
 "$gac\CrystalDecisions.ReportAppServer.Controllers\$v\CrystalDecisions.ReportAppServer.Controllers.dll"
 "$gac\CrystalDecisions.ReportAppServer.CommonObjectModel\$v\CrystalDecisions.ReportAppServer.CommonObjectModel.dll"
)
$cs = Get-Content (Join-Path $PSScriptRoot 'X3RptAddTariffView.cs') -Raw -Encoding UTF8
Add-Type -TypeDefinition $cs -ReferencedAssemblies $refs -Language CSharp

$dir = Split-Path -Parent $OutPath
if($dir -and -not (Test-Path $dir)){ New-Item -ItemType Directory -Force -Path $dir | Out-Null }
if(Test-Path $OutPath){ Remove-Item $OutPath -Force }

$res = [X3RptAddTariffView]::Build($RptPath,$OutPath,$WorkDsn,$WorkDb,$WorkUser,$WorkPass)
Write-Host "LOG: $res"
Write-Host ("RPT criado: " + (Test-Path $OutPath) + "  ->  $OutPath")
