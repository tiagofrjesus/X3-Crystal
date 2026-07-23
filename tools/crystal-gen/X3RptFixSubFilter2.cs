// X3RptFixSubFilter2 — repara COMPLETAMENTE os subreports novos logoHdr2/logoHdr3 do
// TEB_PIECE.rpt: o roundtrip GetSubreport()->SaveAs()->ImportSubreportEx() usado para os criar
// perdeu DUAS coisas em relacao ao subreport original (logo2/logo3):
//   1) RecordFilter.FreeEditingText do subreport (filtro por ABLOB) — ficou vazio.
//   2) SubreportLinks do SubreportObject de colocacao no relatorio principal (a ligacao explicita
//      que partilha o VALOR do parametro {?X3DOS} do relatorio principal com o parametro homonimo
//      do subreport) — ficou com 0 links (o original logo2/logo3 tem sempre exatamente 1 link
//      MainReportFieldName={?X3DOS} -> SubreportFieldName={?X3DOS}, LinkedParameterName={?X3DOS}).
// As DUAS faltas em conjunto sao a causa do ERR 504 "Missing parameter values" em runtime real
// (o motor Crystal .NET real, ao contrario do preview local, exige as duas coisas).
using System;
using System.Text;
using System.Collections.Generic;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptFixSubFilter2 {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  static IEnumerable<ISCRArea> AllAreas(ReportDefinition rd) {
    var l = new List<ISCRArea>();
    l.Add(rd.ReportHeaderArea); l.Add(rd.PageHeaderArea); l.Add(rd.DetailArea);
    l.Add(rd.ReportFooterArea); l.Add(rd.PageFooterArea);
    for (int i = 0; i < 5; i++) {
      try { var a = rd.GroupHeaderArea[i]; if (a != null) l.Add(a); } catch {}
      try { var a = rd.GroupFooterArea[i]; if (a != null) l.Add(a); } catch {}
    }
    return l;
  }
  static ISCRReportObject Find(ReportDefinition rd, string name) {
    foreach (var area in AllAreas(rd)) {
      if (area == null) continue;
      foreach (Section s in area.Sections) foreach (ISCRReportObject ro in s.ReportObjects) if (ro.Name == name) return ro;
    }
    return null;
  }

  public static string Build(string rptPath, string outPath) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded");

      // 1) RecordFilter — via ISCRFilterController.SetFormulaText (a atribuicao direta de
      //    RecordFilter.FreeEditingText NAO persiste ao gravar, confirmado nesta sessao).
      var srcLogo2 = rcd.SubreportController.GetSubreport("logo2");
      var srcLogo3 = rcd.SubreportController.GetSubreport("logo3");
      string filter2 = srcLogo2.DataDefController.DataDefinition.RecordFilter.FreeEditingText;
      string filter3 = srcLogo3.DataDefController.DataDefinition.RecordFilter.FreeEditingText;
      Lg("filtro logo2 lido");
      Lg("filtro logo3 lido");

      var hdr2 = rcd.SubreportController.GetSubreport("logoHdr2");
      var hdr3 = rcd.SubreportController.GetSubreport("logoHdr3");
      hdr2.DataDefController.RecordFilterController.SetFormulaText(filter2);
      Lg("logoHdr2 RecordFilter reparado");
      hdr3.DataDefController.RecordFilterController.SetFormulaText(filter3);
      Lg("logoHdr3 RecordFilter reparado");

      // 2) SubreportLinks — Subreport4 (logoHdr2) / Subreport5 (logoHdr3) no relatorio PRINCIPAL
      //    precisam do mesmo link {?X3DOS}->{?X3DOS} que Subreport1 (logo2) ja tem. Padrao
      //    Clone+Modify (atribuicao direta de propriedade de ReportObject nao persiste).
      var rd = rcd.ReportDefinition;
      var sub4 = Find(rd, "Subreport4");
      var sub5 = Find(rd, "Subreport5");
      if (sub4 == null) throw new Exception("Subreport4 nao encontrado");
      if (sub5 == null) throw new Exception("Subreport5 nao encontrado");

      foreach (var pair in new[] { new { obj = sub4, tag = "Subreport4/logoHdr2" }, new { obj = sub5, tag = "Subreport5/logoHdr3" } }) {
        var clone = (ISCRReportObject)pair.obj.Clone(true);
        var srClone = (SubreportObject)clone;
        var newLinks = new SubreportLinksClass();
        var lk = new SubreportLinkClass();
        lk.MainReportFieldName = "{?X3DOS}";
        lk.SubreportFieldName = "{?X3DOS}";
        lk.LinkedParameterName = "{?X3DOS}";
        newLinks.Add(lk);
        srClone.SubreportLinks = newLinks;
        rcd.ReportDefController.ReportObjectController.Modify(pair.obj, clone);
        Lg(pair.tag + " SubreportLinks adicionado");
      }

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
