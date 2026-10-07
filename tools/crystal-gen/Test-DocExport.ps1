# Teste end-to-end GENERICO de um relatorio de documento X3 com dados reais: copia com UFLs em stub ->
# exporta via X3RptExportFull para um pedido de impressao existente em AREPORTM -> (opcional) PNG.
# Pode ser chamado de PowerShell 64-bit: relanca-se em 32-bit para o export e faz o PNG em 64-bit.
# Ex.: .\Test-DocExport.ps1 -RptPath ..\..\Lisoaz\RPTs\LZ_FAC.rpt -Out $env:TEMP\fac.pdf -Usr ADMIN -Etat TEB_FAC -NumEdt 525088 -Extra 'X3FCT=GESSIH;X3PRF=ADMIN' -Png
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$RptPath,
  [Parameter(Mandatory)][string]$Out,
  [Parameter(Mandatory)][string]$Usr,
  [Parameter(Mandatory)][string]$Etat,
  [Parameter(Mandatory)][double]$NumEdt,
  [double]$SeqEdt = 0,
  [string]$Extra = "",
  [string]$Pass = $env:X3_DB_PASS,
  [string]$Dsn = "TEST_TEB211",
  [string]$DbName = "tebx3",
  [string]$Schema = "TEB",
  [string]$User = "sa",
  [switch]$Png,
  [int]$MaxPages = 2,
  [ValidateSet('POR','ENG')][string]$Lan = 'POR'
)
$ErrorActionPreference = 'Stop'
$RptPath = (Resolve-Path $RptPath).Path; $Out = [IO.Path]::GetFullPath($Out)
if ([Environment]::Is64BitProcess) {
  & "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
    -RptPath $RptPath -Out $Out -Usr $Usr -Etat $Etat -NumEdt $NumEdt -SeqEdt $SeqEdt -Extra "`"$Extra`"" -Pass $Pass -Dsn $Dsn -DbName $DbName -Schema $Schema -User $User -Lan $Lan
  if ($Png -and (Test-Path $Out)) { & (Join-Path $PSScriptRoot 'Pdf-ToPng.ps1') -Pdf $Out -MaxPages $MaxPages }
  exit
}
$gac = 'C:\WINDOWS\Microsoft.Net\assembly\GAC_MSIL'
$v = 'v4.0_13.0.4000.0__692fbea5521e1304'
$refs = @('CrystalReports.Engine','Shared','ReportAppServer.ClientDoc','ReportAppServer.DataDefModel',
          'ReportAppServer.ReportDefModel','ReportAppServer.Controllers','ReportAppServer.CommonObjectModel') |
        % { "$gac\CrystalDecisions.$_\$v\CrystalDecisions.$_.dll" }
foreach ($f in 'X3RptStubUfl.cs','X3RptExportFull.cs') {
  Add-Type -TypeDefinition (Get-Content (Join-Path $PSScriptRoot $f) -Raw -Encoding UTF8) -ReferencedAssemblies $refs -Language CSharp
}
# textos reais dos menus locais (TextOfChapter) para a pre-visualizacao
try {
  $sc = New-Object System.Data.SqlClient.SqlConnection "Server=192.168.1.211;Database=$DbName;User Id=$User;Password=$Pass;Connect Timeout=10"
  $sc.Open(); $cmd = $sc.CreateCommand(); $cmd.CommandText = "select LANCHP_0, LANNUM_0, LANMES_0 from X3.APLSTD where LAN_0='$Lan'"
  $rd = $cmd.ExecuteReader(); while ($rd.Read()) { [X3RptStubUfl]::Texts["$($rd[0]):$($rd[1])"] = [string]$rd[2] }; $sc.Close()
  Write-Host "APLSTD: $([X3RptStubUfl]::Texts.Count) textos"
} catch { Write-Host "APLSTD indisponivel: $($_.Exception.Message)" }
# menus locais novos da Lisoaz (ainda nao existem em BD nenhuma) -> registo specs\LZ_menus_6003.json
$reg = Join-Path $PSScriptRoot 'specs\LZ_menus_6003.json'
if (Test-Path $reg) { foreach ($m in (Get-Content $reg -Raw -Encoding UTF8 | ConvertFrom-Json)) { [X3RptStubUfl]::Texts["6003:$($m.num)"] = $(if ($Lan -eq 'ENG') { $m.eng } else { $m.por }) } }
$stub = Join-Path $env:TEMP ("stub_" + [IO.Path]::GetFileName($RptPath))
if (Test-Path $stub) { Remove-Item $stub -Force }
Write-Host ([X3RptStubUfl]::Build($RptPath, $stub))
if (Test-Path $Out) { Remove-Item $Out -Force }
[X3RptExportFull]::Extra = $Extra
$fmt = if ($Out -like '*.pdf') { 'pdf' } else { 'txt' }
Write-Host ([X3RptExportFull]::Export($stub, $Out, $fmt, $Dsn, $DbName, $Schema, $User, $Pass, $Usr, $Etat, $NumEdt, $SeqEdt, 'POR', 'TEBX3'))


