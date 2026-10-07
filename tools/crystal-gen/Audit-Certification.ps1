<#
.SYNOPSIS
  Auditoria de certificacao AT (LEG POR): compara cada LZ_<doc>.rpt com o modelo base Sage da area
  (Reports-BaseX3) e produz um relatorio Markdown:
    A. campos certificados (TMPSRPT.PT*, TMPSRPTDET.*, PORQRC.*, QR, formulas de certificacao) presentes e visiveis
    B. formulas de certificacao (texto) iguais ao modelo
    C. tabelas/ligacoes TMPSRPT / TMPSRPTDET / PORQRC
    D. parametros do relatorio (modelo vs LZ)
    E. condicoes de TMPSRPT* na selecao de registos
  Limite: a supressao CONDICIONAL de objetos (formulas) nao e avaliada -- so a supressao estatica.
#>
[CmdletBinding()]
param([string]$Out = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')) 'Lisoaz\Auditoria_Certificacao_LZ.md'),
      [string]$Work = (Join-Path $env:TEMP 'lz_audit'))
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
New-Item -ItemType Directory -Force $Work | Out-Null
$ps32 = "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe"
$map = [ordered]@{ ORC='Reports-BaseX3\DEVICLIENT2'; ENC='Reports-BaseX3\ARCCLIENT2'; GRC='Reports-BaseX3\BONLIV2'; FAC='Reports-TEB\SBONFACP'
                   REC='Reports-BaseX3\P-RECIBO'; PAG='Reports-BaseX3\P-RECIBO'; ENF='Reports-BaseX3\BONCDE2'; GT='Reports-BaseX3\TRNNTE' }
$certRx = 'TMPSRPT\.PT|TMPSRPTDET\.|PORQRC\.'

function Load($rpt) {
  $n = [IO.Path]::GetFileNameWithoutExtension($rpt)
  $i = Join-Path $Work "$n.txt"; $s = Join-Path $Work "$n.style.txt"
  if (-not (Test-Path $i) -or (Get-Item $i).LastWriteTime -lt (Get-Item $rpt).LastWriteTime) {
    & $ps32 -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Inspect-X3Report.ps1') -RptPath $rpt -OutFile $i | Out-Null
    & $ps32 -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Dump-Style.ps1') -RptPath $rpt -OutFile $s | Out-Null
  }
  $L = Get-Content $i -Encoding UTF8; $S = Get-Content $s -Encoding UTF8
  $subAt = ($L | Select-String '^=== SUBREPORTS' | Select-Object -First 1).LineNumber; if (-not $subAt) { $subAt = $L.Count }
  $main = $L[0..($subAt - 1)]
  # formulas (bloco === FORMULA FIELDS === ate === PARAMETER FIELDS ===)
  $f = [ordered]@{}; $cur = $null; $on = $false
  foreach ($l in $main) {
    if ($l -match '^=== FORMULA FIELDS') { $on = $true; continue }
    if ($l -match '^=== PARAMETER FIELDS') { $on = $false; continue }
    if (-not $on) { continue }
    if ($l -match '^([A-Za-z_0-9 ]+?) = (.*)$' -and $l -notmatch '^\s') { $cur = $matches[1]; $f[$cur] = $matches[2]; continue }
    if ($cur) { $f[$cur] += "`n" + $l }
  }
  # supressao estatica: objeto e seccao (Dump-Style)
  $supp = @{}; $secSupp = $false
  foreach ($l in $S) {
    if ($l -match '^-- \S+ kind=\S+ H=\d+ suppress=(\S+)') { $secSupp = ($matches[1] -eq 'True'); continue }
    if ($l -match '^\s+\w+Object (\S+) .* supp=(\S+)$') { $supp[$matches[1]] = $secSupp -or ($matches[2] -eq 'True') }
  }
  # objetos
  $objs = foreach ($l in $main) {
    if ($l -match '^\s+\[(\w+)\] Name=(\S+) ') {
      $kind = $matches[1]; $nm = $matches[2]; $src = $null; $tx = $null
      if ($l -match ' DataSource=(\S+)') { $src = $matches[1] }
      if ($l -match ' Text=\[(.*)\]') { $tx = $matches[1] }
      [pscustomobject]@{ kind=$kind; name=$nm; src=$src; text=$tx; hidden=[bool]$supp[$nm] }
    }
  }
  [pscustomobject]@{
    formulas = $f; objs = @($objs)
    params = @($main | Select-String '^(\S+)\s+Type=(crFieldValueType\w+)$' | ForEach-Object { $_.Matches[0].Groups[1].Value })
    tables = @($main | Select-String '^\s*Table: Name=(\S+) Alias=(\S+)' | ForEach-Object { $_.Matches[0].Groups[2].Value })
    links  = @($main | Select-String '^Link: .*' | ForEach-Object { $_.Line } | Where-Object { $_ -match 'TMPSRPT|PORQRC' })
    sel    = (($main | Select-String '^=== RECORD SELECTION' -Context 0,1).Context.PostContext -join ' ')
  }
}
function Norm($t) { if ($null -eq $t) { return '' }; (($t -replace '//[^\n]*', '') -replace '\s+', ' ').Trim() }

