param($Rpt, $Out)
$gac='C:\WINDOWS\Microsoft.Net\assembly\GAC_MSIL'; $v='v4.0_13.0.4000.0__692fbea5521e1304'
$refs=@('CrystalReports.Engine','Shared','ReportAppServer.ClientDoc','ReportAppServer.DataDefModel','ReportAppServer.ReportDefModel','ReportAppServer.Controllers','ReportAppServer.CommonObjectModel') | % { "$gac\CrystalDecisions.$_\$v\CrystalDecisions.$_.dll" }
$refs += 'System.Web.Extensions'
Add-Type -TypeDefinition (Get-Content (Join-Path $PSScriptRoot 'X3RptTexts.cs') -Raw -Encoding UTF8) -ReferencedAssemblies $refs
[IO.File]::WriteAllText($Out, [X3RptTexts]::Dump($Rpt), (New-Object Text.UTF8Encoding($false)))