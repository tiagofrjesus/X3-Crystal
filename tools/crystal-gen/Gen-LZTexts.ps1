<#
.SYNOPSIS
  Converte os textos fixos (PT) visiveis dos relatorios TEB_<doc> em menus locais (capitulo 6003, o menu
  Megavale) para que a Lisoaz traduza no X3 (POR/ENG). Gera:
    specs\LZ_<doc>_texts.json   -> cada Text fixo e trocado por um campo formula com TextOfChapter(...,6003,N)
                                  (mesma posicao/estilo; o Text original e removido)
    specs\LZ_common.json        -> idioma uniforme ({@lzLan}: POR se BPARTNER.LAN_0 = POR, senao ENG)
    specs\LZ_menus_6003.json    -> registo de TODAS as entradas 6003 usadas (POR + proposta ENG) -> Excel
  Os textos com campos certificados (PT*, TMPSRPT/TMPCRPT.PT*) NAO sao convertidos.
  Requer os dumps JSON dos textos (Dump-Texts.ps1) em -TextsDir.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$TextsDir)
$ErrorActionPreference = 'Stop'
$specs = Join-Path $PSScriptRoot 'specs'
$CH = 6003
$TOC = { param($n) "TextOfChapter({?X3DOS}, {@lzLan}, $CH, $n)" }
# nos subreports: pasta e idioma chegam por variaveis partilhadas publicadas pelo principal (formula textofchapter)
$TOCSUB = { param($n) "TextOfChapter(lzDos, lzLanS, $CH, $n)" }
$SUBHDR = 'WhilePrintingRecords; shared stringVar lzDos; shared stringVar lzLanS; '

# ---- entradas 6003 ja existentes na TEB (reutilizadas) com proposta POR/ENG para a Lisoaz ----
$menu = [ordered]@{}
function AddM($n, $por, $eng, $orig) { $menu["$n"] = [ordered]@{ num = $n; por = $por; eng = $eng; origem = $orig; usado = @() } }
AddM 59  'Documento de utilização puramente interna' 'For internal use only' 'TEB'
AddM 85  'Desc. Cabeçalho' 'Header discount' 'TEB'
AddM 86  'Desconto Linhas' 'Line discount' 'TEB'
AddM 90  'Exmo.(s) Senhor(es)' 'Dear Sir/Madam' 'TEB'
AddM 91  'V/Número:' 'Your ref.:' 'TEB'
AddM 92  'Emitido por:' 'Issued by:' 'TEB'
AddM 93  'Nº Contribuinte' 'VAT No.' 'TEB'
AddM 94  'Modo/Cond. Pagamento' 'Payment terms' 'TEB'
AddM 95  'IVA' 'VAT' 'TEB'
AddM 96  'Transporte' 'Transport' 'TEB'
AddM 97  'Descontos' 'Discounts' 'TEB'
AddM 98  'Este documento não constitui documento de transporte, nos termos do Decreto-Lei nº 147/2003' 'This document is not a transport document under Decree-Law no. 147/2003' 'TEB'
AddM 99  '- Sociedade por quotas - Cons. Reg. Com. de Oliveira de Azeméis, matrícula nº 2272' '- Private limited company - Commercial Registry of Oliveira de Azeméis, no. 2272' 'TEB (texto adaptado à Lisoaz)'
AddM 101 'Comercial:' 'Sales rep.:' 'TEB'

