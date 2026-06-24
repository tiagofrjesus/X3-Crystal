<#
.SYNOPSIS
  Executa um relatorio Crystal Reports (.rpt) usando o SDK SAP CrystalDecisions
  instalado localmente, aplica o logon a base de dados e exporta para ficheiro.

.EXAMPLE
  .\Export-X3Report.ps1 -RptPath "C:\X3\reports\BONLIV.rpt" `
      -Server "SQLSRV\X3" -Database "x3v12" -User "sa" -Password "***" `
      -OutFile "C:\tmp\bonliv.pdf" -Format PDF `
      -Parameters @{ X3DOS = "SEED;srv;web"; numdeb = "SI0001"; numfin = "SI0001" }

.NOTES
  Bitness: se der erro de "load" do crpe, corre com o PowerShell 32-bit:
  C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string]$RptPath,
  [Parameter(Mandatory)] [string]$OutFile,
  [ValidateSet('PDF','Excel','ExcelData','Word','RTF','CSV','HTML','XML')]
  [string]$Format = 'PDF',

  # Logon a BD (deixa em branco se o .rpt usa dados embebidos / ja tem logon)
  [string]$Server,
  [string]$Database,
  [string]$User,
  [string]$Password,
  [string]$IntegratedSecurity,   # "true" para Windows auth

  # Parametros do report: @{ nome = valor; ... }
  [hashtable]$Parameters = @{}
)

$ErrorActionPreference = 'Stop'

# --- Carregar o SDK ---
Add-Type -AssemblyName 'CrystalDecisions.CrystalReports.Engine, Version=13.0.4000.0, Culture=neutral, PublicKeyToken=692fbea5521e1304'
Add-Type -AssemblyName 'CrystalDecisions.Shared, Version=13.0.4000.0, Culture=neutral, PublicKeyToken=692fbea5521e1304'

if (-not (Test-Path $RptPath)) { throw "RPT nao encontrado: $RptPath" }

$report = New-Object CrystalDecisions.CrystalReports.Engine.ReportDocument
$report.Load((Resolve-Path $RptPath).Path)

# --- Aplicar logon a BD a todas as tabelas (driver de ligacao) ---
if ($Server -and $Database) {
  $ci = New-Object CrystalDecisions.Shared.ConnectionInfo
  $ci.ServerName   = $Server
  $ci.DatabaseName = $Database
  if ($IntegratedSecurity -eq 'true') {
    $ci.IntegratedSecurity = $true
  } else {
    $ci.UserID   = $User
    $ci.Password = $Password
  }

  foreach ($tbl in $report.Database.Tables) {
    $li = $tbl.LogOnInfo
    $li.ConnectionInfo.ServerName        = $ci.ServerName
    $li.ConnectionInfo.DatabaseName      = $ci.DatabaseName
    $li.ConnectionInfo.UserID            = $ci.UserID
    $li.ConnectionInfo.Password          = $ci.Password
    $li.ConnectionInfo.IntegratedSecurity= $ci.IntegratedSecurity
    $tbl.ApplyLogOnInfo($li)
  }
  # Tambem aplica a sub-reports
  foreach ($sub in $report.Subreports) {
    foreach ($tbl in $sub.Database.Tables) {
      $li = $tbl.LogOnInfo
      $li.ConnectionInfo.ServerName        = $ci.ServerName
      $li.ConnectionInfo.DatabaseName      = $ci.DatabaseName
      $li.ConnectionInfo.UserID            = $ci.UserID
      $li.ConnectionInfo.Password          = $ci.Password
      $li.ConnectionInfo.IntegratedSecurity= $ci.IntegratedSecurity
      $tbl.ApplyLogOnInfo($li)
    }
  }
}

# --- Parametros ---
foreach ($key in $Parameters.Keys) {
  try { $report.SetParameterValue($key, $Parameters[$key]) }
  catch { Write-Warning "Parametro '$key' nao existe no report — ignorado." }
}

# --- Mapear formato -> tipo de exportacao ---
$fmt = switch ($Format) {
  'PDF'       { [CrystalDecisions.Shared.ExportFormatType]::PortableDocFormat }
  'Excel'     { [CrystalDecisions.Shared.ExportFormatType]::Excel }
  'ExcelData' { [CrystalDecisions.Shared.ExportFormatType]::ExcelRecord }
  'Word'      { [CrystalDecisions.Shared.ExportFormatType]::WordForWindows }
  'RTF'       { [CrystalDecisions.Shared.ExportFormatType]::RichText }
  'CSV'       { [CrystalDecisions.Shared.ExportFormatType]::CharacterSeparatedValues }
  'HTML'      { [CrystalDecisions.Shared.ExportFormatType]::HTML40 }
  'XML'       { [CrystalDecisions.Shared.ExportFormatType]::Xml }
}

$outDir = Split-Path -Parent $OutFile
if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }

$report.ExportToDisk($fmt, $OutFile)
$report.Close()
$report.Dispose()

Write-Host "OK -> $OutFile" -ForegroundColor Green
