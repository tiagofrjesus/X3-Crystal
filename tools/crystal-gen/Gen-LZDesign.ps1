<#
.SYNOPSIS
  Gera os specs specs\LZ_<doc>_design.json (layout Lisoaz) a partir de UM conjunto de tokens comum,
  para que todos os documentos fiquem uniformes. Alterar aqui as cores/textos e voltar a correr,
  depois Build-Lisoaz.ps1.
  Regras: nunca mexer em campos certificados (PT*, ATCUD, QR) -- so cor/negrito de rotulos/valores,
  faixas de fundo (atras dos objetos) e o rodape de contactos.
#>
$ErrorActionPreference = 'Stop'
$specs = Join-Path $PSScriptRoot 'specs'

# ---------------- tokens Lisoaz ----------------
$BLUE  = '#1F5FA8'   # cor de marca (rotulos de colunas, tipo de documento, contactos)
$LIGHT = '#E7E6E6'   # faixa da linha de rotulos da grelha / barra de colunas
$DARK  = '#D0CECE'   # caixa de titulo do documento / linha do total
$EDGECOL = '#7F7F7F'   # contorno da barra de colunas
$NCONT = ((Get-Content (Join-Path $specs 'LZ_menus_6003.json') -Raw -Encoding UTF8 | ConvertFrom-Json) | Where-Object { $_.por -like 'Zona Industrial*' }).num   # texto no menu local 6003
$NCONT2 = ((Get-Content (Join-Path $specs 'LZ_menus_6003.json') -Raw -Encoding UTF8 | ConvertFrom-Json) | Where-Object { $_.num -eq $NCONT }).cont
$CONTEXPR = "TextOfChapter({?X3DOS}, {@lzLan}, 6003, $NCONT)" + $(if ($NCONT2) { " + "" "" + TextOfChapter({?X3DOS}, {@lzLan}, 6003, $NCONT2)" } else { '' })
$CONTACTOS_OLD = 'Zona Industrial das Lameiradas, Lote Nº 5 • 4540-423 Mansores - Arouca  •  lisoaz@lisoaz.com  •  www.lisoaz.com  •  Tel. (+351) 256 920 030 (chamada para rede fixa nacional)'

function Band($sec, $name, $l, $t, $w, $h, $bg, [switch]$Edge) {
  $o = [ordered]@{ op='box'; section=$sec; name=$name; l=$l; t=$t; w=$w; h=$h; bg=$bg; back=$true }
  if ($Edge) { $o.border = 'box'; $o.bordercolor = $EDGECOL } else { $o.border = 'none' }
  $o
}
function SetO($name, [hashtable]$p) { $o = [ordered]@{ op='set'; name=$name }; foreach ($k in $p.Keys) { $o[$k] = $p[$k] }; $o }