# ---- textos novos: chave normalizada (minusculas, sem ':' final) -> POR canonico, ENG ----
$dict = [ordered]@{
  'nif'                         = @('NIF', 'VAT No.')
  'declaro que separei os artigos constantes neste documento' = @('Declaro que separei os artigos constantes neste documento', 'I declare that I have picked the items listed in this document')
  'declaro que conferi os artigos constantes neste documento' = @('Declaro que conferi os artigos constantes neste documento', 'I declare that I have checked the items listed in this document')
  'declaro que recebi e conferi os artigos constantes neste documento' = @('Declaro que recebi e conferi os artigos constantes neste documento', 'I declare that I have received and checked the items listed in this document')
  'data'                        = @('Data', 'Date')
  'assinatura'                  = @('Assinatura', 'Signature')
  'assinatura (motorista)'      = @('Assinatura (Motorista)', 'Signature (Driver)')
  'data carga'                  = @('Data Carga', 'Loading date')
  'data/hora carga'             = @('Data/Hora Carga', 'Loading date/time')
  'hora carga'                  = @('Hora Carga', 'Loading time')
  'descarga'                    = @('Descarga', 'Unloading')
  'matricula'                   = @('Matrícula', 'Licence plate')
  'matricula reboque'           = @('Matrícula reboque', 'Trailer plate')
  'os artigos e/ou serviços constantes deste documento foram colocados à disposição do adquirente na mesma, ou na data da guia remessa' = @('Os artigos e/ou serviços constantes deste documento foram colocados à disposição do adquirente na mesma, ou na data da Guia Remessa', 'The goods and/or services in this document were made available to the buyer on its date or on the date of the delivery note')
  '«chamada para rede fixa nacional»' = @('«Chamada para rede fixa nacional»', '«Call to a national landline»')
  'artigo'                      = @('Artigo', 'Item')
  'qtd.'                        = @('Qtd.', 'Qty.')
  'quantidade'                  = @('Quantidade', 'Quantity')
  'preço'                       = @('Preço', 'Price')
  'p.u. s/iva'                  = @('P.U. s/IVA', 'Unit price excl. VAT')
  'exped. nr.'                  = @('Exped. nr.', 'Delivery no.')
  'expedida em'                 = @('Expedida em', 'Shipped on')
  'fat. origem'                 = @('Fat. origem', 'Original invoice')
  'recibo'                      = @('Recibo', 'Receipt')
  'tipo de pag.'                = @('Tipo de Pag.', 'Payment type')
  'banco'                       = @('Banco', 'Bank')
  'exmos. senhores,'            = @('Exmos. Senhores,', 'Dear Sirs,')
  'recebemos para liquidação do(s) documento(s) abaixo mencionados no valor de' = @('Recebemos para liquidação do(s) documento(s) abaixo mencionados no valor de', 'We have received, in settlement of the document(s) listed below, the amount of')
  'vimos por este meio informar que procedemos à liquidação dos documentos abaixo mencionados' = @('Vimos por este meio informar que procedemos à liquidação dos documentos abaixo mencionados', 'We hereby inform you that we have settled the documents listed below')
  'descrição'                   = @('Descrição', 'Description')
  'valor documento'             = @('Valor Documento', 'Document amount')
  'nº do documento'             = @('Nº do Documento', 'Document no.')
  'divisa'                      = @('Divisa', 'Currency')
  'valor pendente'              = @('Valor Pendente', 'Outstanding')
  'valor pago'                  = @('Valor Pago', 'Amount paid')
  'data do doc.'                = @('Data do Doc.', 'Document date')
  'data de doc.'                = @('Data de Doc.', 'Document date')
  'total liquidado em'          = @('Total liquidado em', 'Total settled in')
  'total descontos em'          = @('Total descontos em', 'Total discounts in')
  'valor recebido em'           = @('Valor recebido em', 'Amount received in')
  'total de retenções em'       = @('Total de retenções em', 'Total withholdings in')
  'totais'                      = @('TOTAIS', 'TOTALS')
  'para efeitos de iva, solicitamos a devolução deste(s) documento(s) devidamente assinado' = @('Para efeitos de IVA, solicitamos a devolução deste(s) documento(s) devidamente assinado', 'For VAT purposes, please return this document duly signed')
  'a gerência'                  = @('A Gerência', 'The Management')
  'original'                    = @('Original', 'Original')
  'código fornecedor'           = @('Código fornecedor', 'Supplier code')
  'valor liquidado'             = @('Valor Liquidado', 'Amount settled')
  'vosso documento'             = @('Vosso Documento', 'Your document')
  'notificação de pagamento'    = @('Notificação de Pagamento', 'Payment notification')
  'documento processado por computador' = @('Documento processado por computador', 'Computer-processed document')
  'guia de transporte'          = @('Guia de Transporte', 'Transport note')
  'cliente'                     = @('Cliente', 'Customer')
  'agradecemos que nos comprovem a recepção da nota de crédito através da devolução do respetivo duplicado carimbado e assinado, nº 5 do art.º 78º do civa' = @('Agradecemos que nos comprovem a recepção da Nota de Crédito através da devolução do respetivo duplicado carimbado e assinado, nº 5 do art.º 78º do CIVA', 'Please confirm receipt of this Credit Note by returning the duplicate stamped and signed (art. 78(5) of the Portuguese VAT Code)')
  'total iva - valores em eur'  = @('TOTAL IVA - Valores em EUR', 'VAT TOTAL - Amounts in EUR')
  'taxa'                        = @('Taxa', 'Rate')
  'base'                        = @('Base', 'Base')
  'mt. taxa'                    = @('Mt. taxa', 'Tax amt.')
  'total (eur)'                 = @('Total (EUR)', 'Total (EUR)')
  'base (eur)'                  = @('Base (EUR)', 'Base (EUR)')
  'mt. taxa (eur)'              = @('Mt. taxa (EUR)', 'Tax amt. (EUR)')
  'incidência'                  = @('Incidência', 'Taxable amount')
  'imposto'                     = @('Imposto', 'Tax')
  'nipc'                        = @('NIPC', 'Company reg. no.')
  'contactos'                   = @('Zona Industrial das Lameiradas, Lote Nº 5 • 4540-423 Mansores - Arouca  •  lisoaz@lisoaz.com  •  www.lisoaz.com  •  Tel. (+351) 256 920 030 (chamada para rede fixa nacional)',
                                    'Zona Industrial das Lameiradas, Lote Nº 5 • 4540-423 Mansores - Arouca  •  lisoaz@lisoaz.com  •  www.lisoaz.com  •  Tel. (+351) 256 920 030 (call to a national landline)')
}
# aliases para entradas TEB existentes (a entrada ja inclui ':' quando aplicavel)
$alias = @{ 'emitido por' = 92; 'n. contribuinte' = 93; 'nº contribuinte' = 93; 'descontos' = 97 }
$next = 102
$byKey = @{}
foreach ($k in $dict.Keys) { AddM $next $dict[$k][0] $dict[$k][1] 'Lisoaz (novo)'; $byKey[$k] = $next; $next++ }

