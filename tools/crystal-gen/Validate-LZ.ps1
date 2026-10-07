<#
.SYNOPSIS
  Validacao estrutural LZ_<doc>.rpt vs TEB_<doc>.rpt (fonte): campos certificados (PT*, TMPSRPT.PT*, ATCUD,
  QR) com posicao/fonte iguais; objetos removidos/adicionados; tabelas; parametros (devem ser iguais --
  o AREPORTD e copiado do TEB); credenciais gravadas. Usa Inspect-X3Report.ps1 (32-bit).
#>
[CmdletBinding()]
param([string[]]$Docs = @('ORC','ENC','GRC','FAC','REC','PAG','ENF','GT'), [string]$Work = (Join-Path $env:TEMP 'lz_validate'))
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
New-Item -ItemType Directory -Force $Work | Out-Null
$ps32 = "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe"
function Dump($rpt, $out) { & $ps32 -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Inspect-X3Report.ps1') -RptPath $rpt -OutFile $out | Out-Null; Get-Content $out -Encoding UTF8 }
function Objs($lines) {
  $h = @{}; $sec = ''
  $sub = ($lines | Select-String '^=== SUBREPORTS' | Select-Object -First 1).LineNumber
  if (-not $sub) { $sub = $lines.Count }
  foreach ($l in $lines[0..($sub - 1)]) {
    if ($l -match '^-- Section \S+ / (\S+)') { $sec = $matches[1]; continue }
    if ($l -match '^\s+\[(\w+)\] Name=(\S+) L=(\d+) T=(\d+) W=(\d+) H=(\d+)(.*)$') {
      $h[$matches[2]] = [pscustomobject]@{ kind=$matches[1]; sec=$sec; pos="$($matches[3]),$($matches[4]) $($matches[5])x$($matches[6])"; rest=$matches[7].Trim() }
    }
  }
  $h
}
function Params($lines) { ($lines | Select-String '^(\S+)\s+Type=(crFieldValueType\w+)$' | ForEach-Object { $_.Line.Trim() }) }
function Tables($lines) { ($lines | Select-String '^\s*Table: Name=(\S+) Alias=(\S+)' | ForEach-Object { $_.Matches[0].Groups[2].Value }) | Sort-Object -Unique }
$fail = 0
foreach ($d in $Docs) {
  $teb = Dump (Join-Path $root "Reports-TEB\TEB_$d.rpt") (Join-Path $Work "TEB_$d.txt")
  $lzp = Join-Path $root "Lisoaz\RPTs\LZ_$d.rpt"
  $lz = Dump $lzp (Join-Path $Work "LZ_$d.txt")
  $a = Objs $teb; $b = Objs $lz; $msg = @()
  foreach ($n in $a.Keys) {
    $o = $a[$n]
    $cert = $n -like 'PT*' -or $o.rest -match 'TMPSRPT\.PT|atcud' -or $n -match '^(IMGQRC|atcud)' -or $o.kind -eq 'crReportObjectKindBlobField'
    if (-not $b.ContainsKey($n)) { $msg += "  removido: $n ($($o.sec))" + $(if ($cert) { '  <-- CERTIFICADO!' }); if ($cert) { $fail++ }; continue }
    if ($cert -and ($b[$n].pos -ne $o.pos -or $b[$n].rest -ne $o.rest -or $b[$n].sec -ne $o.sec)) { $msg += "  CERTIFICADO ALTERADO: $n $($o.pos) -> $($b[$n].pos)"; $fail++ }
  }
  $added = @($b.Keys | Where-Object { -not $a.ContainsKey($_) } | Sort-Object)
  $pa = Params $teb; $pb = Params $lz
  $pd = Compare-Object @($pa) @($pb)
  if ($pd) { $msg += "  PARAMETROS DIFERENTES: " + (($pd | ForEach-Object { "$($_.SideIndicator)$($_.InputObject)" }) -join '; '); $fail++ }
  $td = Compare-Object @(Tables $teb) @(Tables $lz)
  if ($td) { $msg += "  tabelas: " + (($td | ForEach-Object { "$($_.SideIndicator)$($_.InputObject)" }) -join ' ') }
  # seccoes com campos certificados: parametros da seccao + visibilidade (estatica/condicional) dos objetos PT iguais
  $sf = Join-Path $PSScriptRoot 'Dump-SectionFormulas.ps1'
  $ca = @(& $ps32 -NoProfile -ExecutionPolicy Bypass -File $sf -Rpt (Join-Path $root "Reports-TEB\TEB_$d.rpt") -Cert)
  $cb = @(& $ps32 -NoProfile -ExecutionPolicy Bypass -File $sf -Rpt $lzp -Cert)
  $cd = Compare-Object $ca $cb
  if ($cd) { $msg += '  SECCOES/VISIBILIDADE CERTIFICADAS ALTERADAS:'; $cd | ForEach-Object { $msg += "    $($_.SideIndicator) $($_.InputObject)" }; $fail++ }
  else { $msg += "  seccoes certificadas: $(@($ca | Where-Object { $_ -match '^\S' }).Count) iguais (altura, supressao, underlay, condicoes, visibilidade PT)" }
  $raw = [IO.File]::ReadAllText($lzp, [Text.Encoding]::GetEncoding(1252))
  if ($raw -match 'sage\.2022|PWD=|<password>') { $msg += '  CREDENCIAIS GRAVADAS!'; $fail++ }
  $nCert = @($a.Keys | Where-Object { $_ -like 'PT*' -or $a[$_].rest -match 'TMPSRPT\.PT' }).Count
  Write-Host "[$d] objetos $($a.Count) -> $($b.Count); certificados verificados: $nCert; parametros: $($pb.Count); adicionados: $($added -join ',')"
  $msg | ForEach-Object { Write-Host $_ }
}
if ($fail) { Write-Host "FALHAS: $fail"; exit 1 } else { Write-Host 'VALIDACAO OK' }
