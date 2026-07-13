<#
.SYNOPSIS
  Adiciona a lógica de tarifa de venda (SPRICCONF/SPRICFICH/SPRICLIST) ao TEB_ITM_ETIQx60.rpt.
  TEM DE CORRER EM POWERSHELL 32-BIT.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$RptPath,
  [Parameter(Mandatory)][string]$OutPath,
  [string]$Pli = "ZPPE",
  [Parameter(Mandatory)][string]$WorkServer,   # OLE DB alcançável p/ build (ex. 192.168.1.211)
  [Parameter(Mandatory)][string]$WorkDb,
  [Parameter(Mandatory)][string]$WorkUser,
  [Parameter(Mandatory)][string]$WorkPass,
  [Parameter(Mandatory)][string]$Dsn,          # DSN ODBC produção (ex. ADX_CRCNN_TEBX3)
  [Parameter(Mandatory)][string]$ProdDb,
  [Parameter(Mandatory)][string]$ProdUser,
  [Parameter(Mandatory)][string]$ProdPass,
  [switch]$NoRepoint
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
$cs = Get-Content (Join-Path $PSScriptRoot 'X3RptAddTariff.cs') -Raw -Encoding UTF8
Add-Type -TypeDefinition $cs -ReferencedAssemblies $refs -Language CSharp

$sql = Get-Content (Join-Path $PSScriptRoot 'tarifa.sql') -Raw

$dir = Split-Path -Parent $OutPath
if($dir -and -not (Test-Path $dir)){ New-Item -ItemType Directory -Force -Path $dir | Out-Null }

$res = [X3RptAddTariff]::Build($RptPath,$OutPath,$Pli,$sql,$WorkServer,$WorkDb,$WorkUser,$WorkPass,$Dsn,$ProdDb,$ProdUser,$ProdPass,(-not $NoRepoint))
Write-Host "LOG: $res"
Write-Host ("RPT criado: " + (Test-Path $OutPath) + "  ->  $OutPath")
