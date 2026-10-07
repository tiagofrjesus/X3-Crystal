# Lições aprendidas — geração/edição de .rpt Sage X3 via RAS SDK

Todos os agentes `x3-crystal-*` devem ler este ficheiro ANTES de gerar/editar qualquer `.rpt`.
Cada lição aqui custou tempo real a descobrir por tentativa-erro nesta sessão — não repitas.

Ver também a skill `/x3` (modo Sage X3/AdxTL) para lookups de schema de tabelas, menus locais,
e o guia `tools/crystal-gen/` (ficheiros `.cs` existentes são exemplos de referência validados).

---

## Ambiente

- **Tudo corre em PowerShell 32-BIT**: `C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe`.
  Em 64-bit dá erro de tipo/permissões a carregar as DLLs do Crystal.
- Referências GAC necessárias em TODOS os scripts (copiar de qualquer `.ps1` já existente em
  `tools/crystal-gen/`):
  ```
  $gac='C:\WINDOWS\Microsoft.Net\assembly\GAC_MSIL'
  $v='v4.0_13.0.4000.0__692fbea5521e1304'
  CrystalDecisions.CrystalReports.Engine.dll, CrystalDecisions.Shared.dll,
  CrystalDecisions.ReportAppServer.ClientDoc.dll, CrystalDecisions.ReportAppServer.DataDefModel.dll,
  CrystalDecisions.ReportAppServer.ReportDefModel.dll, CrystalDecisions.ReportAppServer.Controllers.dll,
  CrystalDecisions.ReportAppServer.CommonObjectModel.dll
  ```
- `Add-Type` trata warnings como erros — nunca deixar variáveis locais sem uso no C#.
- Acentos: escrever `.cs` em UTF-8; ler com `Get-Content -Raw -Encoding UTF8`.
- **Gerar relatório NOVO** (do zero): partir sempre de um seed em branco (`New-Blank.rpt`) —
  `ReportClientDocument.New()` crasha no runtime redistribuível.
- **Editar relatório EXISTENTE**: `eng.Load(caminho)` diretamente, não usar seed.
- COM-interop do PowerShell rebenta a passar objetos COM como argumento entre chamadas
  (`IsComObject failed`) → fazer SEMPRE a construção em C# compilado via `Add-Type`, nunca
  chamadas RAS diretas do PowerShell.
- Usar sempre as **coclasses** (`...Class`) no `new`, nunca as interfaces.

---

## Texto estático — NUNCA usar TextObjectClass/Paragraphs

`TextObjectClass` com `ParagraphsClass`/`ParagraphClass`/`ParagraphTextLinesClass` construídos à
mão **CRASHA** o processo. Confirmado nesta sessão.

Padrão seguro (usado em todos os scripts deste projeto): fórmula com literal + FieldObject:
```csharp
static int lblN = 0;
static void AddText(Section sec, string text, int l, int t, int w, int h) {
  string fn = "lbl" + (lblN++);
  string esc = text.Replace("'", "''");
  RCD.DataDefController.FormulaFieldController.AddByName(fn, "'" + esc + "'", CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
  AddField(sec, "{@" + fn + "}", CrFieldValueTypeEnum.crFieldValueTypeStringField, l, t, w, h, fn);
}
```

---

## Reposicionar objetos existentes — sempre Clone+Modify

Atribuir `ro.Left = X` diretamente NÃO persiste ao gravar (fica certo na sessão viva, mas volta
ao original depois de `SaveAs`+reload). Padrão correto:
```csharp
var clone = (ISCRReportObject)ro.Clone(true);
clone.Left = l; clone.Top = t; clone.Width = w;
RCD.ReportDefController.ReportObjectController.Modify(ro, clone);
```

## Objetos Line e Box — bug conhecido no Modify()

