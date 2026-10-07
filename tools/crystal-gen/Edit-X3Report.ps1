<#
.SYNOPSIS
  Editor generico de .rpt: aplica um ficheiro JSON de operacoes (ver cabecalho de X3RptEdit.cs).
  Grava SEMPRE noutro ficheiro (-Out); se alguma operacao falhar nao grava nada.
  Pode ser chamado de PowerShell 64-bit: relanca-se sozinho em 32-bit.
.EXAMPLE
  .\Edit-X3Report.ps1 -Rpt ..\..\Reports-TEB\TEB_PIECE.rpt -Ops .\specs\TEB_PIECE.json -Out $env:TEMP\TEB_PIECE.rpt
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$Rpt,
  [Parameter(Mandatory)][string]$Ops,
  [Parameter(Mandatory)][string]$Out
)
$ErrorActionPreference='Stop'
$Rpt = (Resolve-Path $Rpt).Path; $Ops = (Resolve-Path $Ops).Path
$Out = [IO.Path]::GetFullPath($Out)
if([Environment]::Is64BitProcess){
  & "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -File $PSCommandPath -Rpt $Rpt -Ops $Ops -Out $Out
  exit $LASTEXITCODE
}
$gac='C:\WINDOWS\Microsoft.Net\assembly\GAC_MSIL'
$v='v4.0_13.0.4000.0__692fbea5521e1304'
$refs=@('CrystalReports.Engine','Shared','ReportAppServer.ClientDoc','ReportAppServer.DataDefModel',
        'ReportAppServer.ReportDefModel','ReportAppServer.Controllers','ReportAppServer.CommonObjectModel') |
      % { "$gac\CrystalDecisions.$_\$v\CrystalDecisions.$_.dll" }
$refs += 'System.Web.Extensions'
Add-Type -TypeDefinition (Get-Content (Join-Path $PSScriptRoot 'X3RptEdit.cs') -Raw -Encoding UTF8) -ReferencedAssemblies $refs -Language CSharp
if (Test-Path $Out) { Remove-Item $Out -Force }
$r = [X3RptEdit]::Run($Rpt, $Ops, $Out)
Write-Host $r
if ($r -notmatch 'GRAVADO') { exit 1 }
