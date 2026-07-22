<#
.SYNOPSIS
  QA VISUAL apenas: exporta um PDF local do TEB_PIECE com a formula typref neutralizada
  (TextOfChapter nunca resolve nesta maquina), so para confirmar visualmente o layout do
  cabecalho. NUNCA usar o .rpt gerado por este script como entrega final.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$RptPath,
  [Parameter(Mandatory)][string]$PdfPath,
  [Parameter(Mandatory)][string]$WorkServer,
  [Parameter(Mandatory)][string]$WorkDb,
  [Parameter(Mandatory)][string]$WorkUser,
  [Parameter(Mandatory)][string]$WorkPass,
  [string]$TypDoc = "ODT",
  [string]$NumDoc = "ODT-E012607/0002"
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
$cs = Get-Content (Join-Path $PSScriptRoot 'X3RptPieceQaVisual.cs') -Raw -Encoding UTF8
Add-Type -TypeDefinition $cs -ReferencedAssemblies $refs -Language CSharp
$dir = Split-Path -Parent $PdfPath
if($dir -and -not (Test-Path $dir)){ New-Item -ItemType Directory -Force -Path $dir | Out-Null }
if (Test-Path $PdfPath) { Remove-Item $PdfPath -Force }
$res = [X3RptPieceQaVisual]::Export($RptPath,$PdfPath,$WorkServer,$WorkDb,$WorkUser,$WorkPass,$TypDoc,$NumDoc)
Write-Host "LOG: $res"
Write-Host ("PDF criado: " + (Test-Path $PdfPath) + "  ->  $PdfPath")
