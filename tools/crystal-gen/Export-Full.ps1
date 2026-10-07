[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$RptPath,
  [Parameter(Mandatory)][string]$OutPath,
  [ValidateSet('txt','pdf')][string]$Format = 'txt',
  [string]$Dsn = "TEST_TEB211",
  [string]$DbName = "tebx3",
  [string]$Schema = "TEB",
  [string]$User = "sa",
  [Parameter(Mandatory)][string]$Pass,
  [Parameter(Mandatory)][string]$Usr,
  [Parameter(Mandatory)][string]$Etat,
  [Parameter(Mandatory)][double]$NumEdt,
  [double]$SeqEdt = 0,
  [string]$Lan = "POR",
  [string]$Dos = "TEBX3"
)
$ErrorActionPreference='Stop'
if([Environment]::Is64BitProcess){ throw "Corre em PowerShell 32-BIT (SysWOW64)." }
$gac='C:\WINDOWS\Microsoft.Net\assembly\GAC_MSIL'
$v='v4.0_13.0.4000.0__692fbea5521e1304'
$refs=@(
 "$gac\CrystalDecisions.CrystalReports.Engine\$v\CrystalDecisions.CrystalReports.Engine.dll"
 "$gac\CrystalDecisions.Shared\$v\CrystalDecisions.Shared.dll"
)
$cs = Get-Content (Join-Path $PSScriptRoot 'X3RptExportFull.cs') -Raw -Encoding UTF8
Add-Type -TypeDefinition $cs -ReferencedAssemblies $refs -Language CSharp
if (Test-Path $OutPath) { Remove-Item $OutPath -Force }
$res = [X3RptExportFull]::Export((Resolve-Path $RptPath).Path, $OutPath, $Format, $Dsn, $DbName, $Schema, $User, $Pass, $Usr, $Etat, $NumEdt, $SeqEdt, $Lan, $Dos)
Write-Host $res

