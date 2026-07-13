param(
  [Parameter(Mandatory)][string]$RptPath,
  [Parameter(Mandatory)][string]$OutDir,
  [Parameter(Mandatory)][string]$ItmRef
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName 'CrystalDecisions.CrystalReports.Engine, Version=13.0.4000.0, Culture=neutral, PublicKeyToken=692fbea5521e1304'
Add-Type -AssemblyName 'CrystalDecisions.Shared, Version=13.0.4000.0, Culture=neutral, PublicKeyToken=692fbea5521e1304'

$report = New-Object CrystalDecisions.CrystalReports.Engine.ReportDocument
$report.Load((Resolve-Path $RptPath).Path)

$report.SetParameterValue("ITMREF", $ItmRef)
$report.SetParameterValue("cPVP", $true)
$report.SetParameterValue("civa", $false)
$report.SetParameterValue("etat", "TEB_ITM_ETIQX60")
$report.SetParameterValue("numedt", 1)
$report.SetParameterValue("usr", "ADMIN")
$report.SetParameterValue("X3DOS", "TEBX3;teb-sagesql;X3")
$report.SetParameterValue("X3LAN", "POR")

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$report.ExportToDisk([CrystalDecisions.Shared.ExportFormatType]::HTML40, (Join-Path $OutDir "out.html"))
Write-Host "OK -> $OutDir"
$report.Close()
$report.Dispose()
