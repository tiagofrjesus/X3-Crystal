<#
.SYNOPSIS
  Teste local do TEB_PIECE com dados reais de 1 peca (sem AREPORTM): copia de teste + PDF. Correr em 32-bit.
  Ex.: Test-PieceExport.ps1 -RptPath ..\..\Reports-TEB\TEB_PIECE.rpt -OutPdf $env:TEMP\piece.pdf -Pass ***
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$RptPath,
  [Parameter(Mandatory)][string]$OutPdf,
  [Parameter(Mandatory)][string]$Pass,
  [string]$Typ = "ODT",
  [string]$Num = "ODT-E012607/0002",
  [string]$Jou = "ODG",
  [string]$Leg = "POR",
  [string]$Dsn = "TEST_TEB211",
  [switch]$RealSelection,          # mantem a record selection do relatorio (como no X3)
  [string]$Cpy = "TEB", [string]$Fcy = "E01", [string]$Dat = "2026-07-16",
  [string]$Fct = "CONSPCE", [string]$Prf = "ADMIN", [string]$Usr = "TJ"
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
Add-Type -TypeDefinition (Get-Content (Join-Path $PSScriptRoot 'X3RptPieceTest.cs') -Raw -Encoding UTF8) -ReferencedAssemblies $refs -Language CSharp
$copy = Join-Path $env:TEMP ("pttest_" + [IO.Path]::GetFileName($RptPath))
if (Test-Path $copy) { Remove-Item $copy -Force }
$bTyp = if ($RealSelection) { "" } else { $Typ }   # "" = Build nao mexe na record selection
Write-Host ([X3RptPieceTest]::Build((Resolve-Path $RptPath).Path, $copy, $bTyp, $Num, $Jou, $Leg, $Dsn, 'tebx3', 'sa', $Pass))
if (Test-Path $OutPdf) { Remove-Item $OutPdf -Force }
if ($RealSelection) {
  Write-Host ([X3RptPieceTest]::ExportReal($copy, $OutPdf, $Dsn, 'tebx3', 'TEB', 'sa', $Pass, 'TEBX3', $Cpy, $Fcy, $Typ, $Num, [datetime]$Dat, $Fct, $Prf, $Usr))
} else {
  Write-Host ([X3RptPieceTest]::Export($copy, $OutPdf, $Dsn, 'tebx3', 'TEB', 'sa', $Pass, 'TEBX3'))
}
