param(
  [Parameter(Mandatory)][string]$RptPath,
  [Parameter(Mandatory)][string]$OutFile,
  [Parameter(Mandatory)][string]$ItmRef
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName 'CrystalDecisions.CrystalReports.Engine, Version=13.0.4000.0, Culture=neutral, PublicKeyToken=692fbea5521e1304'
Add-Type -AssemblyName 'CrystalDecisions.Shared, Version=13.0.4000.0, Culture=neutral, PublicKeyToken=692fbea5521e1304'

$report = New-Object CrystalDecisions.CrystalReports.Engine.ReportDocument
$report.Load((Resolve-Path $RptPath).Path)

Write-Host "Main report tables:"
foreach ($tbl in $report.Database.Tables) {
  Write-Host ("  " + $tbl.Name + " Server=" + $tbl.LogOnInfo.ConnectionInfo.ServerName + " DB=" + $tbl.LogOnInfo.ConnectionInfo.DatabaseName)
}
Write-Host "Subreports:"
foreach ($sub in $report.Subreports) {
  Write-Host ("  subreport: " + $sub.Name)
  foreach ($tbl in $sub.Database.Tables) {
    Write-Host ("    " + $tbl.Name + " Server=" + $tbl.LogOnInfo.ConnectionInfo.ServerName + " DB=" + $tbl.LogOnInfo.ConnectionInfo.DatabaseName)
  }
}

$report.SetParameterValue("ITMREF", $ItmRef)
$report.SetParameterValue("cPVP", $true)
$report.SetParameterValue("civa", $false)
$report.SetParameterValue("etat", "TEB_ITM_ETIQX60")
$report.SetParameterValue("numedt", 1)
$report.SetParameterValue("usr", "ADMIN")
$report.SetParameterValue("X3DOS", "TEBX3;teb-sagesql;X3")
$report.SetParameterValue("X3LAN", "POR")

try {
  $report.ExportToDisk([CrystalDecisions.Shared.ExportFormatType]::PortableDocFormat, $OutFile)
  Write-Host "OK -> $OutFile"
} catch {
  Write-Host "EXPORT ERR: $($_.Exception.Message)"
  Write-Host "InnerException: $($_.Exception.InnerException)"
}
$report.Close()
$report.Dispose()