# APLSTD.LANMES e A(123): textos maiores sao divididos (num espaco) numa entrada de continuacao, concatenada com " "
$MAXLEN = 123
function Split2($s) {
  if ($s.Length -le 40) { return @($s, '') }   # chamado so quando POR ou ENG excede: divide sempre as duas linguas
  $i = $s.LastIndexOf(' ', [Math]::Min($MAXLEN, [int]($s.Length / 2) + 20))
  if ($i -lt 1) { $i = $s.LastIndexOf(' ', $MAXLEN) }
  @($s.Substring(0, $i).TrimEnd(), $s.Substring($i + 1).TrimStart())
}
$cont = @{}
foreach ($n in @($menu.Keys)) {
  $m = $menu[$n]
  if ($m.por.Length -le $MAXLEN -and $m.eng.Length -le $MAXLEN) { continue }
  $p = Split2 $m.por; $e = Split2 $m.eng
  $m.por = $p[0]; $m.eng = $e[0]; $m.cont = $next
  AddM $next $p[1] $e[1] "continuação de $n"
  $cont[[int]$n] = $next; $next++
}
function TocExpr($toc, $n) { if ($cont.ContainsKey([int]$n)) { (& $toc $n) + ' + " " + ' + (& $toc $cont[[int]$n]) } else { & $toc $n } }

function Norm($s) {
  $s = ($s -replace '\s+', ' ').Trim().ToLower()
  $s = $s -replace '^os artigos e/ou serviços os artigos e/ou serviços', 'os artigos e/ou serviços'   # erro de texto no TEB
  $s = $s -replace 'matrícula', 'matricula'
  $s
}
function Num($core, $where) {
  $k = Norm $core
  $kk = $k.TrimEnd(':').TrimEnd()
  if ($alias.ContainsKey($kk)) { $n = $alias[$kk] }
  elseif ($byKey.ContainsKey($k)) { $n = $byKey[$k] }
  elseif ($byKey.ContainsKey($kk)) { $n = $byKey[$kk] }
  else { throw "Sem entrada de menu para: '$core' ($where)" }
  if ($menu["$n"].usado -notcontains $where) { $menu["$n"].usado += $where }
  $n
}
function Lit($s) { '"' + ($s -replace '"', '""') + '"' }
function FieldExpr($f) { if ($f -match 'DAT_0\}$') { "ToText($f, ""dd-MM-yyyy"")" } else { $f } }

