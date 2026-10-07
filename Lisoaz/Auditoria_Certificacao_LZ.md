# Auditoria de certificação AT — relatórios LZ (Lisoaz) vs modelos base Sage

Gerado por `tools/crystal-gen/Audit-Certification.ps1` em 2026-10-07 23:17. Supressão condicional (fórmulas) não avaliada; só a estática.

## Resumo

| Relatório | Modelo Sage | Fontes certificadas verificadas | Problemas (A/B/C) |
|---|---|---|---|
| LZ_ORC | DEVICLIENT2 | 5 | 0 |
| LZ_ENC | ARCCLIENT2 | 5 | 0 |
| LZ_GRC | BONLIV2 | 19 | 2 |
| LZ_FAC | SBONFACP | 20 | 0 |
| LZ_REC | P-RECIBO | 2 | 0 |
| LZ_PAG | P-RECIBO | 2 | 3 |
| LZ_ENF | BONCDE2 | 0 | 0 |
| LZ_GT | TRNNTE | 2 | 0 |

## Conclusões

Todos os campos certificados (PT_MENTION*, PTCPY*, PTATCOD, ATCUD, QR) e as secções onde estão ficaram **exatamente como nos relatórios TEB de origem**: posição, fórmula, visibilidade estática e condicional, e parâmetros da secção. Isto é verificado por `Validate-LZ.ps1`. As diferenças face aos modelos Sage abaixo vêm todas da TEB e não foram alteradas, por regra (são os campos que a Sage valida na homologação):

- **LZ_GRC — `{@SelItmDes}` em falta.** A TEB substituiu a designação certificada por `ZDESCL`, e o LZ usa `ITMDES_0`. Com `ITMMASTER.SELITMDES = 0` (o caso de todos os artigos: 16 951 na TEB e 542 no demo Lisoaz), a fórmula do modelo devolve exatamente `ITMDES_0`, por isso o resultado é igual. **Manter `SELITMDES = 0` nos artigos da Lisoaz**; caso contrário, repor as tabelas `AVWTEXTRA_DES2/3` e a fórmula.
- **LZ_GRC — `PTMENTION07` oculto.** A TEB mostra o seu próprio rótulo de tipo de documento. Fica como na TEB, por decisão de não mexer em PT_MENTION.
- **LZ_PAG — sem ATCUD/QR/PORQRC.** A TEB_PAG não deriva do P-RECIBO. Para pagamentos a fornecedor o X3 não gera ATCUD/QR (a `PORQRC` só tem registos de recibos `R*`; 0 para `PCNF-*`), por isso não há falha. Os parâmetros `numedt/seqedt/etat/usr/X3LAN/impdetiva` do P-RECIBO não são usados pela TEB_PAG, que seleciona por `numdeb/numfin`.
## LZ_ORC  ↔  DEVICLIENT2

**A. Campos certificados (visível no modelo → no LZ)**

| Fonte | Modelo | LZ | Estado |
|---|---|---|---|
| `{@atcud}` | oculto | oculto | ok |
| `{TMPSRPT.PTMENTION01_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION04_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION05_0}` | visível | visível | ok |
| `QR (PORQRC.IMGQRC)` | oculto | oculto | ok |

**B. Fórmulas de certificação**

- todas iguais ao modelo (1: atcud)

**C. Tabelas e ligações de certificação**

- TMPSRPT : modelo=True LZ=True 
- PORQRC : modelo=True LZ=True 
- TMPSRPT: igual ao modelo
- PORQRC: igual ao modelo

**D. Parâmetros**

- modelo: 32, LZ: 35
- só no LZ: PrintImg, nIBANs, decimalPunit

**E. Seleção de registos (condições de certificação)**

- modelo: (sem condições TMPSRPT)
- LZ: (sem condições TMPSRPT)

## LZ_ENC  ↔  ARCCLIENT2

**A. Campos certificados (visível no modelo → no LZ)**

| Fonte | Modelo | LZ | Estado |
|---|---|---|---|
| `{@atcud}` | oculto | visível | ok |
| `{TMPSRPT.PTMENTION01_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION04_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION05_0}` | visível | visível | ok |
| `QR (PORQRC.IMGQRC)` | oculto | visível | ok |

**B. Fórmulas de certificação**

- todas iguais ao modelo (1: atcud)

**C. Tabelas e ligações de certificação**

- TMPSRPT : modelo=True LZ=True 
- PORQRC : modelo=True LZ=True 
- TMPSRPT: igual ao modelo
- PORQRC: igual ao modelo

