# Teste end-to-end de um relatorio X3 com dados reais: cria copia com UFLs em stub e exporta-a via
# Export-Full (pedido de impressao existente em AREPORTM). Correr em PowerShell 32-bit.
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$RptPath,
  [Parameter(Mandatory)][string]$OutTxt,
  [Parameter(Mandatory)][string]$Pass,
  [Parameter(Mandatory)][string]$Usr,
  [Parameter(Mandatory)][double]$NumEdt,
  [string]$Etat = "TEB_REC",
  [string]$Dsn = "TEST_TEB211",
  [ValidateSet('txt','pdf')][string]$Format = "txt"
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
foreach($f in 'X3RptStubUfl.cs','X3RptExportFull.cs'){
  Add-Type -TypeDefinition (Get-Content (Join-Path $PSScriptRoot $f) -Raw -Encoding UTF8) -ReferencedAssemblies $refs -Language CSharp
}
$stub = Join-Path $env:TEMP ("stub_" + [IO.Path]::GetFileName($RptPath))
if (Test-Path $stub) { Remove-Item $stub -Force }
Write-Host ([X3RptStubUfl]::Build((Resolve-Path $RptPath).Path, $stub))
if (Test-Path $OutTxt) { Remove-Item $OutTxt -Force }
Write-Host ([X3RptExportFull]::Export($stub, $OutTxt, $Format, $Dsn, 'tebx3', 'TEB', 'sa', $Pass, $Usr, $Etat, $NumEdt, 0, 'POR', 'TEBX3'))