# ---- conversao de um texto -> expressao Crystal ----
function Convert-Text($text, $where, $toc = $TOC) {
  $lines = $text -split "`r?`n"
  $exprs = foreach ($line in $lines) {
    $parts = [regex]::Split($line, '(\{[^}]+\})') | Where-Object { $_ -ne '' }
    $e = foreach ($p in $parts) {
      if ($p -match '^\{.+\}$') { FieldExpr $p; continue }
      if ($p -notmatch '[A-Za-zÀ-ú]') { Lit $p; continue }
      $m = [regex]::Match($p, '^(\s*)(.*?)(\s*:?\s*)$')
      $lead = $m.Groups[1].Value; $core = $m.Groups[2].Value; $trail = $m.Groups[3].Value
      $kk = (Norm $core).TrimEnd(':').TrimEnd()
      $n = Num $core $where
      if ($alias.ContainsKey($kk) -and $menu["$n"].por.EndsWith(':')) { $trail = $trail -replace ':', '' }   # o ':' ja vem do menu
      $x = @(); if ($lead) { $x += Lit $lead }; $x += (TocExpr $toc $n); if ($trail) { $x += Lit $trail }
      $x -join ' + '
    }
    if (-not $e) { '""' } else { @($e) -join ' + ' }
  }
  @($exprs) -join ' + Chr(13) + '
}

