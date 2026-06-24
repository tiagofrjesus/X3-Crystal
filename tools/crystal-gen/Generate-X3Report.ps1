<#
.SYNOPSIS
  Gera um .rpt do Sage X3 a partir de um Command SQL, usando o SDK Crystal (RAS).
  TEM DE CORRER EM POWERSHELL 32-BIT:
    C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -File Generate-X3Report.ps1 ...

.EXAMPLE
  & C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -ExecutionPolicy Bypass -File `
    .\Generate-X3Report.ps1 -Blank "d:\Git\X3-Crystal\New-Blank.rpt" `
    -OutRpt "d:\Git\X3-Crystal\Reports-Gerados\ZEXTRART.rpt" -SqlFile ".\extrato.sql" `
    -WorkServer "192.168.1.211" -Dsn "ADX_CRCNN_TEBX3" -Db "tebx3" -User "sa" -Password "***" `
    -Company "TEB Materiais de Construção, Lda"
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$Blank,
  [Parameter(Mandatory)][string]$OutRpt,
  [Parameter(Mandatory)][string]$SqlFile,
  [Parameter(Mandatory)][string]$WorkServer,   # servidor OLE DB alcançável p/ build (ex. IP)
  [Parameter(Mandatory)][string]$Dsn,          # DSN ODBC do X3 (ex. ADX_CRCNN_TEBX3)
  [Parameter(Mandatory)][string]$Database,
  [Parameter(Mandatory)][string]$User,
  [Parameter(Mandatory)][string]$Password,
  [string]$Company = ""
)
$ErrorActionPreference='Stop'
if([Environment]::Is64BitProcess){ throw "Corre em PowerShell 32-BIT (SysWOW64). O runtime Crystal .NET é 32-bit." }

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
$cs = Get-Content (Join-Path $PSScriptRoot 'X3Rpt.cs') -Raw -Encoding UTF8
Add-Type -TypeDefinition $cs -ReferencedAssemblies $refs -Language CSharp

$sql = Get-Content $SqlFile -Raw
$dir = Split-Path -Parent $OutRpt
if($dir -and -not (Test-Path $dir)){ New-Item -ItemType Directory -Force -Path $dir | Out-Null }
if(Test-Path $OutRpt){ Remove-Item $OutRpt -Force }

$res = [X3Rpt]::Build($Blank,$OutRpt,$sql,$WorkServer,$Dsn,$Database,$User,$Password,$Company)
Write-Host "LOG: $res"
Write-Host ("RPT criado: " + (Test-Path $OutRpt) + "  ->  $OutRpt")
