<#
.SYNOPSIS
  Gera os relatorios da Lisoaz (Lisoaz/RPTs/LZ_<doc>.rpt) a partir dos da TEB (Reports-TEB/TEB_<doc>.rpt),
  encadeando os specs: LZ_<doc>_port.json (diferencas de dados TEB->Lisoaz) -> LZ_common.json (menus
  locais custom TEB) -> LZ_<doc>_design.json (layout Lisoaz). Specs em falta sao saltados.
.EXAMPLE
  .\Build-Lisoaz.ps1                 # todos
  .\Build-Lisoaz.ps1 -Docs FAC,GRC
#>
[CmdletBinding()]
param([string[]]$Docs = @('ORC','ENC','GRC','FAC','REC','PAG','ENF','GT'))
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$specs = Join-Path $PSScriptRoot 'specs'
$work = Join-Path $env:TEMP 'lz_build'; New-Item -ItemType Directory -Force $work | Out-Null
foreach ($d in $Docs) {
  $cur = Join-Path $root "Reports-TEB\TEB_$d.rpt"
  $chain = @("LZ_${d}_port.json", "LZ_common.json", "LZ_${d}_texts.json", "LZ_${d}_design.json") | ? { Test-Path (Join-Path $specs $_) }
  $i = 0; $ok = $true
  foreach ($s in $chain) {
    $i++; $next = Join-Path $work "LZ_${d}_$i.rpt"
    $r = & (Join-Path $PSScriptRoot 'Edit-X3Report.ps1') -Rpt $cur -Ops (Join-Path $specs $s) -Out $next 2>&1 | Out-String
    if ($r -cnotmatch 'GRAVADO ->') { Write-Host "[$d] FALHOU em $s`n$r"; $ok = $false; break }
    ($r -split "`r?`n") | ? { $_ -match '^ERR|theme:' } | % { Write-Host "[$d] $s : $_" }
    $cur = $next
  }
  if ($ok) { Copy-Item $cur (Join-Path $root "Lisoaz\RPTs\LZ_$d.rpt") -Force; Write-Host "[$d] OK ($($chain -join ' > '))" }
}

