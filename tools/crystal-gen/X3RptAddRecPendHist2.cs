// X3RptAddRecPendHist2 -- v2 do fix: identico a X3RptAddRecPendHist.cs, mas usa o RecPendHist3.rpt
// (tabela renomeada para alias PAYMENTD_HIST, sem colisao com o alias "PAYMENTD" ja usado pelo
// relatorio PRINCIPAL). MainReportFieldName dos SubreportLinks continua a apontar para
// {PAYMENTD.X} do relatorio PRINCIPAL (correto, inalterado); SubreportFieldName e o RecordFilter
// passam a referenciar {PAYMENTD_HIST.X} (a tabela PROPRIA do subreport, agora com nome distinto).
using System;
using System.Text;
using System.Collections.Generic;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.ReportAppServer.Controllers;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptAddRecPendHist2 {
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

      int origHeight = targetSec.Height;
      Lg("altura original da seccao = " + origHeight);
      rcd.SubreportController.ImportSubreportEx("RecPendHist", subRptPath, targetSec, 0, 0, 50, 50);
      Lg("ImportSubreportEx OK (altura da seccao em memoria apos import = " + targetSec.Height + ")");

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
      Lg("SetSubreportLinks (3) via SubreportController OK (SubreportFieldName agora referencia PAYMENTD_HIST)");

      var subDoc = rcd.SubreportController.GetSubreport("RecPendHist");
      subDoc.DataDefController.RecordFilterController.SetFormulaText(
        "{PAYMENTD_HIST.VCRNUM_0} = {?Pm-PAYMENTD.VCRNUM_0} and ToText({PAYMENTD_HIST.DUDNUM_0},0,\"\") = ToText({?Pm-PAYMENTD.DUDNUM_0},0,\"\") and {PAYMENTD_HIST.NUM_0} <= {?Pm-PAYMENTD.NUM_0}");
      Lg("RecordFilter do subreport escrito (referencia PAYMENTD_HIST)");

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

      try {
        rcd.ReportDefController.ReportSectionController.SetProperty(
          targetSec, CrReportSectionPropertyEnum.crReportSectionPropertyHeight, origHeight);
        Lg("altura da seccao reposta para " + origHeight + " via ReportSectionController.SetProperty");
      } catch (Exception e) { Lg("reset altura ERR: " + e.Message); }

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