**D. Parâmetros**

- modelo: 35, LZ: 35

**E. Seleção de registos (condições de certificação)**

- modelo: (sem condições TMPSRPT)
- LZ: (sem condições TMPSRPT)

## LZ_GRC  ↔  BONLIV2

**A. Campos certificados (visível no modelo → no LZ)**

| Fonte | Modelo | LZ | Estado |
|---|---|---|---|
| `{@atcud}` | oculto | visível | ok |
| `{@PT_MENTION_001}` | visível | visível | ok |
| `{@SelItmDes}` | visível | — | FALTA |
| `{TMPSRPT.PTATCOD_0}` | visível | visível | ok |
| `{TMPSRPT.PTCPYADDLIG_0}` | visível | visível | ok |
| `{TMPSRPT.PTCPYADDLIG_1}` | visível | visível | ok |
| `{TMPSRPT.PTCPYADDLIG_2}` | visível | visível | ok |
| `{TMPSRPT.PTCPYCTY_0}` | visível | visível | ok |
| `{TMPSRPT.PTCPYNAM_0}` | visível | visível | ok |
| `{TMPSRPT.PTCPYPOSCOD_0}` | visível | visível | ok |
| `{TMPSRPT.PTCPYSAT_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION01_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION02_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION03_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION05_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION06_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION07_0}` | visível | oculto | OCULTO no LZ |
| `{TMPSRPTDET.VATEXEREA_0}` | visível | visível | ok |
| `QR (PORQRC.IMGQRC)` | oculto | visível | ok |

**B. Fórmulas de certificação**

- todas iguais ao modelo (4: PT_MENTION_001, isPtFinalConsumer, atcud, SelItmDes)

**C. Tabelas e ligações de certificação**

- TMPSRPT : modelo=True LZ=True 
- TMPSRPTDET : modelo=True LZ=True 
- PORQRC : modelo=True LZ=True 
- TMPSRPT: igual ao modelo
- PORQRC: igual ao modelo
- TMPSRPTDET: igual ao modelo

**D. Parâmetros**

- modelo: 34, LZ: 35
- só no LZ: svalores

**E. Seleção de registos (condições de certificação)**

- modelo: {TMPSRPTDET.NUMREQ_0} | {TMPSRPTDET.NUMREQ_0} = {?numedt} | {TMPSRPTDET.RPTCOD_0} | {TMPSRPTDET.RPTCOD_0} = {?etat} | {TMPSRPTDET.USR_0} | {TMPSRPTDET.USR_0} = {?usr}
- LZ: {TMPSRPTDET.NUMREQ_0} | {TMPSRPTDET.NUMREQ_0} = {?numedt} | {TMPSRPTDET.RPTCOD_0} | {TMPSRPTDET.RPTCOD_0} = {?etat} | {TMPSRPTDET.USR_0} | {TMPSRPTDET.USR_0} = {?usr}

## LZ_FAC  ↔  SBONFACP

**A. Campos certificados (visível no modelo → no LZ)**

| Fonte | Modelo | LZ | Estado |
|---|---|---|---|
| `{@atcud_isLastPage_true}` | oculto | — | falta (oculto no modelo) |
| `{@atcud_isLastPage}` | oculto | oculto | ok |
| `{@atcud}` | visível | visível | ok |
| `{@PT_MENTION_001}` | visível | visível | ok |
| `{TMPSRPT.PTCPYADDLIG_0}` | visível | visível | ok |
| `{TMPSRPT.PTCPYADDLIG_1}` | visível | visível | ok |
| `{TMPSRPT.PTCPYADDLIG_2}` | visível | visível | ok |
| `{TMPSRPT.PTCPYCTY_0}` | visível | visível | ok |
| `{TMPSRPT.PTCPYNAM_0}` | visível | visível | ok |
| `{TMPSRPT.PTCPYPOSCOD_0}` | visível | visível | ok |
| `{TMPSRPT.PTCPYSAT_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION01_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION02_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION03_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION04_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION05_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION06_0}` | visível | visível | ok |
| `{TMPSRPT.PTMENTION07_0}` | visível | visível | ok |
| `{TMPSRPTDET.VATEXEREA_0}` | visível | visível | ok |
| `QR (PORQRC.IMGQRC)` | visível | visível | ok |

**B. Fórmulas de certificação**

