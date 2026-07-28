// X3RptAddRecPendHist3 -- igual ao v2 (X3RptAddRecPendHist2.cs), mas testa a hipotese nova:
// definir explicitamente um DefaultValue (0) nos 3 parametros "Pm-..." do subreport RecPendHist
// via ParameterFieldController.Modify, mesmo sabendo que o subreport IVAC (funcional) NAO tem
// nenhum DefaultValue definido -- teste rapido e barato antes de descartar a via subreport.
using System;
using System.Text;
using System.Collections.Generic;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.ReportAppServer.Controllers;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptAddRecPendHist3 {
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

  public static string Build(string srcRptPath, string subRptPath, string outPath) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(srcRptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded " + srcRptPath);

      var rd = rcd.ReportDefinition;
      var targetSec = FindSection(rd, "GroupHeaderSection3");
      if (targetSec == null) throw new Exception("GroupHeaderSection3 nao encontrada");
      int origHeight = targetSec.Height;

      rcd.SubreportController.ImportSubreportEx("RecPendHist", subRptPath, targetSec, 0, 0, 50, 50);
      Lg("ImportSubreportEx OK");

      var placedObj = FindSubreportObjInSection(targetSec, "RecPendHist");
      if (placedObj == null) throw new Exception("SubreportObject RecPendHist nao encontrado apos import");
      var newLinks = new SubreportLinksClass();
      Action<string,string> addLink = (mainExpr, subExpr) => {
        var lk = new SubreportLinkClass();
        lk.MainReportFieldName = mainExpr;
        lk.SubreportFieldName = subExpr;
        lk.LinkedParameterName = "{?Pm-" + mainExpr.Trim('{','}') + "}";
        newLinks.Add(lk);
      };
      addLink("{PAYMENTD.VCRNUM_0}", "{PAYMENTD_HIST.VCRNUM_0}");
      addLink("{PAYMENTD.DUDNUM_0}", "{PAYMENTD_HIST.DUDNUM_0}");
      addLink("{PAYMENTD.NUM_0}",    "{PAYMENTD_HIST.NUM_0}");
      rcd.SubreportController.SetSubreportLinks("RecPendHist", newLinks);
      Lg("SetSubreportLinks OK");

      var subDoc = rcd.SubreportController.GetSubreport("RecPendHist");
      subDoc.DataDefController.RecordFilterController.SetFormulaText(
        "{PAYMENTD_HIST.VCRNUM_0} = {?Pm-PAYMENTD.VCRNUM_0} and ToText({PAYMENTD_HIST.DUDNUM_0},0,\"\") = ToText({?Pm-PAYMENTD.DUDNUM_0},0,\"\") and {PAYMENTD_HIST.NUM_0} <= {?Pm-PAYMENTD.NUM_0}");
      Lg("RecordFilter escrito");

      // *** TESTE NOVO: definir DefaultValues explicitos nos 3 parametros do subreport ***
      // reler o subreport (subDoc antigo pode estar cacheado sem os parametros recem-criados
      // pelo SetSubreportLinks, mesmo padrao "leitura nao reflete estado ate reler" ja
      // documentado noutras propriedades em LESSONS.md)
      var subDocFresh = rcd.SubreportController.GetSubreport("RecPendHist");
      int paramCount = 0;
      foreach (ISCRParameterField pfc in subDocFresh.DataDefController.DataDefinition.ParameterFields) paramCount++;
      Lg("parametros encontrados no subreport (fresh): " + paramCount);
      int okCount = 0;
      foreach (ISCRParameterField pf in subDocFresh.DataDefController.DataDefinition.ParameterFields) {
        try {
          var newPf = new ParameterFieldClass();
          newPf.Name = pf.Name;
          newPf.Type = pf.Type;
          newPf.ParameterType = CrParameterFieldTypeEnum.crParameterFieldTypeReportParameter;
          newPf.AllowNullValue = true;
          var dv = new ParameterFieldDiscreteValueClass();
          if (pf.Type == CrFieldValueTypeEnum.crFieldValueTypeNumberField) dv.Value = 0.0;
          else dv.Value = "";
          newPf.DefaultValues.Add(dv);
          subDocFresh.DataDefController.ParameterFieldController.Modify(pf, newPf);
          okCount++;
          Lg("DefaultValue definido para " + pf.Name);
        } catch (Exception e) { Lg("DefaultValue FALHOU para " + pf.Name + ": " + e.Message); }
      }
      Lg("total DefaultValues definidos: " + okCount);

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
      var newPend = new FormulaFieldClass();
      newPend.Name = pendTarget.Name;
      newPend.Text = "{@valorDoc}-{@valorLiqHist}";
      rcd.DataDefController.FormulaFieldController.Modify(pendTarget, newPend);
      Lg("valorpend apontado para valorLiqHist");

      try {
        rcd.ReportDefController.ReportSectionController.SetProperty(
          targetSec, CrReportSectionPropertyEnum.crReportSectionPropertyHeight, origHeight);
        Lg("altura da seccao reposta");
      } catch (Exception e) { Lg("reset altura ERR: " + e.Message); }

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