Para `Line` e `Box`, o RAS SDK mantém a margem DIREITA original (`Right = OldLeft+OldWidth`) e só
desloca o `Left` — a `Width` fica recalculada como `OldRight - NewLeft`, ignorando o valor pedido.
`Add()` de uma `Line` NOVA falha SEMPRE nesta versão do SDK ("Não há suporte para a inclusão ou a
alteração desse tipo de objeto de relatório" — mesma limitação que Subreport, ver abaixo).

Na prática raramente importa: são réguas/fundos decorativos que, com `Left=0`, ficam simplesmente
cortados na margem da página, visualmente idênticos a um objeto já reescalado. Só é um problema
real se o objeto NÃO começar em `Left=0` e precisar mesmo de encolher — nesse caso não há solução
limpa conhecida; documentar a limitação em vez de tentar contornar.

## Objetos Box — ReportObjectController.Add() NÃO suporta este tipo (resolvido)

Erro: "Seção de relatório não localizada" (`Section not found`), lançado por
`ReportObjectControllerClass.Add(ReportObject, Section, Int32)`.

Confirmado nesta sessão, com teste isolado e reprodutível (`tools/crystal-gen/BoxTest.ps1` +
`X3RptBoxTest.cs`, caso `TestBlank`): `Add()` de uma `BoxObjectClass` falha SEMPRE, mesmo:
- num seed em branco (`New-Blank.rpt`) recém-carregado, como PRIMEIRÍSSIMA operação de toda a
  sessão (sem nenhum Add/Remove/Modify anterior que pudesse ter invalidado a `Section`);
- numa secção `DetailSection1` obtida na mesma iteração, sem cache antigo;
- em qualquer secção testada (Detail, PageFooter, GroupHeader).

Ou seja: **não é** um problema de secção "stale"/invalidada por operações anteriores (como
acontece com `Line`), nem de ordem de operações — é uma limitação de TIPO, na mesma família de
`Subreport` (ver abaixo), só que com uma mensagem de erro diferente. `Add()` de `FieldObjectClass`
e `TextObjectClass`-via-fórmula continuam a funcionar sempre, em qualquer secção, em qualquer
momento da sessão (confirmado repetidamente nesta e noutras sessões).

**Contorno validado**: usar um `FieldObjectClass` com uma fórmula vazia (`''`) como `DataSource` e
preencher a propriedade `.Border` (`BorderClass` com `LeftLineStyle`/`RightLineStyle`/
`TopLineStyle`/`BottomLineStyle = crLineStyleSingle`) — visualmente idêntico a uma `Box`, mas
`Add()` funciona de forma fiável porque o objeto é do tipo `Field`, não `Box`:
```csharp
RCD.DataDefController.FormulaFieldController.AddByName("fHdrMetaBox", "''", CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
var boxFld = new FieldObjectClass();
boxFld.DataSource = "{@fHdrMetaBox}";
boxFld.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeStringField;
boxFld.Kind = CrReportObjectKindEnum.crReportObjectKindField;
boxFld.Left = 35; boxFld.Top = 955; boxFld.Width = 11750; boxFld.Height = 495;
boxFld.Name = "hdrMetaBox";
var brd = new BorderClass();
brd.LeftLineStyle = CrLineStyleEnum.crLineStyleSingle;
brd.RightLineStyle = CrLineStyleEnum.crLineStyleSingle;
brd.TopLineStyle = CrLineStyleEnum.crLineStyleSingle;
brd.BottomLineStyle = CrLineStyleEnum.crLineStyleSingle;
brd.BackgroundColor = 0xFFFFFFFF;
boxFld.Border = brd;
RCD.ReportDefController.ReportObjectController.Add(boxFld, sec13, -1);
```
Ver `tools/crystal-gen/X3RptPieceHeaderFinish.cs` (`hdrMetaBox`) como exemplo completo validado
(gravado, recarregado, confirmado sem overlap via `Inspect-X3Report.ps1`).

## Subreports — ReportObjectController.Add NÃO suporta este tipo

Erro: "Não há suporte para a inclusão ou a alteração desse tipo de objeto de relatório."

Usar antes `SubreportController.ImportSubreportEx`:
```csharp
RCD.SubreportController.ImportSubreportEx(string Name, string reportURL, Section Section,
                                           int left, int top, int width, int height)
  -> SubreportClientDocument
```
`reportURL` tem de ser um CAMINHO DE FICHEIRO real — não aceita o nome de um subreport já
embutido diretamente. Para reaproveitar um subreport já existente no `.rpt` (ex. um logótipo):
1. `RCD.SubreportController.GetSubreport("nome")` → devolve o `ISCDReportClientDocument` embutido.
2. Gravar esse subreport para um `.rpt` temporário em disco.
3. Chamar `ImportSubreportEx` com o caminho desse ficheiro temporário.

### Este roundtrip NÃO preserva RecordSelectionFormula nem SubreportLinks — reatribuir manualmente

Confirmado reproduzindo e corrigindo o bug em `TEB_PIECE.rpt` (2026-07-23, ERR 504 "Missing
parameter values" em runtime real, `Job 5811`): criar um subreport novo por
`GetSubreport()`→`SaveAs()`→`ImportSubreportEx()` (para reaproveitar um logótipo já embutido
noutra secção) perde DUAS coisas em relação ao subreport original, e as DUAS têm de ser
reatribuídas manualmente a seguir, ou o parâmetro fica "órfão" (declarado, `PromptToUser=True`,
mas `UseCount=0`) e o motor Crystal .NET REAL (não o preview/validação local) falha com "Missing
parameter values", mesmo que a validação estrutural (`Inspect-X3Report.ps1`) pareça OK:

1. **`RecordFilter`/`RecordSelectionFormula` do subreport** — fica vazia (`[]`). A atribuição
   DIRETA de propriedade (`subDoc.DataDefController.DataDefinition.RecordFilter.FreeEditingText =
   texto;`) **NÃO PERSISTE** ao gravar (fica certa na sessão viva — confirma-se relendo antes do
   `SaveAs` — mas volta a vazia depois de `SaveAs`+reload), o mesmo padrão "atribuição direta não
   persiste" já documentado para `Left`/`Top` de `ReportObject`. A API que persiste de facto é o
   controller dedicado, descoberto por reflexão sobre `ISCRDataDefController`:
   ```csharp
   subDoc.DataDefController.RecordFilterController.SetFormulaText(textoDaFormula);
   ```
   (`RecordFilterController` é do tipo `FilterController`/`ISCRFilterController`, que também expõe
   `Modify(Filter NewFilter)`, `AddItem`, `ModifyItem` — `SetFormulaText` é o mais direto quando já
   se tem o texto completo da fórmula de outro subreport equivalente.)

2. **`SubreportLinks` do `SubreportObject`** (a colocação do subreport DENTRO do relatório
   principal, não o subreport em si) — fica com 0 entradas. Um subreport original que partilha
   parâmetro com o relatório principal (ex. `logo2`/`logo3`/`logo1` em `TEB_PIECE.rpt`, todos
   recebendo `{?X3DOS}` do relatório principal) tem sempre exatamente 1
   `SubreportLink` (`MainReportFieldName={?X3DOS}`, `SubreportFieldName={?X3DOS}`,
   `LinkedParameterName={?X3DOS}`) no `SubreportObject` de colocação — é este link que faz o motor
   de impressão real herdar o VALOR do parâmetro do relatório principal em vez de o pedir de novo.
   Também aqui a atribuição direta não persiste — usar Clone+Modify (mesmo padrão de
   "Reposicionar objetos existentes" acima):
   ```csharp
   var clone = (ISCRReportObject)subreportObj.Clone(true);
   var srClone = (SubreportObject)clone;
   var newLinks = new SubreportLinksClass();          // SEMPRE nova coleção, nunca reaproveitar
   var lk = new SubreportLinkClass();
   lk.MainReportFieldName = "{?X3DOS}";
   lk.SubreportFieldName  = "{?X3DOS}";
   lk.LinkedParameterName = "{?X3DOS}";
   newLinks.Add(lk);
   srClone.SubreportLinks = newLinks;
   RCD.ReportDefController.ReportObjectController.Modify(subreportObj, clone);
   ```

**Diagnóstico/confirmação**: usar `ISCRParameterField` (via `typeof(ISCRParameterField).GetProperty
("UseCount")` — reflexão sobre o TIPO da interface COMPILE-TIME, não `pf.GetType()`, que devolve
`System.__ComObject` sem interfaces úteis para um RCW) para ler o `UseCount` do parâmetro dentro de
cada subreport. Um subreport com `RecordFilter` a usar o parâmetro E `SubreportLinks` a ligá-lo ao
principal mostra `UseCount=2`; só com o filtro (sem o link) mostra `UseCount=1`; sem nenhum dos
dois, `UseCount=0` (órfão, é o estado quebrado). Comparar sempre com um subreport IRMÃO que já
funciona (ex. `logo2` vs `logoHdr2` novo) em vez de adivinhar o valor esperado.

Ver `tools/crystal-gen/X3RptFixSubFilter2.cs` + `tools/crystal-gen/Fix-PieceSubFilter.ps1` como
exemplo completo validado (aplicado e confirmado com `Reports-TEB/TEB_PIECE.rpt`).

---

## Tabelas novas — SEMPRE nativas, nunca Command

Lição crítica (registada primeiro em `Reports-TEB/TEB_ITM_ETIQx60.txt`, reconfirmada com
`TEB_PIECE`): o motor de impressão do X3 só REMAPEIA a ligação de tabelas NATIVAS para o DSN do
folder em runtime. Uma `CommandTableClass` fica colada ao DSN de build e dá "Falha de logon" no
print server de produção.

Padrão validado (ver `X3RptPiecePortrait.cs`, `X3RptPieceHeaderRedesign.cs`):
```csharp
var logon = new PropertyBagClass(); logon.Add("DSN", workDsn); logon.Add("Database", workDb);
logon.Add("UseDSNProperties", "False"); logon.Add("UID", workUser); logon.Add("PWD", workPass);
var attr = new PropertyBagClass(); attr.Add("Database DLL", "crdb_odbc.dll");
attr.Add("QE_DatabaseName", workDb); attr.Add("QE_DatabaseType", "ODBC (RDO)");
attr.Add("QE_ServerDescription", workDsn); attr.Add("QE_SQLDB", "True"); attr.Add("SSO Enabled", "False");
attr.Add("QE_LogonProperties", logon);
var ci = new ConnectionInfoClass(); ci.Attributes = attr; ci.UserName = workUser; ci.Password = workPass;
ci.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;

var nt = new TableClass();
nt.Name = "NOMETABELA";                  // nome real da tabela X3
nt.Alias = "ALIAS_ESCOLHIDO";            // como as fórmulas/campos vão referenciar
nt.QualifiedName = "TEB.NOMETABELA";     // schema.tabela explícito — portável dev/prod
nt.ConnectionInfo = ci;
RCD.DatabaseController.AddTable(nt, null);
```
- DSN de build alcançável a partir desta máquina de dev: **`TEST_TEB211`** (ODBC, aponta para
  `192.168.1.211`/`tebx3`, schema `TEB`). Credenciais: `sa` / `sage.2022` (ver memória
  `x3-teb-db-connection`).
- Depois de `AddTable`, **limpar as credenciais** antes de gravar (o print engine do X3 fornece-as
  em runtime — não guardar a password no ficheiro final):
  ```csharp
  var cleanLogon = new PropertyBagClass(); cleanLogon.Add("DSN", workDsn); ... // sem UID/PWD
  var cleanCi = new ConnectionInfoClass(); cleanCi.Attributes = cleanAttr; cleanCi.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
  RCD.DatabaseController.ModifyTableConnectionInfo("ALIAS_ESCOLHIDO", cleanCi);
  ```
  Confirmar sempre no fim: `grep -c "<password>" ficheiro.rpt` deve dar `0`.

## Links entre tabelas — StringsClass sempre NOVA

Ao copiar/modificar um link existente, criar SEMPRE uma `StringsClass` nova com `.Add()` para
`SourceFieldNames`/`TargetFieldNames` — reutilizar diretamente a coleção COM de um link já
existente corrompe o join (sintoma: query falha com "argumento inválido para o banco de dados" só
em runtime, sem erro nenhum ao gravar):
```csharp
var srcNames = new StringsClass();
foreach (string f in (System.Collections.IEnumerable)linkExistente.SourceFieldNames) srcNames.Add(f);
```

## Tabelas 1-para-muitos em cabeçalhos de grupo

Um campo de uma tabela ligada 1-para-muitos (ex. linhas de detalhe) colocado no CABEÇALHO DE GRUPO
(que imprime antes da secção Detail) resolve NATURALMENTE para o PRIMEIRO registo do grupo — não
precisa de fórmulas com variáveis partilhadas nem "Underlay Following Sections". Confirmado com
`BPARTNER` ligado a `GACCENTRYD.BPR_0`, mostrado no cabeçalho do documento.

---

## Nome do ficheiro / código do relatório no dicionário X3

**Armadilha crítica**: o ficheiro `.rpt` tem de ser gravado com o NOME EXATO já registado no
dicionário X3 (`AREPORT.CRYCOD_0`). Um código de relatório NOVO (mesmo com AREPORT/AREPORTD/
AREPORTV copiados via `COPRPT`) pode ficar sem dados se o "processo de inicialização"
(`AREPORT.TRTINI_0`) do relatório original tiver lógica AdxTL com `Case ETAT` (código do
relatório) sem `Default`/`Else` — o tratamento simplesmente não faz nada para o código novo, sem
erro nenhum. Confirmado com a família `PIECE`/`JOUGEN`/`LOT` (tratamento `TRTJOULEG` + `RPTLEG`).

A Sage confirma oficialmente (pedido de suporte #74969): para esta família de relatórios, **não é
suportado duplicar o registo no dicionário** — a recomendação é manter o código original e só
trocar o `.rpt` associado. Nem sempre isto é possível (ex. requisito de não tocar no standard);
nesse caso, o fix é criar um subprograma NOVO (prefixo `Z`) que replica o ramo relevante do
tratamento original, adaptado ao código novo — ver `Reports-TEB/ZTRTJOULEG.src` como exemplo
completo e documentado (inclui o truque de forçar a variável local `ETAT` para o código antigo só
no instante de um `Gosub` para um subprograma standard partilhado, quando esse subprograma
TAMBÉM tem lógica não genérica — confirmar sempre primeiro, por SQL, que o `.rpt` alvo não filtra
por `{AREPORTM.RPTCOD_0}` nos joins/record-selection antes de usar este truque).

Diagnóstico: a tabela `AREPORTM` (chave temporária de impressão) tem chave
`NUMREQ+USR+RPTCOD+NUMLIG`. Verificar sempre por SQL se a linha é gerada:
```sql
SELECT RPTCOD_0, COUNT(*), MAX(CREDATTIM_0) FROM TEB.AREPORTM WHERE RPTCOD_0='<codigo>' GROUP BY RPTCOD_0
```

---

## Diagnóstico "imprime mas sem dados"

1. Confirmar via SQL se a linha em `AREPORTM` está a ser gerada (ver acima).
2. Se o cliente tiver acesso ao `GESASU` (Development > Script dictionary > Scripts >
   Subprograms), inserir temporariamente `Infbox "DEBUG valor=[" + variavel + "]"` no AdxTL para
   ver valores em runtime (interativo, X3 real) — remover depois de confirmar.
3. Testar localmente (RAS a partir desta máquina de dev) reapontando TODAS as tabelas para uma
   ligação alcançável (`192.168.1.211`/`tebx3`, OLE DB SQLOLEDB) e, só para teste, afrouxando
   joins `INNER` problemáticos para `LEFT OUTER` (NUNCA no ficheiro final entregue).
4. Tabelas grandes (centenas de milhar de linhas — ex. `GACCENTRY`/`GACCENTRYD`): filtrar já no
   texto do Command SQL do teste (`WHERE TYP_0=... AND NUM_0=...`), senão o teste demora minutos
   a puxar a tabela inteira pela rede sem filtro.
5. `TextOfChapter` (função UFL do X3 para traduções) NUNCA resolve a partir desta máquina de dev
   — um relatório que a use vai sempre falhar a exportação local completa (erro "Unable to find
   language and/or path in general registry"). Isto é NORMAL/esperado, não é regressão — validar
   por inspeção estrutural em vez de tentar renderizar PDF completo localmente.

---

## Validação (sem visualizador gráfico disponível)

Usar sempre `tools/crystal-gen/Inspect-X3Report.ps1` (dump read-only: tabelas, campos, fórmulas,
parâmetros, links, print options, secções/objetos) para confirmar por texto:
- Sem sobreposições: por secção, ordenar objetos por `Left` e confirmar que `Left+Width` de um não
  ultrapassa o `Left` do seguinte.
- Larguras dentro da página (twips: A4 retrato=11906, A4 paisagem=16838).
- Sem credenciais gravadas: `grep -c "<password>" ficheiro.rpt` → `0`.
- Alturas de secção suficientes: maior `Top+Height` de qualquer objeto da secção ≤ altura da
  secção.
- Tabelas/links/fórmulas esperados presentes (comparar contagem antes/depois para apanhar
  remoções acidentais).

### Renderizar PDF para imagem localmente (sem pdftoppm/Ghostscript/ImageMagick)

Esta máquina não tem `pdftoppm`/`gs`/`magick` instalados. Alternativa validada: a API nativa do
Windows `Windows.Data.Pdf` (WinRT), acessível a partir de PowerShell **normal (64-bit)**, sem
instalar nada:
```powershell
Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.Data.Pdf.PdfDocument,Windows.Data.Pdf,ContentType=WindowsRuntime] | Out-Null
[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime] | Out-Null
# ... AsTask() via reflexão sobre System.WindowsRuntimeSystemExtensions para await IAsyncOperation/
# IAsyncAction (Windows PowerShell 5.1 não tem await nativo) — GetFileFromPathAsync, LoadFromFileAsync,
# GetPage(i), page.RenderToStreamAsync(stream, PdfPageRenderOptions) para um InMemoryRandomAccessStream,
# depois copiar bytes para um .png em disco.
```
Dá para renderizar qualquer página a PNG e inspecionar visualmente sem sair do ambiente Windows já
disponível. Não precisa de ser a PowerShell 32-bit do Crystal (esta parte não usa RAS/GAC).

### Subreports de imagem (logo/selo) — imagem empilhada/repetida dezenas de vezes, caixa esticada até ao fim da página

**CORREÇÃO DESTA LIÇÃO (2026-07-28): a nota anterior ("isto é só artefacto de teste local, não
reflete produção") estava ERRADA/incompleta.** O mesmo sintoma visual — a caixa do subreport de
logo/selo a esticar-se verticalmente até ocupar quase a página inteira, com a MESMA imagem
repetida/empilhada dezenas de vezes dentro da mesma caixa, empurrando o resto do layout para
baixo — **aconteceu também em PRODUÇÃO REAL** (via AIMP, `TEB_PIECE.rpt`, depois de um fix anterior
que já tinha corrigido corretamente o `RecordFilter`/`SubreportLinks`). A causa raiz REAL não tem
nada a ver com o `RecordFilter` estar vazio ou errado — o texto da fórmula pode estar 100%
idêntico, byte a byte, ao de um subreport irmão que funciona, e o bug acontece de qualquer forma.

**Causa raiz verdadeira**: a tabela `ABLOB` dentro do subreport de logo/selo era uma
`CommandTableClass` com SQL **sem WHERE nenhum** (`SELECT col1,col2,... FROM TEB.ABLOB`), em vez de
uma tabela NATIVA (`TableClass`) como nos subreports originais que sempre funcionaram. Isto violou
a lição já documentada acima ("Tabelas novas — SEMPRE nativas, nunca Command"), só que desta vez
dentro de um SUBREPORT em vez do relatório principal — a mesma regra aplica-se igualmente aí. Um
`Command` sem filtro nenhum na própria SQL devolve a tabela `ABLOB` INTEIRA (todas as
imagens/selos/anexos de TODAS as empresas/sites/documentos gravados em `ABLOB`, potencialmente
centenas ou milhares de linhas) para o motor de impressão, que depois tenta aplicar o
`RecordSelectionFormula` (`RecordFilter`) *depois* da query já ter devolvido tudo. No motor de
impressão .NET real usado pelo AIMP/print server (ao contrário do que se via no preview local desta
sessão nalguns testes), essa filtragem pós-query sobre uma tabela `Command` sem WHERE não restringe
fiavelmente a 1 registo — e a secção `Detail` do subreport, que teria de imprimir só 1 vez, imprime
uma vez por cada linha devolvida (potencialmente dezenas), cada uma com a mesma imagem, empilhadas
verticalmente, e a secção cresce (a `Height` declarada é ignorada) para caber todas.

Como se chegou a este estado: ao reaproveitar um subreport de logo já embutido (`logo2`/`logo3`)
para uma NOVA colocação (ex. no cabeçalho), o padrão documentado nesta secção
(`GetSubreport()`→gravar para `.rpt` temporário→`ImportSubreportEx()`) é o correto — mas nesta
sessão, uma tentativa alternativa (`tools/crystal-gen/X3RptBuildLogoSub.cs`) construiu o `.rpt`
"carrier" a partir de um SEED EM BRANCO, reconstruindo a tabela `ABLOB` manualmente como `Command`
(para contornar a falta de conectividade desta máquina de dev ao DSN de produção `ADX_CS_X3V7` —
`SetTableLocation` entre duas `CommandTable` não valida conectividade, ao contrário de uma `Table`
nativa, o que tornou esse atalho tentador). Isso introduziu silenciosamente o problema: a validação
ESTRUTURAL (`Inspect-X3Report.ps1`) não apanha isto porque não inspeciona `ClassName`/`CommandText`
das tabelas DENTRO de subreports, só as do relatório principal — e o `RecordFilter`/`SubreportLinks`
(que SÃO verificados) estavam corretos, escondendo o problema real.

**Diagnóstico**: para cada subreport de imagem, ler `ISCRTable.ClassName` da tabela
`ABLOB`/equivalente (via `subDoc.DatabaseController.Database.Tables[0]`) — deve ser
`"CrystalReports.Table"`. Se for `"CrystalReports.CommandTable"`, é a causa deste bug,
independentemente de o `RecordFilter`/`SubreportLinks` estarem certos. Comparar sempre com um
subreport IRMÃO que já funciona em produção (mesmo princípio já usado para `UseCount`).

**Fix**: `SubreportController.SetTableLocation(subreportName, oldCommandTable, newNativeTable)` —
o mesmo padrão de "Tabelas novas — SEMPRE nativas, nunca Command", mas aplicado ao nível do
subreport em vez do relatório principal. `newNativeTable` construído com o DSN alcançável do dev
(`TEST_TEB211`) só para passar a validação de conectividade do `SetTableLocation` — o DSN exato
usado no build É IRRELEVANTE em produção, porque só interessa que seja uma `TableClass` NATIVA (o
motor de impressão X3 remapeia tabelas nativas para o DSN do folder em runtime,
independentemente do DSN gravado). Depois de `SetTableLocation`, limpar credenciais com
`subDoc.DatabaseController.ModifyTableConnectionInfo(alias, cleanConnectionInfo)` (mesmo
`ModifyTableConnectionInfo` documentado para o relatório principal, disponível também no
`DatabaseController` do subreport). **Nota sobre leitura em sessão viva**: depois do
`SetTableLocation`, reler `ISCRTable.ClassName` na MESMA sessão (antes do `SaveAs`) pode continuar
a mostrar `"CrystalReports.CommandTable"` (mais um caso do padrão já conhecido "propriedade não
reflete o estado real até gravar+reabrir") — a confirmação fiável é sempre depois de
`SaveAs`+reload. Ver `tools/crystal-gen/X3RptFixLogoHdrTable.cs` como exemplo completo validado
(aplicado e confirmado com `Reports-TEB/TEB_PIECE.rpt`: `ClassName` passou de
`CrystalReports.CommandTable` para `CrystalReports.Table` depois do reload, `RecordFilter`
manteve-se idêntico byte a byte a `logo2`/`logo3`, sem credenciais gravadas no ficheiro final).

Nota separada (não a causa deste bug, mas um artefacto real do ambiente de QA local que se mantém
válido): ao testar visualmente repontando a tabela `ABLOB` com um `SELECT *` genérico sem qualquer
filtro por imagem/chave, os subreports de logo podem renderizar como retângulos SEM imagem que se
esticam até ao fim da página — isso É um artefacto do BLOB inválido devolvido pelo repoint
simplificado de teste local, distinto do bug de produção acima (aqui a imagem aparece
repetida/empilhada, não ausente). A validação estrutural de posição/tamanho
(`Inspect-X3Report.ps1`) continua válida para bounding boxes, mas — como agora se confirmou — NÃO
é suficiente para apanhar uma `CommandTable` escondida dentro de um subreport; é preciso
inspecionar explicitamente o `ClassName` das tabelas de CADA subreport, não só das do relatório
principal.

Também útil para este tipo de teste: para forçar um PDF a exportar localmente apesar de fórmulas
com `TextOfChapter` (que nunca resolvem nesta máquina — ver acima), pode substituir-se
temporariamente o texto dessas fórmulas por um literal, SÓ numa cópia de QA descartável (nunca no
ficheiro entregue):
```csharp
var newF = new FormulaFieldClass();
newF.Name = ff.Name; newF.Text = "'[QA:" + ff.Name + "]'";
rcd.DataDefController.FormulaFieldController.Modify(ff, newF);
```

### Subreport NOVO com tabela própria usando o MESMO alias de uma tabela já existente no relatório principal — colisão de namespace, ERR 504 "Missing parameter values"

Caso `TEB_REC.rpt` (2026-07-28): depois de adicionar o subreport `RecPendHist` (construído do zero
com uma tabela nativa `PAYMENTD`, seguindo À RISCA o padrão já documentado acima — `SetSubreportLinks`
via API dedicada, `RecordFilterController.SetFormulaText`, tabela nativa não-Command, sem parâmetro
pré-criado antes do link), o print engine Crystal .NET real (AIMP) continuou a falhar com "ERR 504 —
Valores de parâmetro ausentes", exatamente como o bug já corrigido em `TEB_PIECE.rpt`. Um diagnóstico
exaustivo (`UseCount=2`/`PromptToUser=True` em todos os 3 parâmetros do subreport, `SubreportLinks`
presentes e idênticos via propriedade E via `SubreportController.GetSubreportLinks`, `RecordFilter`
escrito corretamente, tabela `ClassName=CrystalReports.Table` nativa, todas as propriedades reflectidas
de `ISCRParameterField` idênticas ao padrão do subreport irmão `IVAC` já funcional) não mostrou
NENHUMA diferença estrutural — o subreport parecia 100% saudável pelo mesmo critério que já tinha
resolvido o bug de `TEB_PIECE.rpt`.

**Causa raiz encontrada**: a tabela nativa `PAYMENTD` DENTRO do subreport `RecPendHist` tinha o
MESMO `Name` E `Alias` ("PAYMENTD") de uma tabela já existente no relatório PRINCIPAL (também
`PAYMENTD`, usada pelo dataset principal do recibo). Comparando com os OUTROS subreports do mesmo
`.rpt` (`IVA`/`IVAC` usam a tabela `PAYVAT` — nome que NUNCA aparece no relatório principal;
`logo.rpt` usa `ABLOB` — também nunca usada no principal), `RecPendHist` era o ÚNICO subreport de
todo o documento cuja tabela própria colide de nome/alias com uma tabela do relatório que o contém.
Isto nunca aconteceria construindo o subreport pelo Designer do Crystal (que renomeia automaticamente
o alias em caso de colisão ao inserir um subreport) — só acontece porque a tabela foi adicionada
programaticamente via `DatabaseController.AddTable` com o alias literal da tabela original, sem
verificar colisão com o documento que a vai конter.

**Fix**: reconstruir o subreport com um alias distinto para a tabela própria (`PAYMENTD_HIST` em vez
de `PAYMENTD`), mantendo o `MainReportFieldName` dos `SubreportLink`s inalterado (continua a apontar
para a tabela `PAYMENTD` do relatório PRINCIPAL — está correto, é a fonte do valor) e atualizando
apenas o `SubreportFieldName`/`RecordFilter`/fórmulas internas do subreport para referenciar o novo
alias:
```csharp
var nt = new TableClass();
nt.Name = "PAYMENTD";           // nome real da tabela — mantém-se
nt.Alias = "PAYMENTD_HIST";     // alias ÚNICO no documento — evita colisão com o principal
...
// no link: MainReportFieldName aponta para o principal, SubreportFieldName para o alias novo
lk.MainReportFieldName = "{PAYMENTD.VCRNUM_0}";       // tabela do relatório PRINCIPAL
lk.SubreportFieldName  = "{PAYMENTD_HIST.VCRNUM_0}";  // tabela PRÓPRIA do subreport, alias novo
```
Ferramentas: `tools/crystal-gen/X3RptBuildRecPendHist3.cs` (constrói o subreport com alias
`PAYMENTD_HIST`) + `X3RptAddRecPendHist2.cs`/`Add-RecPendHist2.ps1` (importa e liga com o alias
novo). Não foi possível confirmar 100% em ambiente real (sem acesso à base de dados de produção
`GX3APP` a partir desta máquina de dev — só se conseguiu reproduzir o MESMO texto de erro
localmente via `ReportDocument.Export()` sem ligação à BD, mas esse teste local falha igualmente
para a versão ANTIGA/já funcional em produção, portanto não discrimina causa); a correção foi
validada apenas estruturalmente (mesmo padrão são/são de `IVAC`, tabela nativa confirmada,
credenciais limpas, sem sobreposições, `SubreportLinks` e `RecordFilter` corretos). Regra geral a
aplicar sempre a partir de agora: ao criar uma tabela nativa NOVA dentro de um subreport via
`AddTable`, escolher SEMPRE um `Alias` que não exista em MAIS NENHUM outro table (nem no relatório
principal, nem nos outros subreports do mesmo documento) — nunca reutilizar o `Name` da tabela

**ATUALIZAÇÃO (2026-07-28, mesmo dia): este fix de alias NÃO resolveu o problema em produção.**
Job seguinte (Job 33) deu EXATAMENTE o mesmo erro "ERR 504 — Valores de parâmetro ausentes", com o
alias já corrigido para `PAYMENTD_HIST`. Um segundo diagnóstico, desta vez MUITO mais profundo
(dump completo de TODAS as propriedades de `ISCRParameterField` via reflexão sobre a interface
inteira — não só `UseCount`/`PromptToUser` — comparando propriedade a propriedade o parâmetro
`Pm-PAYMENTD.NUM_0` do subreport `RecPendHist` contra o mesmo parâmetro do subreport irmão `IVAC`
já funcional; confirmação de ONDE vive fisicamente cada parâmetro, `DataDefinition` do subreport
vs do relatório principal — igual para os dois; reflexão sobre TODOS os métodos de
`ISCRSubreportController`, não só `SetSubreportLinks`/`GetSubreportLinks`) não encontrou NENHUMA
diferença, em NENHUMA propriedade, entre o subreport avariado e o subreport saudável. Conclusão:
**a via subreport-com-parâmetro-ligado, construída programaticamente via RAS SDK, é
estruturalmente indistinguível de uma versão saudável e MESMO ASSIM falha em produção real** — ou
seja, o defeito não está em nenhuma propriedade inspecionável pelo RAS SDK, o que torna esta
abordagem inerentemente pouco fiável para reproduzir programaticamente (2 tentativas falhadas
nesta sessão, ambas "estruturalmente perfeitas" e ambas falhas em produção). **Recomendação: evitar
subreports com parâmetros ligados construídos via RAS SDK quando existir alternativa nativa (ver
secção seguinte); só usar esta técnica quando não houver mesmo outra forma de obter o dado.**

### Alternativa preferível a subreport-com-parâmetro: JOIN nativo já existente no relatório principal

Continuação do caso acima (`TEB_REC.rpt`): o requisito de negócio era mostrar corretamente o "valor
pendente" (`valorpend`) de uma fatura paga em várias prestações — a fórmula original `valorLiq`
(`Sum({PAYMENTD.AMTLIN_0}, {PAYMENTD.VCRNUM_0})`) só via as linhas do recibo ATUAL (o dataset do
relatório já vem filtrado por `AREPORTM.CLEA1_0` = número do recibo a imprimir), nunca o acumulado
de todos os recibos anteriores contra a mesma fatura.

Em vez de construir um subreport novo (frágil, ver acima), havia já um fix escrito e nunca aplicado
numa sessão anterior (`tools/crystal-gen/X3RptFixRecPendente.cs`/`Fix-RecPendente.ps1`) que usa
`GACCDUDATE.PAYCUR_0` — campo NATIVO já disponível via um JOIN que já existia no relatório principal
(`PAYMENTD.VCRNUM_0`/`DUDNUM_0` → `GACCDUDATE.NUM_0`/`ACCNUM_0`, `crTableJoinTypeEqualJoin`, visível
em `Inspect-X3Report.ps1` desde a versão original do ficheiro) — mantido pelo próprio X3 a cada
reconciliação de pagamento, sem necessidade de subreport nenhum, sem parâmetro novo nenhum:
```csharp
newF.Text =
  "if ({GACCDUDATE.SNS_0} = 1 or {GACCDUDATE.SNS_0} = -1) then\r\n" +
  "{GACCDUDATE.PAYCUR_0} * {GACCDUDATE.SNS_0}\r\n" +
  "else\r\n" +
  "{GACCDUDATE.PAYCUR_0}";
rcd.DataDefController.FormulaFieldController.Modify(target, newF);  // target = formula "valorLiq"
```
**Confirmado por query SQL direta** (`TEST_TEB211`/`tebx3`, fatura `FSE-E012601/0182` com 7 recibos
parciais reais): `GACCDUDATE.PAYCUR_0` = soma exata de TODAS as linhas `PAYMENTD.AMTLIN_0` dessa
fatura à data de HOJE (3314.43 = 338.24+689.14+787.58+775.45+682.35+41.67+0). Ou seja, `PAYCUR_0`
é o acumulado "à data de agora", não um valor point-in-time amarrado a um recibo específico — isto
resolve corretamente o caso normal (documento impresso logo a seguir a ser criado, sem recibos
posteriores ainda existentes: nesse instante "à data de agora" == "até e incluindo este recibo").
Só na reimpressão tardia de um recibo ANTIGO, depois de mais pagamentos terem sido feitos entretanto
contra a mesma fatura, é que este valor deixa de refletir o estado histórico exato desse recibo (mostra
o estado atual da fatura, não o estado em que estava nesse momento) — um trade-off aceitável dado o
ganho de robustez, documentado aqui para decisão consciente caso o requisito de reimpressão histórica
volte a ser pedido explicitamente.

**Validado**: aplicado sobre o `TEB_REC.rpt` ORIGINAL (73728 bytes, antes de qualquer subreport),
diff estrutural completo via `Inspect-X3Report.ps1` mostrou UMA ÚNICA diferença em todo o documento
— o texto da fórmula `valorLiq` — zero tabelas novas, zero parâmetros novos, zero subreports novos,
zero credenciais gravadas. Esta é a lição geral a aplicar: **antes de recorrer a um subreport com
parâmetro ligado para "trazer" um valor de outra tabela, verificar primeiro se essa tabela já não
está acessível via JOIN direto no relatório principal** (mesmo que não pelo campo óbvio — aqui
`GACCDUDATE` já estava juntada e só faltava usar o campo certo, `PAYCUR_0`, em vez de recalcular
a partir do dataset já filtrado). Um JOIN nativo elimina inteiramente a classe de bugs "Missing
parameter values" associada a subreports com parâmetros ligados.

Ferramentas: `tools/crystal-gen/X3RptFixRecPendente.cs` + `tools/crystal-gen/Fix-RecPendente.ps1`
(já existiam de uma sessão anterior, nunca tinham sido aplicados) — aplicados sobre uma cópia do
`TEB_REC.rpt` original e confirmados válidos; ficheiro final gravado em `Reports-TEB/TEB_REC.rpt`
(76800 bytes). O subreport `RecPendHist` e toda a infraestrutura associada (`X3RptBuildRecPendHist*.cs`,
`X3RptAddRecPendHist*.cs`) foram ABANDONADOS — mantidos no repositório só como registo histórico do
que foi tentado e não resultou.

### Reutilizar a mesma fórmula para "valor exibido na linha" E "valor usado no cálculo do total" — bug de dados em produção real, não estrutural

Continuação do caso acima (`TEB_REC.rpt`): logo no primeiro teste real em produção do fix
`GACCDUDATE.PAYCUR_0`, apareceu um bug de DADOS (não de estrutura/parâmetros — o ficheiro imprime
sem erro nenhum, mas mostra números errados). Caso concreto reportado com screenshot: recibo
`RPMB-26E01/01682` (cliente `T002925`), linha de detalhe da fatura `FTR-E0126/002850` mostrava
"Valor Pago = 0,25" e "Valor Pendente = 0,00", mas o rodapé ("Total liquidado"/"Valor recebido")
mostrava corretamente "0,10" — inconsistência óbvia (o recibo só tem uma linha e recebeu 0,10, não
podia ter "pago" 0,25 nessa linha).

**Confirmado por SQL direto** num servidor de teste adicional (`192.168.1.204\SQLDEV`, BD `teb`,
mesmo padrão de schema `TEB.TABELA`): a fatura `FTR-E0126/002850` (`GACCDUDATE.ACCNUM_0=190624`)
tinha DOIS recibos parciais contra ela — `RPMB-26E01/01682` (0,10) e `RPMB-26E01/01683` (0,15),
soma exata 0,25 = `GACCDUDATE.PAYCUR_0` (fatura já totalmente liquidada `DUDSTA_0=2`). Ou seja,
`PAYCUR_0` mostrou corretamente o acumulado "a data de hoje" (0,25) — o BUG não estava no valor de
`PAYCUR_0` em si (que está correto para o que representa), mas em USAR ESSE MESMO valor também
para preencher a coluna "Valor Pago" da linha de detalhe, que precisa de granularidade DIFERENTE:
quanto ESTE recibo especificamente pagou a ESTE documento (`PAYMENTD.AMTLIN_0` = 0,10), não o
acumulado de TODOS os recibos contra a mesma fatura até hoje.

**Causa raiz exata**: a fórmula `valorLiq` (alterada no fix anterior para usar `GACCDUDATE.PAYCUR_0`)
era usada em DOIS sítios com necessidades DIFERENTES:
1. o campo visível `valorLiq1` (coluna "Valor Pago" da linha) — precisa do valor **deste recibo
   específico** (`PAYMENTD.AMTLIN_0`), granularidade de linha;
2. a fórmula `valorpend` (`valorDoc - valorLiq`) — precisa do **acumulado** para refletir faturas
   pagas em várias prestações (razão de ser do fix anterior).
O rodapé (`total_liquidado = {PAYMENTH.AMTBAN_0} + Sum({@descontos}, {AREPORTM.NUMLIG_0})`) nunca
usa `valorLiq`/`GACCDUDATE` — usa diretamente o cabeçalho do recibo (`PAYMENTH.AMTBAN_0`), por isso
sempre esteve correto e serviu de referência fiável para detetar a discrepância.

**Fix**: separar em DUAS fórmulas distintas — `valorLiq` mantém-se inalterada (`PAYCUR_0`-based,
usada só por `valorpend`); nova fórmula `valorLiqLinha` (`PAYMENTD.AMTLIN_0`, mesmo padrão de sinal
condicional por `GACCDUDATE.SNS_0` já usado em `valorDoc`/`valorLiq`) passa a alimentar o campo
visível da linha. **Nota de API**: `ReportObjectController.Modify()` REJEITA mudar o `DataSource`
de um `FieldObject` já existente ("Impossível alterar a origem de dados de objetos de campo") —
mais uma limitação de tipo além das já documentadas para `Box`/`Line`/`Subreport`. Contorno: `Remove()`
do campo antigo + `Add()` de um `FieldObjectClass` NOVO na MESMA posição/tamanho com a fórmula nova
(`Add()` de `FieldObjectClass` funciona sempre, ver lição "Texto estático" acima):
```csharp
rcd.ReportDefController.ReportObjectController.Remove(fldAntigo);
var novo = new FieldObjectClass();
novo.DataSource = "{@valorLiqLinha}";
novo.Left = l; novo.Top = t; novo.Width = w; novo.Height = h;   // mesma posicao do antigo
rcd.ReportDefController.ReportObjectController.Add(novo, seccao, -1);
```
**Trade-off residual, comunicado e não assumido silenciosamente**: `valorpend` continua a usar o
acumulado "a data de hoje" (`PAYCUR_0`), não um acumulado point-in-time "até e incluindo este
recibo". No caso real testado isto dá um resultado correto (0,00 pendente = fatura de facto já
liquidada por completo), mas o par "Valor Pago" (0,10, só este recibo) + "Valor Pendente" (0,00,
estado atual da fatura) não soma aritmeticamente ao "Valor Documento" (0,25) da forma ingénua que
um leitor poderia esperar — são duas métricas de granularidade diferente (esta transação vs. estado
atual da fatura). Ficou por confirmar explicitamente com o utilizador se esta semântica é a
pretendida ou se "Valor Pendente" deveria antes refletir apenas "depois desta transação, ignorando
outras" (o que reintroduziria o bug original que motivou todo este trabalho, para faturas com mais
de um recibo).

Ferramentas: `tools/crystal-gen/X3RptFixValorPagoLinha.cs` + `Fix-ValorPagoLinha.ps1`.

**ATUALIZAÇÃO (2026-07-28): o utilizador clarificou o requisito de negócio — é o OPOSTO do que se
assumiu acima.** O utilizador quer o acumulado HISTÓRICO até e incluindo cada recibo (point-in-time,
"quanto faltava pagar depois deste recibo especificamente"), não o estado atual da fatura. Caso real:
fatura com 2 recibos (0,10 depois 0,15, total 0,25) — reimprimir o 1º recibo deve mostrar Valor
Pendente=0,15 (não 0,00). Isto é exatamente a lógica que o subreport `RecPendHist` abandonado tentava
implementar. Antes de reviver o subreport, investigaram-se duas alternativas nativas:

1. **SQL Expression Field — CONFIRMADO INVIÁVEL via RAS SDK.** Pesquisa exaustiva por reflexão em
   TODAS as assemblies RAS carregadas (`CommonObjectModel`, `DataDefModel`, `ReportDefModel`,
   `Controllers`) por qualquer tipo/membro com "SQLExpression" no nome: só existem 2 tipos, ambos em
   `CrystalDecisions.CrystalReports.Engine` (a API de alto nível "Engine", não o RAS `ClientDocument`
   usado em todos os scripts deste projeto) — `SQLExpressionFieldDefinition`/`SQLExpressionFieldDefinitions`,
   e são **só de LEITURA** (apenas getters, a coleção não tem `Add`/`Remove`/`Modify`). `ISCRDataDefinition`
   (o modelo RAS usado por `ClientDocument`) não tem sequer uma propriedade `SQLExpressionFields` —
   só `FormulaFields`/`ParameterFields`/`RecordFilter`/etc. Conclusão: **SQL Expression Fields só podem
   ser criados através do Crystal Reports Designer completo (aplicação IDE), nunca programaticamente
   via RAS SDK** — via não disponível neste pipeline headless.

2. **VIEW nativa na BD — tecnicamente validada, mas é uma alteração de INFRAESTRUTURA fora do âmbito
   de só editar o `.rpt`.** A subquery correlacionada
   `SELECT SUM(p2.AMTLIN_0) FROM TEB.PAYMENTD p2 WHERE p2.VCRNUM_0=p.VCRNUM_0 AND p2.DUDNUM_0=p.DUDNUM_0
   AND p2.NUM_0<=p.NUM_0` foi confirmada por SQL real no servidor de teste `192.168.1.204\SQLDEV`
   (fatura `FTR-E0126/002850`: recibo `RPMB-26E01/01682`→0,10, recibo `RPMB-26E01/01683`→0,25 — os
   valores EXATOS esperados pelo utilizador). Criar uma VIEW real (`TEB.VPAYMENTD_CUM`) com esta
   subquery e juntá-la como tabela NATIVA (`TableClass`, não `Command` — Crystal não distingue VIEW de
   TABLE via ODBC) ao relatório principal seria tecnicamente robusto (evita a classe de bugs de
   subreport-com-parâmetro E a classe de bugs de `CommandTable`/DSN). MAS: isto exige **criar um
   objeto novo na base de dados de PRODUÇÃO** (`GX3APP`, inacessível a partir desta máquina de dev),
   uma alteração de esquema que está fora do âmbito de "só editar o `.rpt`" e que o utilizador teria de
   aplicar manualmente (ou via IDE/DBA do X3) antes do relatório funcionar. **Bloqueador prático a
   confirmar com o utilizador antes de investir mais tempo nesta via** — não avançar sem essa
   confirmação explícita.

3. **Fix aplicado: reviver o subreport `RecPendHist` (v3, alias `PAYMENTD_HIST`) com uma variável
   NOVA nunca testada — `DefaultValues` explícitos nos 3 parâmetros `Pm-...`.** Descoberta relevante
   no caminho: **os parâmetros de um subreport criados por `SetSubreportLinks` NÃO aparecem na
   coleção `DataDefinition.ParameterFields` NA MESMA SESSÃO, mesmo relendo com `GetSubreport()` outra
   vez** (mostra 0 parâmetros) — só existem de facto depois de `SaveAs`+reload (mesmo padrão já
   documentado noutras propriedades: "não reflete o estado real até gravar+reabrir", mas desta vez
   afetando a própria EXISTÊNCIA dos objetos parâmetro, não só valores de propriedades). Por isso, para
   definir `DefaultValues` nestes parâmetros é preciso um processo em DUAS PASSAGENS: 1) importar o
   subreport + `SetSubreportLinks` + `RecordFilterController.SetFormulaText` + `SaveAs`; 2) CARREGAR
   DE NOVO esse ficheiro já gravado (só aí os parâmetros aparecem na coleção) e só então
   `ParameterFieldController.Modify` para acrescentar o `DefaultValues`.
   Notas de API do `Modify(Object OldParameterField, ParameterField NewParameterField)`:
   - Erro "A propriedade ReportName dos campos de parâmetro novo e antigo deve ser a mesma" se
     `newPf.ReportName` não for copiado do parâmetro antigo (`newPf.ReportName = pf.ReportName;`,
     igual ao nome do subreport, ex. "RecPendHist").
   - Erro "essa opção de parâmetro é somente leitura e não pode ser alterada" (só aparece no
     `SaveAs`, não no `Modify` em si) se se sobrescrever `ParameterType`/`AllowNullValue` com valores
     hardcoded em vez de copiar do parâmetro ORIGINAL (`newPf.ParameterType = pf.ParameterType;
     newPf.AllowNullValue = pf.AllowNullValue;`) — mudar estas duas propriedades espec��ficas num
     parâmetro AUTO-CRIADO por um `SubreportLink` parece marcar alguma flag interna como
     "opção somente leitura", só detetada na serialização final.
   ```csharp
   var newPf = new ParameterFieldClass();
   newPf.Name = pf.Name; newPf.Type = pf.Type;
   newPf.ParameterType = pf.ParameterType;      // copiar, NAO hardcode
   newPf.AllowNullValue = pf.AllowNullValue;    // copiar, NAO hardcode
   newPf.ReportName = pf.ReportName;            // OBRIGATORIO, nome do subreport
   var dv = new ParameterFieldDiscreteValueClass(); dv.Value = 0.0; // ou "" para String
   newPf.DefaultValues.Add(dv);
   subDoc.DataDefController.ParameterFieldController.Modify(pf, newPf);
   ```
   Confirmado depois de `SaveAs`+reload: `DefaultValues` passou de `[]` (vazio, igual ao subreport
   `IVAC` "saudável") para uma entrada real (`[System.__ComObject]`) — a PRIMEIRA diferença estrutural
   concreta encontrada nesta investigação inteira entre `RecPendHist` e o padrão de referência.
   **IMPORTANTE — isto NÃO É confirmação de que o fix funciona em produção real**: as 2 tentativas
   anteriores também pareciam estruturalmente perfeitas (byte a byte iguais a `IVAC` em todas as
   propriedades escalares) e mesmo assim falharam 2x em produção real com o mesmo erro. Este `v7`
   aplicado a `Reports-TEB/TEB_REC.rpt` é o candidato mais bem fundamentado até agora, mas **precisa
   de confirmação com um print real (Job novo) antes de se considerar resolvido** — se falhar outra
   vez com o mesmo erro apesar desta mudança, a via subreport-com-parâmetro construída via RAS SDK
   deve considerar-se definitivamente inviável neste ambiente, e as únicas alternativas restantes são
   a VIEW nativa (ponto 2, bloqueador de infraestrutura) ou aceitar a semântica "estado atual" com o
   trade-off já documentado (secção anterior).
   Ferramentas: `tools/crystal-gen/X3RptBuildRecPendHist3.cs` (subreport, alias `PAYMENTD_HIST`,
   inalterado) + `X3RptAddRecPendHist3.cs`/`Add-RecPendHist3.ps1` (importa+liga, 1ª passagem) +
   `X3RptSetDefaultValues.cs`/`SetDefaultValues.ps1` (define `DefaultValues`, 2ª passagem sobre o
   ficheiro já gravado). `valorpend` volta a apontar para `valorLiqHist` (lê o subreport); `valorLiqLinha`
   (fix da secção anterior, "Valor Pago" = só este recibo) mantém-se inalterado.

**ATUALIZAÇÃO (2026-07-28): o fix `DefaultValues` TAMBÉM FALHOU em produção real (Job 39, 3ª
tentativa, mesmo erro exato "Valores de parâmetro ausentes").** Confirmado por análise de um ficheiro
fornecido pelo utilizador (`Diversos/TEB_REC_CrDll_TEB.rpt`, 83456 bytes) que, inspecionado com
`Diag-Rec.ps1`, mostrou EXATAMENTE o mesmo padrão do fix `DefaultValues` aplicado (`DefaultValues =
[System.__ComObject]`, não vazio, igual ao que se tinha construído) — ou seja, este é o ficheiro (ou
uma variante muito próxima) que estava em produção no momento da 3ª falha, provando empiricamente que
o fix `DefaultValues` não resolveu, apesar de ser a primeira diferença estrutural concreta alguma vez
encontrada. **CONCLUSÃO DEFINITIVA depois de 3 tentativas falhadas** (colisão de alias, depois
`DefaultValues`, ambas "estruturalmente saudáveis" e mesmo assim falhas): **subreport com parâmetro
ligado via `SubreportLinks`, construído programaticamente via RAS SDK, NÃO É FIÁVEL neste ambiente de
produção real (AIMP/print server X3), independentemente de quão saudável pareça pelas ferramentas de
inspeção disponíveis via RAS SDK.** Não continuar a tentar variações desta técnica — a causa raiz
exata nunca foi identificada (é provavelmente algo ao nível da serialização binária final do `.rpt`
ou do comportamento do motor de impressão real que o RAS SDK não expõe/reflete de forma alguma,
mesmo por reflexão exaustiva sobre todas as propriedades de `ISCRParameterField` e todos os métodos
de `ISCRSubreportController`).

### Fix definitivo (sem subreport, sem parâmetro novo): self-join nativo com `TableLink` de desigualdade

Alternativa investigada e APLICADA com sucesso a `Reports-TEB/TEB_REC.rpt`: em vez de subreport ou
`SQLExpressionField` (confirmado inviável, ver acima) ou uma `VIEW` nova na BD (bloqueador de
infraestrutura fora do âmbito do `.rpt`), o RAS SDK permite adicionar a mesma tabela `PAYMENTD` UMA
SEGUNDA VEZ com um alias diferente (`PAYMENTD_CUM`) e ligá-la à primeira por um **`TableLink` nativo
com operador de desigualdade** — descoberta chave: `CrTableJoinTypeEnum` (namespace
`CrystalDecisions.ReportAppServer.DataDefModel`) inclui `crTableJoinTypeLessOrEqualJoin` (e
`GreaterThanJoin`/`GreaterOrEqualJoin`/`NotEqualJoin`), não só `EqualJoin`/`OuterJoin` — um `TableLink`
nativo do Crystal SUPORTA `<=` diretamente.

Desenho da solução (nenhum SQL Expression, nenhum Command, nenhum subreport, nenhum parâmetro novo):
1. `AddTable` da tabela `PAYMENTD` uma 2ª vez, `Alias="PAYMENTD_CUM"` (tabela NATIVA, mesmo padrão
   "sempre nativa" já validado).
2. UM `TableLink` nativo (`crTableJoinTypeLeftOuterJoin`) para a parte de IGUALDADE composta
   (`VCRNUM_0`+`DUDNUM_0`) — usar `LeftOuter` em vez de `Equal` para nunca perder a linha principal.
3. A condição de desigualdade (`NUM_0 <= NUM_0`) foi feita via `RecordFilterController.SetFormulaText`
   (append à fórmula de seleção já existente), NÃO via um 2º `TableLink` entre o mesmo par de tabelas
   — evita a incerteza de o Crystal suportar/combinar corretamente 2 `TableLink`s distintos entre as
   MESMAS duas tabelas com `JoinType`s diferentes (não testado, preferiu-se o caminho mais
   convencional): `... and ({PAYMENTD_CUM.NUM_0} <= {PAYMENTD.NUM_0} or IsNull({PAYMENTD_CUM.NUM_0}))`
   (o `IsNull` protege a linha principal caso o `LeftOuterJoin` não encontre nenhuma correspondência).
4. Fórmula `valorLiqHistJoin` = `Sum({PAYMENTD_CUM.AMTLIN_0}, {PAYMENTD.VCRNUM_0})` (sinal condicional
   por `GACCDUDATE.SNS_0`, mesmo padrão de `valorDoc`/`valorLiq`) — o `Sum(campo, campoDeGrupo)` é
   válido porque `{PAYMENTD.VCRNUM_0}` já é um `Group` existente no relatório (Group[2]); o self-join
   multiplica linhas `PAYMENTD_CUM` dentro desse mesmo grupo, e o `Sum` agregado ao nível do grupo
   colapsa-as de volta ao acumulado histórico correto.
5. `valorpend = {@valorDoc}-{@valorLiqHistJoin}`.

**Validado END-TO-END com refresh de dados REAL** (não só estrutural) — a validação mais forte de
toda esta investigação: construiu-se um `.rpt` de teste ISOLADO (só as 2 tabelas `PAYMENTD`/
`PAYMENTD_CUM`, self-join igual ao de produção, filtrado para o caso real) apontado ao servidor de
teste `192.168.1.204\SQLDEV`/`teb` (acessível a partir desta máquina, ao contrário da produção
`GX3APP`), e exportou-se para texto (`ReportDocument.Export`, com logon aplicado via
`Table.ApplyLogOnInfo` em tempo de execução). Resultado: fatura `FTR-E0126/002850` (2 recibos, 0,10 e
0,15) — recibo `RPMB-26E01/01682` deu **0,10** (correto, só este recibo, é o 1º), recibo
`RPMB-26E01/01683` deu **0,25** (correto, acumulado histórico incluindo o anterior) — os EXATOS
valores que o utilizador confirmou como corretos. Esta é a primeira vez nesta investigação inteira
que se conseguiu confirmar o valor calculado com uma query real contra dados reais, em vez de só
validação estrutural (que já se mostrou, 3 vezes, insuficiente para prever comportamento em produção).

Aplicado a `Reports-TEB/TEB_REC.rpt` (78848 bytes) a partir do baseline com o fix `valorLiqLinha`
(76800 bytes, sem subreport). `valorLiqLinha` ("Valor Pago" = só este recibo) mantém-se inalterado.
Sem subreport, sem parâmetro novo, sem `SQLExpressionField`, sem `Command` — só tabela nativa
duplicada + link + fórmula, o padrão mais simples e robusto possível dentro do que o RAS SDK permite.
Ferramentas: `tools/crystal-gen/X3RptSelfJoinCum.cs` + `SelfJoinCum.ps1` (aplica o fix ao relatório
principal) + `X3RptTestSelfJoin.cs` (teste isolado end-to-end usado para a validação com dados reais).

**Nota — ainda por confirmar em produção real** (nenhum fix anterior sobreviveu a essa prova): apesar
da validação end-to-end com dados reais ser muito mais forte que qualquer verificação anterior nesta
investigação, só um print job real no servidor de produção confirma definitivamente. Se este fix
TAMBÉM falhar, as alternativas restantes são: aceitar a semântica "estado atual" (`GACCDUDATE.PAYCUR_0`,
já testada sem erro em produção) ou a `VIEW` nativa `TEB.VPAYMENTD_CUM` (texto SQL já pronto, secção
anterior) — que exige uma alteração de infraestrutura na BD de produção, fora do âmbito de editar o
`.rpt`, e que o utilizador teria de aplicar manualmente.

**ATUALIZAÇÃO (2026-07-28): confirmado progresso real — já não é "Valores de parâmetro ausentes"
(Job 40), mas um erro DIFERENTE e mais específico:**
```
Connection error: Table:PAYMENTD_CUM - Location teb.TEB.PAYMENTD
Falha ao abrir a conexão. [Código do Fornecedor de Banco de Dados: 17]
```
**Causa raiz exata, confirmada propriedade-a-propriedade** (`ISCRTable.ConnectionInfo.Attributes`,
comparando `PAYMENTD` original vs `PAYMENTD_CUM` no `.rpt`): a v1 do self-join tinha usado **OLEDB**
(`Database DLL=crdb_ado.dll`, `QE_DatabaseType=OLE DB (ADO)`) apontando DIRETAMENTE ao servidor de
teste (`QE_ServerDescription=192.168.1.204\SQLDEV`), para evitar depender de um DSN ODBC pré-configurado
nesta máquina. Isto foi um erro: TODAS as outras tabelas nativas deste relatório — `PAYMENTD` original
(`Database DLL=crdb_odbc.dll`, DSN `ADX_REPOSX3`), e `GACCENTRY`/`GACCDUDATE` (mesmo padrão, DSN
`X3TEB` — nome que nem sequer existe nesta máquina de dev!) — usam sempre ODBC. O motor de impressão
X3 só remapeia para o DSN real do folder tabelas nativas ligadas via ODBC; uma tabela ligada via
OLEDB/ADO fica literalmente presa ao servidor gravado no `.rpt`, daí a tentativa (e falha) de o print
server de produção se ligar ao servidor de DEV. Confirma-se assim, de forma ainda mais precisa, a
lição já registada ("o NOME do DSN é irrelevante, só importa ser ODBC") — agora sabe-se também que o
TIPO DE DRIVER (ODBC vs OLEDB) é igualmente crítico, não só o nome do DSN.

**Fix**: reconstruir `PAYMENTD_CUM` com `Database DLL=crdb_odbc.dll`/`QE_DatabaseType=ODBC (RDO)`
(exatamente igual à tabela `PAYMENTD` original e a `GACCENTRY`/`GACCDUDATE`), usando um DSN ODBC
alcançável do dev — usou-se `X3_TEB_DEV` (DSN local já registado, aponta precisamente para
`192.168.1.204\SQLDEV`/`teb`, o mesmo servidor/BD onde o caso real já tinha sido confirmado por SQL).
Revalidado END-TO-END com refresh de dados real via ODBC (não só OLEDB): recibo `RPMB-26E01/01682` →
**0,10**, recibo `RPMB-26E01/01683` → **0,25** — valores idênticos aos já confirmados com OLEDB,
confirmando que a mudança de driver não afeta a lógica, só a fiabilidade da ligação em produção.
Aplicado a `Reports-TEB/TEB_REC.rpt` (77824 bytes). Ferramentas atualizadas:
`tools/crystal-gen/X3RptSelfJoinCum.cs`/`SelfJoinCum.ps1` (agora usa ODBC, parâmetro `WorkDsn` em
vez de `WorkServer`) + `X3RptDiagTableConn.cs`/`DiagTableConn.ps1` (dump completo do `PropertyBag` de
`ConnectionInfo` de uma tabela, usado para encontrar esta causa) +
`X3RptTestSelfJoinOdbc.cs`/`TestSelfJoinOdbc.ps1` (reteste isolado via ODBC).

**Lição geral a partir de agora**: ao adicionar QUALQUER tabela nativa nova, comparar SEMPRE
`Database DLL`/`QE_DatabaseType` (não só `ClassName=CrystalReports.Table`) com uma tabela já
comprovadamente funcional no MESMO relatório antes de dar como concluído — `ClassName` sozinho não
basta para confirmar "nativa E com o driver certo" (uma tabela `OLE DB (ADO)` também mostra
`ClassName=CrystalReports.Table`, é igualmente "nativa" no sentido documentado antes, mas com driver
errado falha da mesma forma que uma `CommandTable` falharia, só que com uma mensagem de erro
diferente — "Falha ao abrir a conexão" em vez de "Falha de logon").

**Nota de ambiente — ficheiro com "user-mapped section" preso**: ao tentar sobrescrever
`Reports-TEB/TEB_REC.rpt` depois de várias invocações consecutivas de `Inspect-X3Report.ps1`/scripts
RAS, `Copy-Item`/`cp` falhou com "The requested operation cannot be performed on a file with a
user-mapped section open" — nem `Get-Process`/`Get-CimInstance` nem a API `RestartManager`
(`RmGetList`, a mesma usada pelo diálogo "ficheiro em uso" do Explorer) encontraram o processo
responsável. Contorno que resultou: `Remove-Item` do ficheiro (que teve sucesso, apesar do
`Copy-Item`/overwrite direto falhar) seguido de `Copy-Item` normal para o mesmo caminho — o
`Remove-Item` liberta a entrada de diretório mesmo com uma secção mapeada em memória pendurada,
permitindo escrever um ficheiro novo no mesmo nome.

Quando o RAS SDK não documenta a assinatura certa de um método (ex. `ImportSubreportEx`), usar
reflexão para a descobrir em vez de adivinhar às cegas:
```csharp
foreach (var mm in typeof(Interface).GetMethods()) {
  if (mm.Name == "MetodoAlvo") {
    var ps = mm.GetParameters();
    foreach (var p in ps) W(p.ParameterType.Name + " " + p.Name);
  }
}
```

---

## Editor genérico guiado por JSON (2026-10-07) — usar ANTES de escrever um .cs novo

`Edit-X3Report.ps1 -Rpt <in> -Ops <spec.json> -Out <out>` (motor `X3RptEdit.cs`; relança-se sozinho em
32-bit; se uma operação falhar não grava nada). Operações: `remove`, `set` (posição/fonte/cor/alinhamento/
borda via Clone+Modify), `label`, `field`, `box`, `formula`, `sectionHeight`, `suppress`, `group`,
`addTable` (nativa, credenciais limpas; password via `"pass": "env:X3_DB_PASS"`), `importSubreport`
(+links+filtro), `selection`. Exemplo completo: `specs/TEB_PIECE_portrait.json`.
Teste visual: `Test-PieceExport.ps1` (ou `Test-RecExport.ps1`) → PDF → `Pdf-ToPng.ps1` → ver PNG.
Dump de grupos/running totals/estilo (fontes, cores, bordas): `Dump-Style.ps1`.

Lições novas descobertas ao construí-lo:
- **Objeto acabado de criar tem `FontColor` nulo** → criar `FontColorClass`+`FontClass` novos em vez de
  `Clone` (senão "Object reference not set").
- **Caixa/linha com fórmula `''` NÃO desenha a borda no export** (o render visual confirmou: nem a
  `hdrMetaBox` antiga aparecia). Usar `ChrW(160)` (espaço não-quebrável) como valor da fórmula.
- **Importar um subreport que JÁ traz parâmetros `Pm-*`** (ex. `Reports-TEB/logo.rpt`) e ligá-lo
  (por `SetSubreportLinks` OU Clone+Modify do objeto de colocação) faz o RAS criar `Pm-X??01` ao gravar e
  deixar os `Pm-X` antigos órfãos → "Valores de parâmetro ausentes". Fix: importar uma cópia
  "carrier" sem `Pm-*` e sem record selection (o editor faz isto automaticamente quando há `links`).
- O `logo.rpt` standalone tem secções de cabeçalho/rodapé de 600 twips não suprimidas → o logo desce e o
  subreport cresce; no carrier suprimir tudo exceto Detail, e tirar a moldura do objeto de colocação.
- **Agrupar por conta "esconde" linhas**: no PIECE a linha está no cabeçalho do grupo `ACC_0` e os
  running totals avaliam na mudança desse grupo — trocar o grupo para `LIN_0` desagrupa e corrige os
  totais de uma vez.
- A área de grupo do documento do PIECE já tem "repetir cabeçalho em cada página"; o `GroupHeaderSection1`
  (referencial) tem supressão condicional — não pôr lá objetos.

## Segurança / âmbito

- Nunca tocar em `Reports-BaseX3/` (referência standard) nem em `Reports-TEB/PIECE.rpt` (standard
  registado) sem instrução explícita.
- Trabalhar sempre numa cópia de teste primeiro (pasta scratch), validar, só depois copiar para o
  ficheiro final em `Reports-TEB/`.
- Nunca commitar/fazer push sem pedido explícito do utilizador.

## TEB_REC (2026-10-07): regressão + fix definitivo do "Valor Pendente"

**Regressão**: o `.rpt` em produção (editado a 24-09 para acrescentar `TABPAYTYP`/`BANK`) partiu de uma
versão ANTERIOR ao fix de julho — voltou a `valorpend = valorDoc - Sum(PAYMENTD.AMTLIN_0)` (só este
recibo). Caso: FTC-E0126/013084 (73,80), recibo anterior 60,00 + recibo RTRF-26E01/03938 13,80 →
imprimia pendente 60,00. **Antes de editar um .rpt, partir SEMPRE da última versão em
`Reports-TEB/` e comparar com o que está em produção.**

**O fix de julho (acumulado dos recibos ANTERIORES, `NUM_0 <=`) também estava errado**, por 3 motivos
confirmados com dados reais:
1. `NUM_0` comparado como texto não é cronológico entre tipos (`RPMB-…` vs `RTRF-…`).
2. O self-join multiplica linhas: recibo com 2 linhas p/ a mesma fatura+vencimento (RECEB+DFINA, ~600
   casos) contava o histórico 2x; descontos/retenções/`total` também multiplicavam.
3. **As conciliações parciais redistribuem o pendente entre faturas SEM linhas PAYMENTD** (RTRF-26E01/
   03869: FTC-013077 dava pendente −653,83 e FTC-013107 ficava com 653,83 que o X3 tem como pago).
   Somar recibos ≠ `GACCDUDATE.PAYCUR_0`.

**Fix (`X3RptRecPendFix.cs` / `Fix-RecPendHist.ps1`)** — só tabela nativa ODBC + link nativo + fórmulas:
- `PAYMENTD_CUM` (LeftOuter VCRNUM_0+DUDNUM_0), seleção: própria linha do recibo **ou
  `CREDATTIM_0 >` (recibos POSTERIORES)** — na impressão normal não há nenhum, o join não acrescenta linhas.
- `pago à data = GACCDUDATE.PAYCUR_0 − Sum(@amtLinLater)/Sum(@selfRow)` (mín. 0); `valorpend = valorDoc − isso`.
- `@selfRow` (=1 só na combinação linha↔ela própria) protege `valorLiq` (Valor Pago), `descontos`,
  `withholdingTax`, `total`.

**Teste end-to-end com dados REAIS do relatório COMPLETO** (novo, muito melhor que testes isolados):
`Test-RecExport.ps1` = `X3RptStubUfl.cs` (cópia de teste com `TextOfChapter`/`AmountToWord` em stub —
fora do print server os UFLs dão "Local menus file not found") + `X3RptExportFull.cs` (remapeia todas as
tabelas p/ DSN `TEST_TEB211`, `Location = TEB.<tabela>`, e usa um pedido de impressão que já existe em
`TEB.AREPORTM` — as linhas ficam lá depois de cada impressão: `usr`/`numedt`/`seqedt=0`).
Validado em 5 recibos (03938, 09830, 03869, 03889, reimpressão 03939) = valores esperados por SQL.

---

## Lisoaz (2026-10-07) — port TEB -> outro cliente, design uniforme, POR/ENG

Pipeline: `Build-Lisoaz.ps1` encadeia specs `LZ_<doc>_port.json` > `LZ_common.json` > `LZ_<doc>_texts.json` > `LZ_<doc>_design.json`
(gerados por `Gen-LZTexts.ps1` / `Gen-LZDesign.ps1`). Validar com `Validate-LZ.ps1` + `Audit-Certification.ps1`; testar com
`Test-AllLZ.ps1 -Lan POR|ENG` (dados reais TEB via AREPORTM).

- **Campos certificados (PT_MENTION*, PT*, ATCUD, QR) e as SECCOES onde estao: nao mexer em nada** (posicao, fonte, visibilidade,
  condicoes, altura). O editor recusa `set`/`remove` em PT* (salvo `force`) e `formulaReplace` salta formulas PT_*/isPt*/atcud*.
  `Validate-LZ.ps1` compara `Dump-SectionFormulas.ps1 -Cert` (TEB vs LZ).
- **Faixas de fundo** (`box` com `back:true` = indice 0 na seccao) ficam por baixo dos textos. Caixas-campo vazias precisam de
  fonte minima (o editor poe size 1): com Arial 10 por omissao o RAS estica-as e a SECCAO cresce (nunca encolhe -> "Altura de seccao invalida").
  Uma caixa com contorno tambem pode fazer crescer a seccao alguns twips: dar folga.
- `field` nasce com Arial 10 e o RAS estica a altura; o editor repoe a geometria num 2.o Modify depois de aplicar a fonte.
- Converter Text fixo -> campo formula: usar `copyFormatFrom` (herda supressao condicional do Text; sem isso aparecem rotulos que deviam
  estar ocultos, ex. "Total de retencoes"). TextObject: ler paragrafos/elementos (`X3RptTexts.cs`); ha textos multi-linha.
- Subreports: ops com `"sub": "<nome>"` (formula/field/remove). O subreport nao ve formulas do principal: publicar pasta/idioma em
  shared vars (`lzDos`/`lzLanS`) numa formula do principal ja avaliada no cabecalho (`textofchapter`).
- Menus locais: `TextOfChapter` aparece tambem como `TextofChapter` (case-insensitive) e com numero dinamico; fórmulas comentadas
  (`//`) contem nomes de UFL. Literais tambem entre plicas ('Taxa') nos subreports.
- Export local: subreports SEM tabelas (so texto, ex. Notacredit) dao "Erro no arquivo" sem detalhe -> suprimidos no teste;
  formulas mortas que referenciam campos inexistentes (alias trocado) dao "Nome de campo invalido" mesmo nao usadas -> neutralizar.
- PowerShell 5.1: scripts com acentos precisam de BOM; `R` e alias de Invoke-History; `(ConvertFrom-Json)` entre parenteses antes
  do pipeline; `.values` sem a chave devolve TODOS os valores; `` e `` sao a mesma variavel; arrays aninhados
  num hashtable literal precisam de `@(,@(...))`.