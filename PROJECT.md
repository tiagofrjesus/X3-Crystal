# PROJECT — X3-Crystal (Megavale)

Relatórios Crystal Reports e customizações AdxTL de apoio para clientes Sage X3 da Megavale, gerados e editados por código (RAS SDK), sem abrir o Designer.

## Clientes / pastas

| Cliente | Coleção (schema SQL) | Base de dados | Servidor | Notas |
|---|---|---|---|---|
| **TEB** | `TEB` | `tebx3` | `192.168.1.211` | Produção ativa. DSN de build local `TEST_TEB211`. |
| **Lisoaz** | `LZPROTO` | `x3lz` | `192.168.10.11\SAGEX3` | Novo cliente (2026-10). Precisa da VPN Lisoaz. Ainda sem movimentos (dados demo no schema `SEED`). Sociedade `LZACM`, sites `01INJ` / `02MAQ`. |

Credenciais em `.env` (não versionar). **Acesso às BD: só SELECT.**

## Convenções

- **Prefixo custom:** `Z` (TEB: atividade `XMEGA`; a Lisoaz ainda não tem esta atividade).
- **Menu local Megavale:** capítulo **6003** (TEB: números 0–101; Lisoaz: novos a partir de 102).
- **SPE genérico Megavale:** `ZRPDFPATH` (envio direto por email; `AREPORT.TRTSPE`). É opcional por cliente.
- **Certificação AT (LEG POR):** nunca alterar campos `PT_MENTION*` / `PT*` / ATCUD / QR, nem as secções onde estão (posição, visibilidade, condições, altura). São os campos que a Sage valida na homologação. Os modelos base certificados estão em `Reports-BaseX3/` (ver `reports_certificados_LEG_POR.PNG`).

## Módulos ativos

Vendas (SQH, SOH, SDH, SIH), Compras (POH), Stocks, Nota de transporte (TNH), Tesouraria/pagamentos (PAY), Contabilidade (peças GAS — TEB_PIECE).

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `Reports-TEB/` | Relatórios TEB (`TEB_*.rpt`) e scripts AdxTL TEB (`Z*.src`). |
| `Reports-BaseX3/` | Modelos standard Sage certificados (referência; não editar). |
| `Lisoaz/RPTs/` | Relatórios Lisoaz `LZ_*.rpt` + `LZ_*.txt` (registo AREPORT/AREPORTD/AREPORTV). |
| `Lisoaz/` | `Menus_locais_LZ.xlsx`, `ZLZMEN6003.src`, `Auditoria_Certificacao_LZ.md`, `PDFs-exemplo/`. |
| `tools/crystal-gen/` | Ferramentas (editor JSON de .rpt, geradores, testes, validadores). **Ler `LESSONS.md` antes de editar .rpt.** |

## Ferramentas principais (`tools/crystal-gen/`)

- `Edit-X3Report.ps1` + `X3RptEdit.cs` — editor de .rpt guiado por spec JSON (ops: field/label/box/set/remove/formula/formulaReplace/theme/sectionHeight/addTable/removeLink/…; `"sub"` para subreports).
- `Build-Lisoaz.ps1` — gera `Lisoaz/RPTs/LZ_*.rpt` a partir de `Reports-TEB/TEB_*.rpt` (specs `LZ_<doc>_port` > `LZ_common` > `LZ_<doc>_texts` > `LZ_<doc>_design`).
- `Gen-LZTexts.ps1` / `Gen-LZDesign.ps1` / `Gen-LZMenuSrc.ps1` — geram os specs, o registo `specs/LZ_menus_6003.json` e o script `Lisoaz/ZLZMEN6003.src`.
- `Test-AllLZ.ps1 -Lan POR|ENG` / `Test-DocExport.ps1` — impressão local (PDF/PNG) com dados reais TEB (pedidos `AREPORTM`).
- `Validate-LZ.ps1` — compara com o TEB: campos e secções certificadas, parâmetros, credenciais.
- `Audit-Certification.ps1` — compara com os modelos base Sage.
- Tudo o que usa o Crystal corre em **PowerShell 32-bit** (os scripts relançam-se sozinhos).

## Trabalho ativo

**Layouts Lisoaz** (ORC, ENC, GRC, FAC, REC, PAG, ENF, GT). RPT prontos e validados, com design uniforme e POR/ENG (idioma = `BPARTNER.LAN_0`: POR → português, senão ENG). Falta do lado X3 Lisoaz:

1. Executar `Lisoaz/ZLZMEN6003.src` (EXETRT) e depois *Atualização menus locais* (GENMENULOC), POR e ENG.
2. Registar os códigos `LZ_*` no AREPORT (copiar de `TEB_*`; CRYCOD = nome do .rpt). Ver `Lisoaz/RPTs/LZ_*.txt`.
3. Carregar o logo (`ABLOB` `LOGO` da sociedade `LZACM`).
4. Decidir sobre o `TRTSPE ZRPDFPATH`. Associar os `LZ_*` aos botões e destinos de impressão.
