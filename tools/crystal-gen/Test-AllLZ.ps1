<#
.SYNOPSIS
  Renderiza (PDF + PNG) todos os LZ_*.rpt com dados reais da BD TEB (tebx3) -- a Lisoaz ainda nao tem
  movimentos. Usa pedidos de impressao existentes em TEB.AREPORTM; para a ENC (sem pedidos) cria uma
  copia de teste com a ligacao AREPORTM invertida e selecao direta pela encomenda.
.EXAMPLE
  .\Test-AllLZ.ps1 -OutDir C:\temp\render -Docs FAC,GRC
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutDir, [string[]]$Docs = @('ORC','ENC','GRC','FAC','REC','PAG','ENF','GT'), [int]$MaxPages = 2, [ValidateSet('POR','ENG')][string]$Lan = 'POR')
$ErrorActionPreference = 'Stop'
if (-not $env:X3_DB_PASS) { throw "Definir `$env:X3_DB_PASS (password sa da BD TEB, ver .env)" }
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
New-Item -ItemType Directory -Force $OutDir | Out-Null
# pedidos de impressao de referencia (TEB.AREPORTM, 2026-10-07)
$cfg = @{
  ORC = @{ Usr='TBLR';  Etat='TEB_ORC'; NumEdt=523842; Extra='X3FCT=GESSQH;X3PRF=ADMIN;typedeb=;typefin=ZZZ;devttc=1;logosoc=true' }
  GRC = @{ Usr='TBFS';  Etat='TEB_GRC'; NumEdt=525402; Extra='X3FCT=GESSDH;X3PRF=ADMIN' }
  FAC = @{ Usr='ADMIN'; Etat='TEB_FAC'; NumEdt=525088; Extra='X3FCT=GESSIH;X3PRF=ADMIN;logosoc=true' }
  REC = @{ Usr='TBFS';  Etat='TEB_REC'; NumEdt=523413; Extra='' }
  PAG = @{ Usr='ADMIN'; Etat='TEB_PAG'; NumEdt=0;      Extra='numdeb=PCNF-26E01/01313;numfin=PCNF-26E01/01313' }
  ENF = @{ Usr='TBJC';  Etat='TEB_ENF'; NumEdt=523419; Extra='X3FCT=GESPOH;X3PRF=ADMIN;logosoc=true' }
  GT  = @{ Usr='TBJC';  Etat='TEB_GT';  NumEdt=523200; Extra='bprdeb=;bprfin=ZZZZZZZZZZ;sitedeb=;sitefin=ZZZZZ;tnhnumdeb=;tnhnumfin=ZZZZZZZZZZZZZZZZZZZZ' }
  ENC = @{ Usr='ADMIN'; Etat='X';       NumEdt=0;      Extra='X3FCT=GESSOH;X3PRF=ADMIN'; Order='ENT-E0126/003558' }
}
foreach ($d in $Docs) {
  $c = $cfg[$d]; $rpt = Join-Path $root "Lisoaz\RPTs\LZ_$d.rpt"
  if ($c.Order) {
    # copia de teste: AREPORTM deixa de conduzir a query; selecao direta pela encomenda
    $spec = Join-Path $env:TEMP "lz_test_$d.json"
    @(
      @{op='removeLink'; from='AREPORTM'; to='SORDER'},
      @{op='addLink'; from='SORDER'; to='AREPORTM'; fromFields=@('SOHNUM_0'); toFields=@('CLEA1_0'); join='leftouter'},
      @{op='selection'; text="{SORDER.SOHNUM_0} = '$($c.Order)' and {BPADDRESS_FCY.BPATYP_0} = 3 and {BPADDRESS_BPR.BPATYP_0} = 1 and {BPADDRESS_DLV.BPATYP_0} = 1 and {PRICSTRUCT.BPCBPS_0} = 1 and {AFCTFCY.FNC_0} = {?X3FCT} and {AFCTFCY.PRFCOD_0} = {?X3PRF}"}
    ) | ConvertTo-Json -Depth 4 | Out-File $spec -Encoding utf8
    $tmp = Join-Path $env:TEMP "LZ_${d}_test.rpt"
    $r = & (Join-Path $PSScriptRoot 'Edit-X3Report.ps1') -Rpt $rpt -Ops $spec -Out $tmp 2>&1 | Out-String
    if ($r -notmatch 'GRAVADO') { Write-Host "[$d] copia de teste falhou`n$r"; continue }
    $rpt = $tmp
  }
  $pdf = Join-Path $OutDir "LZ_${d}_$Lan.pdf"
  $res = & (Join-Path $PSScriptRoot 'Test-DocExport.ps1') -RptPath $rpt -Out $pdf -Usr $c.Usr -Etat $c.Etat -NumEdt $c.NumEdt -Extra $c.Extra -Png -MaxPages $MaxPages -Lan $Lan 2>&1 | Out-String
  $st = if ($res -match 'EXPORT OK') { 'OK' } else { 'FALHOU' }
  Write-Host "[$d] $st"
  if ($st -ne 'OK') { ($res -split "`r?`n") | ? { $_ -match 'FATAL|rro|ERR' } | select -First 4 | % { Write-Host "   $_" } }
}


