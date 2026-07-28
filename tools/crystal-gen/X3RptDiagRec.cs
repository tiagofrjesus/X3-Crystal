// X3RptDiagRec — diagnostico read-only de TEB_REC.rpt: parametros (main + subreports IVAC e
// RecPendHist) com UseCount/PromptToUser via reflexao sobre ISCRParameterField, SubreportLinks
// de colocacao no relatorio principal, RecordFilter de cada subreport, e ClassName das tabelas
// dentro de cada subreport (para apanhar CommandTable escondida).
using System;
using System.Text;
using System.Collections.Generic;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptDiagRec {
  static StringBuilder o;
  static void W(string s){ o.AppendLine(s); }

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
  static IEnumerable<ISCRReportObject> AllObjects(ReportDefinition rd) {
    var list = new List<ISCRReportObject>();
    foreach (var a in AllAreas(rd)) { if (a == null) continue; foreach (Section s in a.Sections) foreach (ISCRReportObject ro in s.ReportObjects) list.Add(ro); }
    return list;
  }

  static void DumpParams(string label, ISCRDataDefinition dd) {
    W("  -- Parameters (" + label + ") --");
    try {
      var useCountProp = typeof(ISCRParameterField).GetProperty("UseCount");
      var promptProp = typeof(ISCRParameterField).GetProperty("PromptToUser");
      foreach (ISCRParameterField pf in dd.ParameterFields) {
        string uc = "?"; string pt = "?";
        try { uc = useCountProp.GetValue(pf, null).ToString(); } catch (Exception e) { uc = "ERR:" + e.Message; }
        try { pt = promptProp.GetValue(pf, null).ToString(); } catch (Exception e) { pt = "ERR:" + e.Message; }
        W("    " + pf.Name + "  Type=" + pf.Type + "  UseCount=" + uc + "  PromptToUser=" + pt);
      }
    } catch (Exception e) { W("    params ERR: " + e.Message); }
  }

  static void DumpParamsFull(string label, ISCRDataDefinition dd) {
    W("  -- Parameters FULL (" + label + ") --");
    try {
      foreach (ISCRParameterField pf in dd.ParameterFields) {
        W("  PARAM " + pf.Name + ":");
        foreach (var t in new[]{ typeof(ISCRParameterField) }) {
          foreach (var p in t.GetProperties()) {
            try {
              var val = p.GetValue(pf, null);
              if (val is System.Collections.IEnumerable && !(val is string)) {
                var items = new StringBuilder();
                foreach (var it in (System.Collections.IEnumerable)val) items.Append(it + " ");
                W("      " + p.Name + " = [" + items + "]");
              } else {
                W("      " + p.Name + " = " + val);
              }
            } catch (Exception e) { W("      " + p.Name + " = ERR:" + e.Message); }
          }
        }
      }
    } catch (Exception e) { W("    paramsFull ERR: " + e.Message); }
  }

  static void DumpRecordFilter(string label, ISCRDataDefinition dd) {
    try { W("  RecordFilter (" + label + ") = [" + dd.RecordFilter.FreeEditingText + "]"); }
    catch (Exception e) { W("  RecordFilter ERR: " + e.Message); }
  }

  static void DumpTables(string label, ISCRDatabase db) {
    W("  -- Tables (" + label + ") --");
    try {
      foreach (ISCRTable t in db.Tables) {
        W("    Table Name=" + t.Name + " Alias=" + t.Alias + " ClassName=" + t.ClassName + " QualifiedName=" + t.QualifiedName);
        if (t.ClassName == "CrystalReports.CommandTable" || t is CommandTableClass) {
          try { W("       CommandText=" + ((CommandTableClass)t).CommandText); } catch {}
        }
        try {
          var attr = t.ConnectionInfo.Attributes;
          try { W("       UserName=[" + t.ConnectionInfo.UserName + "] Password=[" + t.ConnectionInfo.Password + "]"); } catch (Exception e) { W("       UserName/Password ERR: " + e.Message); }
          try {
            var logon = (PropertyBag)attr["QE_LogonProperties"];
            foreach (string lk in new[]{"UID","PWD"}) { try { W("       logon " + lk + " = [" + logon[lk] + "]"); } catch (Exception e2) { W("       logon " + lk + " missing/ERR: " + e2.Message); } }
          } catch (Exception e) { W("       QE_LogonProperties ERR: " + e.Message); }
        } catch (Exception e) { W("       conninfo ERR: " + e.Message); }
        if (t.Alias == "PAYMENTD") {
          foreach (ISCRField f in t.DataFields) {
            if (f.Name == "NUM_0" || f.Name == "VCRNUM_0" || f.Name == "DUDNUM_0")
              W("       FIELD " + f.Name + " Type=" + f.Type + " FormulaForm=" + f.FormulaForm);
          }
        }
      }
    } catch (Exception e) { W("    tables ERR: " + e.Message); }
  }

  static void DumpSubreportPlacement(ReportDefinition rd, string subreportName) {
    W("  -- SubreportObject placement for '" + subreportName + "' in MAIN report --");
    bool found = false;
    foreach (var ro in AllObjects(rd)) {
      if (ro.Kind == CrReportObjectKindEnum.crReportObjectKindSubreport) {
        var sr = (SubreportObject)ro;
        if (sr.SubreportName == subreportName) {
          found = true;
          W("    ObjectName=" + sr.Name + " L=" + sr.Left + " T=" + sr.Top + " W=" + sr.Width + " H=" + sr.Height);
          try {
            int n = 0;
            foreach (SubreportLink lk in sr.SubreportLinks) {
              n++;
              W("    Link[" + n + "]: MainReportFieldName=" + lk.MainReportFieldName + " SubreportFieldName=" + lk.SubreportFieldName + " LinkedParameterName=" + lk.LinkedParameterName);
            }
            if (n == 0) W("    (SEM SubreportLinks na propriedade do objeto)");
          } catch (Exception e) { W("    SubreportLinks ERR: " + e.Message); }
        }
      }
    }
    if (!found) W("    NAO ENCONTRADO no relatorio principal");
  }

  static void DumpSubreportControllerLinks(ISCDReportClientDocument rcd, string subreportName) {
    W("  -- SubreportController.GetSubreportLinks('" + subreportName + "') --");
    try {
      var links = rcd.SubreportController.GetSubreportLinks(subreportName);
      int n = 0;
      foreach (SubreportLink lk in links) {
        n++;
        W("    CtrlLink[" + n + "]: MainReportFieldName=" + lk.MainReportFieldName + " SubreportFieldName=" + lk.SubreportFieldName + " LinkedParameterName=" + lk.LinkedParameterName);
      }
      if (n == 0) W("    (vazio)");
    } catch (Exception e) { W("    ERR: " + e.Message); }
  }

  public static string Dump(string path) {
    o = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(path);
      var rcd = eng.ReportClientDocument;
      W("=== MAIN REPORT ===");
      var rd = rcd.ReportDefinition;
      DumpParams("MAIN", rcd.DataDefController.DataDefinition);
      DumpRecordFilter("MAIN", rcd.DataDefController.DataDefinition);
      DumpTables("MAIN", rcd.Database);
      W("");
      W("=== GROUPS (main report) ===");
      try {
        int gi = 0;
        foreach (Group g in rcd.DataDefController.DataDefinition.Groups) {
          W("  Group[" + gi + "]: ConditionField=" + g.ConditionField.FormulaForm);
          gi++;
        }
      } catch (Exception e) { W("  groups ERR: " + e.Message); }

      W("");
      W("=== AREA/SECTION MAP (nesting order, to match Group[] index to Section NAME) ===");
      try {
        W("  ReportHeaderArea:"); foreach (Section s in rd.ReportHeaderArea.Sections) W("    " + s.Name);
        W("  PageHeaderArea:"); foreach (Section s in rd.PageHeaderArea.Sections) W("    " + s.Name);
        for (int i = 0; i < 5; i++) {
          try { var a = rd.GroupHeaderArea[i]; if (a == null) continue; W("  GroupHeaderArea[" + i + "] (Group=" + rcd.DataDefController.DataDefinition.Groups[i].ConditionField.FormulaForm + "):"); foreach (Section s in a.Sections) W("    " + s.Name + " Height=" + s.Height); } catch {}
        }
        W("  DetailArea:"); foreach (Section s in rd.DetailArea.Sections) W("    " + s.Name);
        for (int i = 4; i >= 0; i--) {
          try { var a = rd.GroupFooterArea[i]; if (a == null) continue; W("  GroupFooterArea[" + i + "] (Group=" + rcd.DataDefController.DataDefinition.Groups[i].ConditionField.FormulaForm + "):"); foreach (Section s in a.Sections) { W("    " + s.Name + " Height=" + s.Height); foreach (ISCRReportObject ro in s.ReportObjects) { string ds=""; try { if (ro is FieldObject) ds = " DataSource=" + ((FieldObject)ro).DataSource; } catch {} W("       [" + ro.Kind + "] " + ro.Name + ds); } } } catch {}
        }
        W("  ReportFooterArea:"); foreach (Section s in rd.ReportFooterArea.Sections) W("    " + s.Name);
        W("  PageFooterArea:"); foreach (Section s in rd.PageFooterArea.Sections) W("    " + s.Name);
      } catch (Exception e) { W("  area map ERR: " + e.Message + "\n" + e.StackTrace); }

      W("");
      W("=== SUBREPORT PLACEMENTS (main report SubreportObjects) ===");
      DumpSubreportPlacement(rd, "IVAC");
      DumpSubreportPlacement(rd, "RecPendHist");

      W("");
      W("=== SubreportController.GetSubreportLinks (API dedicada) ===");
      DumpSubreportControllerLinks(rcd, "IVAC");
      DumpSubreportControllerLinks(rcd, "RecPendHist");

      W("");
      W("=== SUBREPORT: IVAC (referencia funcional) ===");
      try {
        var ivac = rcd.SubreportController.GetSubreport("IVAC");
        DumpParamsFull("IVAC", ivac.DataDefController.DataDefinition);
        DumpRecordFilter("IVAC", ivac.DataDefController.DataDefinition);
        DumpTables("IVAC", ivac.DatabaseController.Database);
      } catch (Exception e) { W("IVAC ERR: " + e.Message); }

      W("");
      W("=== SUBREPORT: RecPendHist (novo, suspeito) ===");
      try {
        var rph = rcd.SubreportController.GetSubreport("RecPendHist");
        DumpParamsFull("RecPendHist", rph.DataDefController.DataDefinition);
        DumpRecordFilter("RecPendHist", rph.DataDefController.DataDefinition);
        DumpTables("RecPendHist", rph.DatabaseController.Database);
      } catch (Exception e) { W("RecPendHist ERR: " + e.Message); }

      W("");
      W("=== ALL SUBREPORTS — FULL DUMP (params/links/filter/tables) ===");
      try {
        foreach (Eng.ReportDocument sub in eng.Subreports) {
          W("--- subreport doc: " + sub.Name + " ---");
          try {
            var subDoc = rcd.SubreportController.GetSubreport(sub.Name);
            DumpParams(sub.Name, subDoc.DataDefController.DataDefinition);
            DumpRecordFilter(sub.Name, subDoc.DataDefController.DataDefinition);
            DumpTables(sub.Name, subDoc.DatabaseController.Database);
            DumpSubreportPlacement(rd, sub.Name);
          } catch (Exception e) { W("  ERR: " + e.Message); }
        }
      } catch (Exception e) { W("ERR: " + e.Message); }

      eng.Close();
    } catch (Exception ex) {
      W("FATAL: " + ex.Message);
      W(ex.StackTrace);
    }
    return o.ToString();
  }
}