- todas iguais ao modelo (8: isPtFinalConsumer, isPtFooterText, isPtHeaderText, isPtTrainingEnv, PT_MENTION_001, atcud, atcud_isLastPage, atcud_isLastPage_true)

**C. Tabelas e ligações de certificação**

- TMPSRPT : modelo=True LZ=True 
- TMPSRPTDET : modelo=True LZ=True 
- PORQRC : modelo=True LZ=True 
- TMPSRPT: igual ao modelo
- PORQRC: igual ao modelo
- TMPSRPTDET: igual ao modelo

**D. Parâmetros**

- modelo: 38, LZ: 40
- só no LZ: nIBANs, MapaCarga

**E. Seleção de registos (condições de certificação)**

- modelo: {TMPSRPTDET.NUMREQ_0} | {TMPSRPTDET.NUMREQ_0} = {?numedt} | {TMPSRPTDET.RPTCOD_0} | {TMPSRPTDET.RPTCOD_0} = {?etat} | {TMPSRPTDET.USR_0} | {TMPSRPTDET.USR_0} = {?usr}
- LZ: {TMPSRPTDET.NUMREQ_0} | {TMPSRPTDET.NUMREQ_0} = {?numedt} | {TMPSRPTDET.RPTCOD_0} | {TMPSRPTDET.RPTCOD_0} = {?etat} | {TMPSRPTDET.USR_0} | {TMPSRPTDET.USR_0} = {?usr}

## LZ_REC  ↔  P-RECIBO

**A. Campos certificados (visível no modelo → no LZ)**

| Fonte | Modelo | LZ | Estado |
|---|---|---|---|
| `{@atcud}` | visível | visível | ok |
| `QR (PORQRC.IMGQRC)` | oculto | visível | ok |

**B. Fórmulas de certificação**

- todas iguais ao modelo (1: atcud)

**C. Tabelas e ligações de certificação**

- PORQRC : modelo=True LZ=True 
- PORQRC: igual ao modelo

**D. Parâmetros**

- modelo: 16, LZ: 16

**E. Seleção de registos (condições de certificação)**

- modelo: (sem condições TMPSRPT)
- LZ: (sem condições TMPSRPT)

## LZ_PAG  ↔  P-RECIBO

**A. Campos certificados (visível no modelo → no LZ)**

| Fonte | Modelo | LZ | Estado |
|---|---|---|---|
| `{@atcud}` | visível | — | FALTA |
| `QR (PORQRC.IMGQRC)` | oculto | — | falta (oculto no modelo) |

**B. Fórmulas de certificação**

- `atcud`: **não existe no LZ**

**C. Tabelas e ligações de certificação**

- PORQRC : modelo=True LZ=False **FALTA**
- ligação do modelo sem equivalente no LZ: `Src=PAYMENTH Dst=PORQRC SrcFields=[NUM_0 PAYTYP_0 ] DstFields=[DOCNUM_0 DOCTYP_0 ] JoinType=crTableJoinTypeLeftOuterJoin`

**D. Parâmetros**

- modelo: 16, LZ: 10
- só no modelo: impdetiva, numedt, seqedt, etat, usr, X3LAN

**E. Seleção de registos (condições de certificação)**

- modelo: (sem condições TMPSRPT)
- LZ: (sem condições TMPSRPT)

## LZ_ENF  ↔  BONCDE2

**A. Campos certificados (visível no modelo → no LZ)**

| Fonte | Modelo | LZ | Estado |
|---|---|---|---|

**B. Fórmulas de certificação**

- todas iguais ao modelo (0: )

**C. Tabelas e ligações de certificação**


**D. Parâmetros**

- modelo: 33, LZ: 33

**E. Seleção de registos (condições de certificação)**

- modelo: (sem condições TMPSRPT)
- LZ: (sem condições TMPSRPT)

## LZ_GT  ↔  TRNNTE

**A. Campos certificados (visível no modelo → no LZ)**

| Fonte | Modelo | LZ | Estado |
|---|---|---|---|
| `{@atcud}` | oculto | visível | ok |
| `QR (PORQRC.IMGQRC)` | oculto | visível | ok |

**B. Fórmulas de certificação**

- todas iguais ao modelo (1: atcud)

**C. Tabelas e ligações de certificação**

- PORQRC : modelo=True LZ=True 
- PORQRC: igual ao modelo

**D. Parâmetros**

- modelo: 22, LZ: 22

**E. Seleção de registos (condições de certificação)**

- modelo: (sem condições TMPSRPT)
- LZ: (sem condições TMPSRPT)