$md = New-Object Text.StringBuilder
[void]$md.AppendLine('# Auditoria de certificação AT — relatórios LZ (Lisoaz) vs modelos base Sage')
[void]$md.AppendLine('')
[void]$md.AppendLine("Gerado por ``tools/crystal-gen/Audit-Certification.ps1`` em $(Get-Date -Format 'yyyy-MM-dd HH:mm'). Supressão condicional (fórmulas) não avaliada; só a estática.")
[void]$md.AppendLine('')
$summary = @()
foreach ($d in $map.Keys) {
  $bp = Join-Path $root "$($map[$d]).rpt"; $lp = Join-Path $root "Lisoaz\RPTs\LZ_$d.rpt"
  $B = Load $bp; $Z = Load $lp
  $bn = Split-Path $map[$d] -Leaf
  $issues = 0
  [void]$md.AppendLine("## LZ_$d  ↔  $bn"); [void]$md.AppendLine('')
  # formulas de certificacao no modelo
  $certF = @($B.formulas.Keys | Where-Object { $B.formulas[$_] -match $certRx -or $_ -match '^(atcud|PT_)' })
  # A. fontes certificadas usadas em objetos visiveis no modelo
  $keys = @{}
  foreach ($o in $B.objs) {
    $k = $null
    if ($o.kind -eq 'crReportObjectKindBlobField' -and $o.name -match 'IMGQRC') { $k = 'QR (PORQRC.IMGQRC)' }
    elseif ($o.src -and ($o.src -match $certRx -or ($o.src -match '^\{@(.+)\}$' -and $certF -contains $matches[1]))) { $k = $o.src }
    elseif ($o.text) { foreach ($m in [regex]::Matches($o.text, '\{[^}]+\}')) { if ($m.Value -match $certRx -or ($m.Value -match '^\{@(.+)\}$' -and $certF -contains $matches[1])) { $k = $m.Value } } }
    if ($k) { if (-not $keys.ContainsKey($k)) { $keys[$k] = $false }; if (-not $o.hidden) { $keys[$k] = $true } }
  }
  [void]$md.AppendLine('**A. Campos certificados (visível no modelo → no LZ)**'); [void]$md.AppendLine('')
  [void]$md.AppendLine('| Fonte | Modelo | LZ | Estado |'); [void]$md.AppendLine('|---|---|---|---|')
  foreach ($k in ($keys.Keys | Sort-Object)) {
    $zo = @($Z.objs | Where-Object { ($k -like 'QR*' -and $_.kind -eq 'crReportObjectKindBlobField' -and $_.name -match 'IMGQRC') -or $_.src -eq $k -or ($_.text -and $_.text.Contains($k)) })
    $zVis = @($zo | Where-Object { -not $_.hidden }).Count -gt 0
    $bVis = $keys[$k]
    $st = if (-not $zo.Count) { if ($bVis) { 'FALTA'; $issues++ } else { 'falta (oculto no modelo)' } }
          elseif ($bVis -and -not $zVis) { 'OCULTO no LZ'; $issues++ } else { 'ok' }
    [void]$md.AppendLine("| ``$k`` | $(if($bVis){'visível'}else{'oculto'}) | $(if(-not $zo.Count){'—'}elseif($zVis){'visível'}else{'oculto'}) | $st |")
  }
  [void]$md.AppendLine('')
  # B. formulas
  [void]$md.AppendLine('**B. Fórmulas de certificação**'); [void]$md.AppendLine('')
  $fd = @()
  foreach ($n in $certF) {
    if (-not $Z.formulas.Contains($n)) { $fd += "- ``$n``: **não existe no LZ**"; $issues++; continue }
    if ((Norm $B.formulas[$n]) -ne (Norm $Z.formulas[$n])) { $fd += "- ``$n``: difere do modelo`n  - modelo: ``$((Norm $B.formulas[$n]).Substring(0,[Math]::Min(300,(Norm $B.formulas[$n]).Length)))```n  - LZ: ``$((Norm $Z.formulas[$n]).Substring(0,[Math]::Min(300,(Norm $Z.formulas[$n]).Length)))``" }
  }
  if ($fd) { $fd | ForEach-Object { [void]$md.AppendLine($_) } } else { [void]$md.AppendLine("- todas iguais ao modelo ($($certF.Count): $($certF -join ', '))") }
  [void]$md.AppendLine('')
  # C. tabelas / links
  [void]$md.AppendLine('**C. Tabelas e ligações de certificação**'); [void]$md.AppendLine('')
  foreach ($t in @('TMPSRPT','TMPSRPTDET','PORQRC')) {
    $inB = $B.tables -contains $t; $inZ = $Z.tables -contains $t
    if ($inB -or $inZ) { [void]$md.AppendLine("- $t : modelo=$inB LZ=$inZ $(if($inB -and -not $inZ){'**FALTA**'})"); if ($inB -and -not $inZ) { $issues++ } }
  }
  foreach ($l in $B.links) {
    $core = ($l -replace '^Link: ', '')
    $tgt = if ($core -match 'Dst=(\S+)') { $matches[1] } else { '' }
    $zl = @($Z.links | Where-Object { $_ -match "Dst=$tgt " })
    if (-not $zl) { [void]$md.AppendLine("- ligação do modelo sem equivalente no LZ: ``$core``") }
    else {
      foreach ($x in $zl) {
        $zc = $x -replace '^Link: ', ''
        if ($zc -eq $core) { $desc = 'igual ao modelo' } else { $desc = 'modelo `' + $core + '` / LZ `' + $zc + '`' }
        [void]$md.AppendLine('- ' + $tgt + ': ' + $desc)
      }
    }
  }
  [void]$md.AppendLine('')
  # D. parametros
  $pm = @($B.params | Where-Object { $Z.params -notcontains $_ }); $pz = @($Z.params | Where-Object { $B.params -notcontains $_ })
  [void]$md.AppendLine('**D. Parâmetros**'); [void]$md.AppendLine('')
  [void]$md.AppendLine("- modelo: $($B.params.Count), LZ: $($Z.params.Count)")
  if ($pm) { [void]$md.AppendLine("- só no modelo: $($pm -join ', ')") }
  if ($pz) { [void]$md.AppendLine("- só no LZ: $($pz -join ', ')") }
  [void]$md.AppendLine('')
  # E. selecao
  [void]$md.AppendLine('**E. Seleção de registos (condições de certificação)**'); [void]$md.AppendLine('')
  $cb = @([regex]::Matches($B.sel, '\{TMPSRPT\w*\.\w+\}[^()]*') | ForEach-Object { ($_.Value -replace '\s+', ' ').Trim() })
  $cz = @([regex]::Matches($Z.sel, '\{TMPSRPT\w*\.\w+\}[^()]*') | ForEach-Object { ($_.Value -replace '\s+', ' ').Trim() })
  [void]$md.AppendLine("- modelo: $(if($cb){($cb | Sort-Object -Unique) -join ' | '}else{'(sem condições TMPSRPT)'})")
  [void]$md.AppendLine("- LZ: $(if($cz){($cz | Sort-Object -Unique) -join ' | '}else{'(sem condições TMPSRPT)'})")
  [void]$md.AppendLine('')
  $summary += "| LZ_$d | $bn | $($keys.Count) | $issues |"
}
$head = "## Resumo`n`n| Relatório | Modelo Sage | Fontes certificadas verificadas | Problemas (A/B/C) |`n|---|---|---|---|`n" + ($summary -join "`n") + "`n"
$txt = $md.ToString(); $i = $txt.IndexOf('## LZ_')
$txt = $txt.Substring(0, $i) + $head + "`n" + $txt.Substring($i)
[IO.File]::WriteAllText($Out, $txt, (New-Object Text.UTF8Encoding($true)))
Write-Host $head
Write-Host "-> $Out"
