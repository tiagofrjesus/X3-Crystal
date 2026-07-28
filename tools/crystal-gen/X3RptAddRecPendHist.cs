// X3RptAddRecPendHist — importa o subreport RecPendHist.rpt (ja construido/testado por
// X3RptBuildRecPendHist2) para dentro do TEB_REC.rpt, coloca-o (invisivel) em GroupHeaderSection3
// (secao vazia do MESMO grupo cujo footer, GroupFooterSection1, tem os campos valorDoc/valorLiq/
// valorpend), liga os 3 parametros do subreport aos campos da fatura/recibo atuais, e reescreve
// valorpend para usar o acumulado (formula nova valorLiqHist, lendo a shared numbervar
// gValorLiqHist do subreport) em vez de valorLiq. valorLiq NAO se mexe — representa o valor pago
// NESTE recibo especifico (ja correto: o dataset do relatorio principal so tem as linhas deste
// recibo). O bug estava em valorpend, que subtraia valorLiq (so este recibo) em vez do acumulado
// de todos os recibos ja feitos contra a mesma fatura ate e incluindo este.
using System;
using System.Text;
using System.Collections.Generic;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.ReportAppServer.Controllers;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptAddRecPendHist {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  static IEnumerable<ISCRArea> AllAreas(ReportDefinition rd) {
    var l = new List<ISCRArea>();
    l.Add(rd.ReportHeaderArea); l.Add(rd.PageHeaderArea); l.Add(rd.DetailArea);
    l.Add(rd.ReportFooterArea); l.Add(rd.PageFooterArea);
    for (int i = 0; i < 8; i++) {
      try { var a = rd.GroupHeaderArea[i]; if (a != null) l.Add(a); } catch {}
      try { var a = rd.GroupFooterArea[i]; if (a != null) l.Add(a); } catch {}
    }
    return l;
  }
  static Section FindSection(ReportDefinition rd, string name) {
    foreach (var area in AllAreas(rd)) {
      if (area == null) continue;
      foreach (Section s in area.Sections) if (s.Name == name) return s;
    }
    return null;
  }
  static ISCRReportObject FindSubreportObjInSection(Section sec, string subreportName) {
    foreach (ISCRReportObject ro in sec.ReportObjects) {
      if (ro.Kind == CrReportObjectKindEnum.crReportObjectKindSubreport) {
        var sr = (SubreportObject)ro;
        if (sr.SubreportName == subreportName) return ro;
      }
    }
    return null;
  }

  public static string Build(string srcRptPath, string subRptPath, string outPath, string versionLabel) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(srcRptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded " + srcRptPath);

      var rd = rcd.ReportDefinition;
      var targetSec = FindSection(rd, "GroupHeaderSection3");
      if (targetSec == null) throw new Exception("GroupHeaderSection3 nao encontrada");
      Lg("secao alvo encontrada");

      // 1) importar o subreport standalone
      int origHeight = targetSec.Height;
      Lg("altura original da seccao = " + origHeight);
      rcd.SubreportController.ImportSubreportEx("RecPendHist", subRptPath, targetSec, 0, 0, 50, 50);
      Lg("ImportSubreportEx OK (altura da seccao em memoria apos import = " + targetSec.Height + ")");

      // 2) SubreportLinks — usar a API DEDICADA SubreportController.SetSubreportLinks(nome, links),
      //    NAO o Clone+Modify da propriedade SubreportObject.SubreportLinks. Confirmado por
      //    reflexao: ISCRSubreportController expoe GetSubreportLinks/SetSubreportLinks por NOME de
      //    subreport, uma API separada da propriedade de colocacao do objeto — e e esta, não a
      //    propriedade, que o motor de impressao real consulta (a tentativa anterior, via
      //    Clone+Modify da propriedade, produzia uma estrutura visualmente identica a um subreport
      //    saudavel mas continuava a falhar em runtime real com ERR 504).
      var placedObj = FindSubreportObjInSection(targetSec, "RecPendHist");
      if (placedObj == null) throw new Exception("SubreportObject RecPendHist nao encontrado apos import");
      // Padrao confirmado lendo o subreport IVAC ja existente/funcional neste TEB_REC.rpt:
      // SubreportFieldName = campo PROPRIO do subreport (nao um parametro!); LinkedParameterName =
      // "{?Pm-<CampoDoRelatorioPrincipal>}" (nomenclatura auto-gerada do Designer, nao a nossa escolha).
      var newLinks = new SubreportLinksClass();
      Action<string> addLink = (fieldExpr) => {
        var lk = new SubreportLinkClass();
        lk.MainReportFieldName = fieldExpr;
        lk.SubreportFieldName = fieldExpr;
        lk.LinkedParameterName = "{?Pm-" + fieldExpr.Trim('{','}') + "}";
        newLinks.Add(lk);
      };
      addLink("{PAYMENTD.VCRNUM_0}");
      addLink("{PAYMENTD.DUDNUM_0}");
      addLink("{PAYMENTD.NUM_0}");
      rcd.SubreportController.SetSubreportLinks("RecPendHist", newLinks);
      Lg("SetSubreportLinks (3) via SubreportController OK");

      // 3) SO AGORA escrever o RecordFilter do subreport, referenciando os parametros "Pm-..."
      //    que o passo anterior acabou de criar (limpos, sem sufixo, porque nao existia colisao).
      // ToText() no DUDNUM_0: {PAYMENTD.DUDNUM_0} e Int32 no subreport mas o parametro "Pm-..."
      // herda o tipo Number do campo do relatorio principal — comparar como texto elimina
      // qualquer coercao de tipo numerica (Number vs Int32) dependente do driver ODBC/ADO,
      // suspeito de ser a causa de o filtro nao devolver linhas em runtime real (servidor de
      // impressao usa o driver ODBC nativo crdb_odbc.dll, diferente do OLEDB usado nos testes
      // locais — nao confirmado ao certo, mas elimina um vetor de erro plausivel e concreto).
      var subDoc = rcd.SubreportController.GetSubreport("RecPendHist");
      subDoc.DataDefController.RecordFilterController.SetFormulaText(
        "{PAYMENTD.VCRNUM_0} = {?Pm-PAYMENTD.VCRNUM_0} and ToText({PAYMENTD.DUDNUM_0},0,\"\") = ToText({?Pm-PAYMENTD.DUDNUM_0},0,\"\") and {PAYMENTD.NUM_0} <= {?Pm-PAYMENTD.NUM_0}");
      Lg("RecordFilter do subreport escrito");

      // 4) valorLiq NAO se mexe — representa o valor pago NESTE recibo especifico (correto tal
      //    como esta, o dataset do relatorio principal ja so contem as linhas deste recibo). O bug
      //    era em valorpend, que subtraia valorLiq (so este recibo) em vez do acumulado ate este
      //    recibo. Criar uma formula nova "valorLiqHist" com o acumulado (assinado) do subreport,
      //    e apontar valorpend para ela.
      rcd.DataDefController.FormulaFieldController.AddByName(
        "valorLiqHist",
        "shared numbervar gValorLiqHist;\r\n" +
        "if ({GACCDUDATE.SNS_0} = 1 or {GACCDUDATE.SNS_0} = -1) then\r\n" +
        "gValorLiqHist * {GACCDUDATE.SNS_0}\r\n" +
        "else\r\n" +
        "gValorLiqHist",
        CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      Lg("formula valorLiqHist criada");

      ISCRFormulaField pendTarget = null;
      foreach (ISCRFormulaField ff in rcd.DataDefController.DataDefinition.FormulaFields) {
        if (ff.Name == "valorpend") { pendTarget = ff; break; }
      }
      if (pendTarget == null) throw new Exception("formula valorpend nao encontrada");
      Lg("valorpend (antes) = " + pendTarget.Text.Replace("\r\n", " \\n "));
      var newPend = new FormulaFieldClass();
      newPend.Name = pendTarget.Name;
      newPend.Text = "{@valorDoc}-{@valorLiqHist}";
      rcd.DataDefController.FormulaFieldController.Modify(pendTarget, newPend);
      Lg("valorpend (depois) = " + newPend.Text);

      // 5) repor a altura da seccao (crescida por ImportSubreportEx, ex. 220->760, o que introduz
      //    espaco vertical indesejado no recibo). Atribuicao direta de Section.Height NAO persiste
      //    (mesmo padrao ja documentado p/ Left/Top de ReportObject) — usar o controller dedicado.
      try {
        rcd.ReportDefController.ReportSectionController.SetProperty(
          targetSec, CrReportSectionPropertyEnum.crReportSectionPropertyHeight, origHeight);
        Lg("altura da seccao reposta para " + origHeight + " via ReportSectionController.SetProperty");
      } catch (Exception e) { Lg("reset altura ERR: " + e.Message); }

      // 6) label de versao visivel no cabecalho, para confirmar visualmente (no papel impresso)
      //    qual o build que o print server esta mesmo a usar — texto estatico tem de ser
      //    formula-field com literal (SimpleTextObject/Paragraphs fazem CRASH, ver LESSONS.md).
      Section hdrSec = null;
      foreach (Section s in rd.ReportHeaderArea.Sections) { hdrSec = s; break; }
      if (hdrSec != null) {
        string esc = versionLabel.Replace("'", "''");
        rcd.DataDefController.FormulaFieldController.AddByName(
          "fBuildVersion", "'" + esc + "'", CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
        var vf = new FieldObjectClass();
        vf.DataSource = "{@fBuildVersion}"; vf.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeStringField;
        vf.Kind = CrReportObjectKindEnum.crReportObjectKindField;
        vf.Left = 8600; vf.Top = 0; vf.Width = 1700; vf.Height = 200; vf.Name = "fldBuildVersion";
        rcd.ReportDefController.ReportObjectController.Add(vf, hdrSec, -1);
        Lg("label de versao [" + versionLabel + "] adicionada ao ReportHeaderSection1");
      } else { Lg("ReportHeaderSection1 nao encontrada — label de versao NAO adicionada"); }

      string dir = System.IO.Path.GetDirectoryName(outPath); string nm = System.IO.Path.GetFileName(outPath); object od = dir;
      rcd.SaveAs(nm, ref od, 0);
      Lg("saved -> " + outPath);
      eng.Close();
    } catch (Exception ex) {
      Lg("FATAL: " + ex.Message);
      Lg(ex.StackTrace);
    }
    return log.ToString();
  }
}
