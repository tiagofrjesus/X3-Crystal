[CmdletBinding()]
param([Parameter(Mandatory)][string]$RptPath,[Parameter(Mandatory)][string]$OutPath)
$ErrorActionPreference='Stop'
if([Environment]::Is64BitProcess){ throw "32-bit only" }
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
$cs = Get-Content (Join-Path $PSScriptRoot 'X3RptBoxTest.cs') -Raw -Encoding UTF8
Add-Type -TypeDefinition $cs -ReferencedAssemblies $refs -Language CSharp
if (Test-Path $OutPath) { Remove-Item $OutPath -Force }
$res = [X3RptBoxTest]::Test((Resolve-Path $RptPath).Path, $OutPath)
Write-Host "LOG: $res"
$res2 = [X3RptBoxTest]::TestBlank("D:\Git\X3-Crystal\New-Blank.rpt", ($OutPath + ".blank.rpt"))
Write-Host "LOG-BLANK: $res2"
