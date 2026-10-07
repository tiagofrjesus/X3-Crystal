param($Rpt, [switch]$Cert)
$gac='C:\WINDOWS\Microsoft.Net\assembly\GAC_MSIL'; $v='v4.0_13.0.4000.0__692fbea5521e1304'
$refs=@('CrystalReports.Engine','Shared','ReportAppServer.ClientDoc','ReportAppServer.DataDefModel','ReportAppServer.ReportDefModel','ReportAppServer.Controllers','ReportAppServer.CommonObjectModel') | % { "$gac\CrystalDecisions.$_\$v\CrystalDecisions.$_.dll" }
Add-Type -TypeDefinition (Get-Content (Join-Path $PSScriptRoot 'X3RptSectionFormulas.cs') -Raw -Encoding UTF8) -ReferencedAssemblies $refs
if ($Cert) { [X3RptSectionFormulas]::DumpCert($Rpt) } else { [X3RptSectionFormulas]::Dump($Rpt) }