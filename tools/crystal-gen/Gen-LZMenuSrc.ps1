<#
.SYNOPSIS
  Gera o script AdxTL Lisoaz\ZLZMEN6003.src que cria/atualiza o menu local 6003 (AMENLOC + APLSTD) com
  os textos POR/ENG usados pelos relatorios LZ_*, a partir de specs\LZ_menus_6003.json (o mesmo registo
  que alimenta os RPT e o Excel -- ficam sempre sincronizados). Correr de novo depois de Gen-LZTexts.ps1.
#>
[CmdletBinding()]
param([string]$Out = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')) 'Lisoaz\ZLZMEN6003.src'))
$ErrorActionPreference = 'Stop'
$reg = (Get-Content (Join-Path $PSScriptRoot 'specs\LZ_menus_6003.json') -Raw -Encoding UTF8 | ConvertFrom-Json) | Sort-Object { [int]$_.num }

function Q($s) {   # literal AdxTL: aspas internas via chr$(34)
  if ($s -notmatch '"') { return '"' + $s + '"' }
  '"' + ($s -replace '"', '"+chr$(34)+"') + '"'
}
$lines = foreach ($m in $reg) {
  foreach ($x in @(@('POR', $m.por, ''), @('ENG', $m.eng, 'POR'))) {
    if ($x[1].Length -gt 123) { throw "Texto > 123 (APLSTD.LANMES A(123)): $($m.num) $($x[0])" }
    "Call GRAVA(WCHP,{0},""{1}"",{2},""{3}"",NNEW,NUPD,NERR)" -f $m.num, $x[0], (Q $x[1]), $x[2]
  }
}
$hdr = @'
#<AdxTL>@(#)0.0.0.0 $Revision$
###########################################################################################################
# ZLZMEN6003 - Cria / atualiza o menu local 6003 (textos dos relatorios LZ_* da Lisoaz) em POR e ENG.
#
# GERADO por tools/crystal-gen/Gen-LZMenuSrc.ps1 a partir de tools/crystal-gen/specs/LZ_menus_6003.json
# (o mesmo registo usado pelos RPT LZ_* e por Lisoaz/Menus_locais_LZ.xlsx) -- nao editar a mao.
#
# Como executar (pasta LZPROTO, utilizador com acesso ao dicionario):
#   1. Desenvolvimento > Utilitarios > Diversos > Executar tratamentos (EXETRT) -> tratamento ZLZMEN6003
#   2. Desenvolvimento > Utilitarios > Dicionario > Atualizacao menus locais (GENMENULOC) -> POR e ENG
#      (sem este passo os textos novos so aparecem depois da proxima atualizacao do dicionario)
#
# Idempotente: cria o que falta e atualiza os textos que mudaram (so LANCHP = 6003). Nao apaga nada.
# Tudo numa transacao: se houver algum erro de escrita faz Rollback e nada fica gravado (ver o trace).
# Cabecalho AMENLOC como na TEB (modulo 9; alteravel=Nao; menu local=Sim; nao traduzir=Nao), especifico=Sim.
# Codigo de atividade em WACT: vazio, porque a atividade XMEGA (Megavale) nao existe na Lisoaz; se for
# criada, preencher WACT = "XMEGA" e voltar a executar.
# APLSTD.LANMES e A(123): textos maiores ja vem divididos em duas entradas (ver "continuacao" no Excel).
###########################################################################################################

Local Integer WCHP : WCHP = 6003
Local Char    WACT(10) : WACT = ""
Local Integer NNEW, NUPD, NERR
Local Integer WTRACE : WTRACE = 0

If GTRACE = ""
  WTRACE = 1
  Call OUVRE_TRACE("Menu local 6003 - relatorios Lisoaz") From LECFIC
Endif
Call ECR_TRACE("Menu local " + num$(WCHP) + " - inicio", -2) From GESECRAN

If clalev([F:AML]) = 0 : Local File AMENLOC [AML] : Endif
If clalev([F:AST]) = 0 : Local File APLSTD [AST] : Endif

Trbegin [AML],[AST]

# ---------------------------------------------------------------- cabecalho do menu local (AMENLOC)
Read [F:AML]MENLOC = WCHP
If fstat
  Raz [F:AML]
  [F:AML]MENLOC   = WCHP
  [F:AML]MODULE   = 9
  [F:AML]CODACT   = WACT
  [F:AML]AUZMOD   = 1
  [F:AML]MENLOCAL = 2
  [F:AML]SPECIF   = 2
  [F:AML]NONTRA   = 1
  [F:AML]CREDAT   = date$
  [F:AML]CREUSR   = GUSER
  Write [F:AML]
  If fstat
    NERR += 1
    Call ECR_TRACE("Erro a criar AMENLOC " + num$(WCHP) + " (fstat=" + num$(fstat) + ")", 1) From GESECRAN
  Else
    Call ECR_TRACE("AMENLOC " + num$(WCHP) + " criado", -1) From GESECRAN
  Endif
Else
  Call ECR_TRACE("AMENLOC " + num$(WCHP) + " ja existe (mantido)", 0) From GESECRAN
Endif

# ---------------------------------------------------------------- textos (APLSTD): numero, lingua, texto, lingua original
Call GRAVA(WCHP,0,"POR","Lisoaz - textos dos relatorios LZ_*","",NNEW,NUPD,NERR)
Call GRAVA(WCHP,0,"ENG","Lisoaz - LZ_* report texts","POR",NNEW,NUPD,NERR)
'@
$ftr = @'

If NERR = 0
  Commit
  Call ECR_TRACE("Concluido: " + num$(NNEW) + " texto(s) criados, " + num$(NUPD) + " atualizados", -2) From GESECRAN
  Call ECR_TRACE("Falta: Atualizacao menus locais (GENMENULOC) para POR e ENG", -1) From GESECRAN
Else
  Rollback
  Call ECR_TRACE(num$(NERR) + " erro(s): transacao anulada, nada foi gravado", 1) From GESECRAN
Endif

If WTRACE = 1
  Call FERME_TRACE From LECFIC
  Call LEC_TRACE From LECFIC
Endif
End

###########################################################################################################
# GRAVA - cria ou atualiza uma linha de APLSTD (chave CLE = LANCHP;LANNUM;LAN). Chamado dentro da transacao.
###########################################################################################################
Subprog GRAVA(WCHP, WNUM, WLAN, WTXT, WORI, NNEW, NUPD, NERR)
Value Integer WCHP
Value Integer WNUM
Value Char WLAN()
Value Char WTXT()
Value Char WORI()
Variable Integer NNEW
Variable Integer NUPD
Variable Integer NERR

If clalev([F:AST]) = 0 : Local File APLSTD [AST] : Endif

Readlock [F:AST]CLE = WCHP ; WNUM ; WLAN
If fstat = 0
  If [F:AST]LANMES <> WTXT
    [F:AST]LANMES = WTXT
    [F:AST]UPDDAT = date$
    [F:AST]UPDUSR = GUSER
    Rewrite [F:AST]
    If fstat
      NERR += 1
      Call ECR_TRACE("Erro a atualizar " + num$(WCHP) + "/" + num$(WNUM) + " " + WLAN + " (fstat=" + num$(fstat) + ")", 1) From GESECRAN
    Else
      NUPD += 1
      Call ECR_TRACE("Atualizado " + num$(WNUM) + " " + WLAN + ": " + WTXT, 0) From GESECRAN
    Endif
  Endif
Elsif fstat = 1
  NERR += 1
  Call ECR_TRACE("Linha bloqueada " + num$(WCHP) + "/" + num$(WNUM) + " " + WLAN, 1) From GESECRAN
Else
  Raz [F:AST]
  [F:AST]LANCHP = WCHP
  [F:AST]LANNUM = WNUM
  [F:AST]LAN    = WLAN
  [F:AST]LANMES = WTXT
  [F:AST]LANORI = WORI
  [F:AST]CREDAT = date$
  [F:AST]CREUSR = GUSER
  Write [F:AST]
  If fstat
    NERR += 1
    Call ECR_TRACE("Erro a criar " + num$(WCHP) + "/" + num$(WNUM) + " " + WLAN + " (fstat=" + num$(fstat) + ")", 1) From GESECRAN
  Else
    NNEW += 1
  Endif
Endif
End
'@
$txt = $hdr + "`r`n" + (($lines | ForEach-Object { $_ }) -join "`r`n") + "`r`n" + $ftr
$txt = $txt -replace "(?<!`r)`n", "`r`n"
[IO.File]::WriteAllText($Out, $txt, (New-Object Text.UTF8Encoding($false)))
Write-Host "-> $Out ($($reg.Count) entradas x 2 linguas = $($lines.Count) chamadas GRAVA)"