# d = definicao por documento:
#  grid    : @(seccao, L, T, W, Hrotulos, Htotal, Ldoc)  -> faixa clara na linha de rotulos (L..Ldoc) + celula escura do documento (Ldoc..L+W)
#  docLbl  : rotulo do tipo de documento (azul negrito);  docNum: numero do documento (negrito 9)
#  vals    : valores da grelha a negrito;  blueLbl: rotulos de colunas que nao sao apanhados pelo tema
#  colBar  : @(seccao, L, T, W, H) barra de colunas;  total: @(seccao, L, T, W, H) linha do total
#  footer  : @(seccao, alturaNova, Tcampo)
$docs = [ordered]@{
  GRC = @{ grid=@('Section14',150,360,10590,285,600,7860); docLbl='lblpiece1'; docNum='typenumpiece1'; vals=@('BPCORD01','SHIDAT01','txtppayment1')
           colBar=@('GroupHeaderSection7',135,45,10620,290); total=@('PageFooterSection3',6960,935,3780,290); footer=@('PageFooterSection10',290,40) }
  FAC = @{ grid=@('GroupHeaderSection1',165,360,11175,300,585,8160); docLbl='lbltypefac1'; docNum='piece2'; vals=@('clientCode1','ACCDAT02','txtppayment1')
           blueLbl=@('Texte34L','Texte35L','Texte36','Texte37L','gcolrem01','gcolrem11','gcolrem21','Champ51')
           colBar=@('GroupHeaderSection2',150,20,11205,250); fixH=@(,@('GroupHeaderSection2',315)); total=@('PageFooterSection2',7635,1000,3705,265); footer=@('PageFooterSection4',1400,1160) }
  ORC = @{ grid=@('PageHeaderSection6',315,435,10410,300,645,8325); docLbl='lblpiece1'; docNum='piece1'; vals=@('BPCORD01','quodat1','txtppayment1','EECNUM02')
           colBar=@('Section15',300,60,10440,290); total=@('PageFooterSection9',7215,1105,3510,255); footer=@('PageFooterSection3',1130,885) }
  ENC = @{ docLbl='Text17'; blueLbl=@('lblclient1')
           colBar=@('Section17',56,60,10805,300); total=@('PageFooterSection8',6874,128,4099,290); footer=@('PageFooterSection12',1715,1480) }
  ENF = @{ grid=@('Section12',255,450,10470,315,585,7680); docLbl='lblpiece1'; docNum='typenumpiece1'; vals=@('EECNUM01')
           blueLbl=@('Text1L')
           colBar=@('GroupHeaderSection10',60,45,10770,285); total=@('PageFooterSection5',6990,225,3690,260); footer=@('PageFooterSection7',965,720) }
  GT  = @{ grid=@('GroupHeaderSection7',315,390,10470,300,585,7845); docLbl='Text24L'; docNum='Number1'; vals=@('BPRNUM03','EECNUM04','TRNDAT03')
           colBar=@('GroupHeaderSection2',300,30,10740,265); footer=@('PageFooterSection1',1290,1050) }
  REC = @{ grid=@('GroupHeaderSection1',240,330,10275,300,645,8380); docLbl='Text1L'; docNum='numRecibo1'; vals=@('codCliente1','eecnum1','ACCDAT01','Text10','Text12')
           colBar=@('GroupHeaderSection8',225,1560,10305,285); total=@('GroupFooterSection3',5775,1300,4740,290); footer=@('PageFooterSection1',1270,1030) }
  PAG = @{ grid=@('GroupHeaderSection1',150,585,10275,300,645,7500); docLbl='Text24L'; docNum='NUM01'; vals=@('BPR01','EECNUM01','ACCDAT01')
           colBar=@('GroupHeaderSection1',255,2640,10110,280); total=@('GroupFooterSection3',4710,1030,5647,270); footer=@('PageFooterSection1',1395,1150) }
}

foreach ($d in $docs.Keys) {
  $c = $docs[$d]
  $ops = @(
    [ordered]@{ comment = "GERADO por Gen-LZDesign.ps1 -- nao editar a mao. Design Lisoaz uniforme: azul $BLUE, faixas $LIGHT/$DARK; campos certificados PT*/ATCUD/QR intocados." },
    [ordered]@{ op='theme'; map=@{ '#330035'=$BLUE; '#000400'=$BLUE }; bold=$true }
  )
  if ($c.grid) {
    $s, $l, $t, $w, $hl, $ht, $ld = $c.grid
    $ops += Band $s 'lzGridLbl' $l $t ($ld - $l) $hl $LIGHT
    $ops += Band $s 'lzGridDoc' $ld $t ($l + $w - $ld) $ht $DARK
  }
  if ($c.docLbl) { $ops += SetO $c.docLbl @{ bold=$true; color=$BLUE } }
  if ($c.docNum) { $ops += SetO $c.docNum @{ bold=$true; size=9 } }
  foreach ($v in @($c.vals)) { if ($v) { $ops += SetO $v @{ bold=$true } } }
  foreach ($v in @($c.blueLbl)) { if ($v) { $ops += SetO $v @{ bold=$true; color=$BLUE } } }
  if ($c.colBar) { $s, $l, $t, $w, $h = $c.colBar; $ops += Band $s 'lzColBar' $l $t $w $h $LIGHT -Edge }
  if ($c.total)  { $s, $l, $t, $w, $h = $c.total;  $ops += Band $s 'lzTotBand' $l $t $w $h $DARK }
  if ($c.footer) {
    $s, $hNew, $tF = $c.footer
    $ops += [ordered]@{ op='formula'; name='lzContactos'; text=$CONTEXPR }
    $ops += [ordered]@{ op='sectionHeight'; section=$s; h=$hNew }
    $ops += [ordered]@{ op='field'; section=$s; name='lzContactosF'; source='{@lzContactos}'; type='string'; l=135; t=$tF; w=10620; h=220; font='Arial'; size=7; color=$BLUE; align='center' }
  }
  foreach ($fx in @($c.fixH)) { if ($fx) { $ops += [ordered]@{ op='sectionHeight'; section=$fx[0]; h=$fx[1]; comment='repor altura original (seccao com campo certificado)' } } }
  $ops | ConvertTo-Json -Depth 5 | Out-File (Join-Path $specs "LZ_${d}_design.json") -Encoding utf8
  Write-Host "LZ_${d}_design.json: $($ops.Count) ops"
}
