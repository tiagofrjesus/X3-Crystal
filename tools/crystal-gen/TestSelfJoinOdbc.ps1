[CmdletBinding()]
param(
  [string]$SeedPath = "D:\Git\X3-Crystal\New-Blank.rpt",
  [Parameter(Mandatory)][string]$OutPath,
  [string]$Recibo = "RPMB-26E01/01682",
  [string]$WorkDsn = "X3_TEB_DEV",
  [string]$WorkDb = "teb",
  [string]$WorkUser = "sa",
  [Parameter(Mandatory)][string]$WorkPass,
  [switch]$DoExport,
  [string]$TxtOutPath
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
$cs = Get-Content (Join-Path $PSScriptRoot 'X3RptTestSelfJoinOdbc.cs') -Raw -Encoding UTF8
Add-Type -TypeDefinition $cs -ReferencedAssemblies $refs -Language CSharp

if (-not $DoExport) {
  if (Test-Path $OutPath) { Remove-Item $OutPath -Force }
  $res = [X3RptTestSelfJoinOdbc]::Build((Resolve-Path $SeedPath).Path, $OutPath, $Recibo, $WorkDsn, $WorkDb, $WorkUser, $WorkPass)
  Write-Host "BUILD LOG: $res"
  Write-Host ("RPT criado: " + (Test-Path $OutPath))
} else {
  $res = [X3RptTestSelfJoinOdbc]::ExportText((Resolve-Path $OutPath).Path, $TxtOutPath, $WorkUser, $WorkPass)
  Write-Host "EXPORT LOG: $res"
}
