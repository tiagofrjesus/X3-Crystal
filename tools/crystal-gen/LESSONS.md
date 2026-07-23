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

### Subreports de imagem (logo/selo) em teste local — ABLOB "quebrado" estica a caixa até ao fim da página

Ao gerar um PDF de QA local repontando a tabela `ABLOB` com um `SELECT *` genérico (sem filtrar
pela imagem/chave certa), os subreports de logo renderizaram como retângulos SEM imagem que se
esticam até ao fim da página, ignorando a `Height` declarada — em vez de aparecerem no tamanho
correto. Confirmado removendo os subreports do documento de teste: sem eles, o layout volta ao
normal (1 página só, resto dos objetos no sítio certo). **Isto é um artefacto do BLOB
inválido/errado devolvido pelo repoint simplificado de teste — não reflete o comportamento em
produção** (onde o X3 fornece o BLOB certo via GESATX). Tal como o `TextOfChapter`, é uma limitação
do ambiente de QA local, não uma regressão do `.rpt`. A validação fiável de posição/tamanho destes
objetos continua a ser ESTRUTURAL (`Inspect-X3Report.ps1`: bounding boxes por `Left/Top/Width/
Height` declarados, sem sobreposição), não o render visual local — mas o render visual local
CONTINUA a ser útil para apanhar sobreposições REAIS entre outros objetos (texto, caixas), que não
sofrem deste artefacto.

Também útil para este tipo de teste: para forçar um PDF a exportar localmente apesar de fórmulas
com `TextOfChapter` (que nunca resolvem nesta máquina — ver acima), pode substituir-se
temporariamente o texto dessas fórmulas por um literal, SÓ numa cópia de QA descartável (nunca no
ficheiro entregue):
```csharp
var newF = new FormulaFieldClass();
newF.Name = ff.Name; newF.Text = "'[QA:" + ff.Name + "]'";
rcd.DataDefController.FormulaFieldController.Modify(ff, newF);
```

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

## Segurança / âmbito

- Nunca tocar em `Reports-BaseX3/` (referência standard) nem em `Reports-TEB/PIECE.rpt` (standard
  registado) sem instrução explícita.
- Trabalhar sempre numa cópia de teste primeiro (pasta scratch), validar, só depois copiar para o
  ficheiro final em `Reports-TEB/`.
- Nunca commitar/fazer push sem pedido explícito do utilizador.
