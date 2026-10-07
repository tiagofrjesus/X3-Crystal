<#
.SYNOPSIS
  Dump read-only de grupos, running totals e estilo (fontes/cores/bordas) de um .rpt. Correr em 32-bit.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$RptPath, [string]$OutFile)
$ErrorActionPreference='Stop'
if([Environment]::Is64BitProcess){ throw "Corre em PowerShell 32-BIT (SysWOW64)." }
$gac='C:\WINDOWS\Microsoft.Net\assembly\GAC_MSIL'
$v='v4.0_13.0.4000.0__692fbea5521e1304'
$refs=@(
 "$gac\CrystalDecisions.CrystalReports.Engine\$v\CrystalDecisions.CrystalReports.Engine.dll"
 "$gac\CrystalDecisions.Shared\$v\CrystalDecisions.Shared.dll"
 "System.Drawing"
)
Add-Type -TypeDefinition (Get-Content (Join-Path $PSScriptRoot 'X3RptDumpStyle.cs') -Raw -Encoding UTF8) -ReferencedAssemblies $refs -Language CSharp
$res = [X3RptDumpStyle]::Dump((Resolve-Path $RptPath).Path)
if ($OutFile) { $res | Out-File -FilePath $OutFile -Encoding UTF8; Write-Host "OK -> $OutFile" } else { Write-Host $res }