$removedByPort = @{ ENC = @('Text50'); GRC = @('Text3') }
foreach ($d in 'ORC','ENC','GRC','FAC','REC','PAG','ENF','GT') {
  $j = Get-Content (Join-Path $TextsDir "TEB_$d.json") -Raw -Encoding UTF8 | ConvertFrom-Json
  $hasSub = $false
  $ops = @([ordered]@{ comment = "GERADO por Gen-LZTexts.ps1 -- textos fixos -> menu local $CH (TextOfChapter, idioma {@lzLan}). Nao editar a mao." })
  foreach ($t in $j) {
    if ($t.hidden) { continue }
    if ((($t.text -replace '\{[^}]*\}', '')) -notmatch '[A-Za-zÀ-ú]{2,}') { continue }
    if ($removedByPort[$d] -contains $t.name) { continue }
    if ($t.name -like 'PT*' -or $t.text -match 'TMP[SC]RPT\w*\.PT') { Write-Host "[$d] $($t.name) mantido (contem campo certificado): $($t.text)"; continue }
    $where = "LZ_$d.$($t.name)"
    if ($t.sub) { $where = "LZ_$d[$($t.sub)].$($t.name)"; $expr = $SUBHDR + (Convert-Text $t.text $where $TOCSUB) }
    else { $expr = Convert-Text $t.text $where }
    $fn = "lzT_$($t.name)"
    $fo = [ordered]@{ op = 'formula'; name = $fn; text = $expr }
    if ($t.sub) { $fo.sub = $t.sub }
    $ops += $fo
    $f = [ordered]@{ op = 'field'; section = $t.section; name = "$($t.name)L"; source = "{@$fn}"; type = 'string'; l = $t.l; t = $t.t; w = $t.w; h = $t.h
                     font = $t.font; size = $t.size; bold = $t.bold; italic = $t.italic; underline = $t.underline; color = $t.color }
    switch -Wildcard ($t.align) { '*Right*' { $f.align = 'right' } '*Center*' { $f.align = 'center' } default { $f.align = 'left' } }
    $f.copyFormatFrom = $t.name          # herda supressao/condicoes do Text original
    $rm = [ordered]@{ op = 'remove'; name = $t.name }
    if ($t.sub) { $f.sub = $t.sub; $rm.sub = $t.sub; $hasSub = $true }
    $ops += $f
    $ops += $rm
  }
  if ($d -eq 'ENC') {
    $n = Num 'NIPC' 'LZ_ENC.lbl_formejuridique'
    $ops += [ordered]@{ op = 'formulaReplace'; mainOnly = $true; required = $true; find = '" - Mat\. Cons\. Comercial: Nr\. "'; replace = '" - " + ' + (TocExpr $TOC $n) + ' + " "' }
  }
  if ($d -eq 'FAC') {   # rotulos do quadro de IVA (formulas com literal no subreport)
    foreach ($x in @(@('lbl_taux', 'Taxa'), @('lbl_baseTVA', 'Incidência'), @('lbl_mtTVA', 'Imposto'))) {
      $n = Num $x[1] "LZ_FAC[SINVOICEV_TAXE.rpt - 01].@$($x[0])"
      $ops += [ordered]@{ op = 'formula'; sub = 'SINVOICEV_TAXE.rpt - 01'; name = $x[0]; text = $SUBHDR + (TocExpr $TOCSUB $n) }
    }
    $hasSub = $true
  }
  if ($hasSub) {   # o principal publica pasta/idioma para os subreports (formula textofchapter, ja avaliada no cabecalho)
    $pub = [ordered]@{ op = 'formulaReplace'; mainOnly = $true; required = $true; formula = 'textofchapter'; find = '^\s*WhilePrintingRecords\s*;'
                       replace = 'WhilePrintingRecords; shared stringVar lzDos := {?X3DOS}; shared stringVar lzLanS := {@lzLan};' }
    $ops = @($ops[0], $pub) + $ops[1..($ops.Count - 1)]
  }
  $sp = Join-Path $specs "LZ_${d}_texts.json"
  if ($ops.Count -le 1) { if (Test-Path $sp) { Remove-Item $sp }; Write-Host "[$d] sem textos fixos"; continue }
  ConvertTo-Json -InputObject @($ops) -Depth 5 | Out-File $sp -Encoding utf8
  Write-Host "[$d] $([int](($ops.Count - 1) / 3)) texto(s) convertidos"
}
# contactos (formula do design) -> registo de uso
$nC = $byKey['contactos']; foreach ($d in 'ORC','ENC','GRC','FAC','REC','PAG','ENF','GT') { $menu["$nC"].usado += "LZ_$d.lzContactos" }

# ---- spec comum: idioma uniforme (o capitulo 6003 fica em TextOfChapter) ----
$common = @(
  [ordered]@{ comment = 'GERADO por Gen-LZTexts.ps1. Idioma do documento = idioma do terceiro: POR se BPARTNER.LAN_0 = POR, senao ENG (formula lzLan). Aplica-se a lang_trad/P_lang_trad e as chamadas TextOfChapter/X3TranslatedText que usavam {?X3LAN} ou {BPARTNER.LAN_0}. So o relatorio principal; formulas PT_*/isPt*/atcud* nunca sao alteradas.' },
  [ordered]@{ op = 'formula'; name = 'lzLan'; text = 'if {BPARTNER.LAN_0} = "POR" then "POR" else "ENG"' },
  [ordered]@{ op = 'formulaReplace'; mainOnly = $true; find = '\b((?:P_)?lang_trad)\s*:=\s*[^;]+;'; replace = '$1 := {@lzLan};' },
  [ordered]@{ op = 'formulaReplace'; mainOnly = $true; find = '((?:TextOfChapter|X3TranslatedText)\s*\(\s*[^,()]+,\s*)(\{\?X3LAN\}|\{BPARTNER\.LAN_0\})(\s*,)'; replace = '$1{@lzLan}$3' }
)
$common | ConvertTo-Json -Depth 5 | Out-File (Join-Path $specs 'LZ_common.json') -Encoding utf8
$menu.Values | ConvertTo-Json -Depth 5 | Out-File (Join-Path $specs 'LZ_menus_6003.json') -Encoding utf8
Write-Host "menu $CH : $($menu.Count) entradas ($(@($menu.Values | Where-Object { $_.origem -like 'Lisoaz*' }).Count) novas, a partir de 102)"
