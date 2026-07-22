// X3RptInspect — dump read-only da estrutura de um .rpt existente (tabelas, campos, formulas, objetos)
// Compilar via Add-Type em PowerShell 32-BIT.
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptInspect {
  static StringBuilder o;
  static void W(string s){ o.AppendLine(s); }
  static void DumpObj(string label, object obj) {
    var sb = new StringBuilder(label + ": ");
    if (obj == null) { W(sb.ToString() + "(null)"); return; }
    foreach (var t in obj.GetType().GetInterfaces()) {
      foreach (var p in t.GetProperties()) {
        try {
          var val = p.GetValue(obj, null);
          if (val is System.Collections.IEnumerable && !(val is string)) {
            var items = new StringBuilder();
            foreach (var it in (System.Collections.IEnumerable)val) {
              if (it is ISCRField) items.Append(((ISCRField)it).FormulaForm + " ");
              else items.Append(it + " ");
            }
            sb.Append(p.Name + "=[" + items + "] ");
          } else {
            sb.Append(p.Name + "=" + val + " ");
          }
        } catch (Exception e) { sb.Append(p.Name + "=ERR:" + e.Message + " "); }
      }
    }
    W(sb.ToString());
  }

  public static string Dump(string path){
    o = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(path);
      var rcd = eng.ReportClientDocument;

      W("=== TABLES ===");
      foreach (ISCRTable t in rcd.Database.Tables) {
        W("Table: Name=" + t.Name + " Alias=" + t.Alias + " ClassName=" + t.ClassName + " QualifiedName=" + t.QualifiedName);
        try {
          var attr = t.ConnectionInfo.Attributes;
          foreach (string k in new[]{"Database DLL","QE_DatabaseType","QE_ServerDescription","QE_DatabaseName","QE_SQLDB"}) {
            try { W("   attr " + k + " = " + attr[k]); } catch {}
          }
        } catch (Exception e) { W("   conninfo ERR: " + e.Message); }
        if (t is CommandTableClass || t.ClassName == "CrystalCommandTable") {
          try { W("   CommandText = " + ((CommandTableClass)t).CommandText); } catch {}
        }
        W("   Fields:");
        foreach (ISCRField f in t.DataFields) {
          W("     " + f.Name + "  (" + f.Type + ")  FormulaForm=" + f.FormulaForm);
        }
      }

      W("");
      W("=== LINKS ===");
      try {
        ISCRDatabase db = rcd.Database;
        W("-- TableLinks --");
        foreach (ISCRTableLink lk in db.TableLinks) {
          var sb = new StringBuilder("Link: ");
          try { sb.Append("Src=" + lk.SourceTableAlias + " "); } catch (Exception e) { sb.Append("Src ERR:" + e.Message + " "); }
          try { sb.Append("Dst=" + lk.TargetTableAlias + " "); } catch (Exception e) { sb.Append("Dst ERR:" + e.Message + " "); }
          try {
            sb.Append("SrcFields=[");
            foreach (string f in (System.Collections.IEnumerable)lk.SourceFieldNames) sb.Append(f + " ");
            sb.Append("] ");
          } catch (Exception e) { sb.Append("SrcFields ERR:" + e.Message + " "); }
          try {
            sb.Append("DstFields=[");
            foreach (string f in (System.Collections.IEnumerable)lk.TargetFieldNames) sb.Append(f + " ");
            sb.Append("] ");
          } catch (Exception e) { sb.Append("DstFields ERR:" + e.Message + " "); }
          try { sb.Append("JoinType=" + lk.JoinType); } catch (Exception e) { sb.Append("JoinType ERR:" + e.Message); }
          W(sb.ToString());
        }
      } catch (Exception e) { W("links ERR: " + e.Message + "\n" + e.StackTrace); }

      W("");
      W("=== PRINT OPTIONS ===");
      try {
        var po = rcd.PrintOutputController.GetPrintOptions();
        W("PaperSize=" + po.PaperSize + " Orientation=" + po.PaperOrientation
          + " Dissociate=" + po.DissociatePageSizeAndPrinterPaperSize);
      } catch (Exception e) { W("printopts ERR: " + e.Message); }

      W("");
      W("=== FORMULA FIELDS ===");
      try {
        foreach (ISCRFormulaField ff in rcd.DataDefController.DataDefinition.FormulaFields) {
          W(ff.Name + " = " + ff.Text);
        }
      } catch (Exception e) { W("formulas ERR: " + e.Message); }

      W("");
      W("=== PARAMETER FIELDS ===");
      try {
        foreach (ISCRParameterField pf in rcd.DataDefController.DataDefinition.ParameterFields) {
          W(pf.Name + "  Type=" + pf.Type);
        }
      } catch (Exception e) { W("params ERR: " + e.Message); }

      W("");
      W("=== RECORD SELECTION FORMULA ===");
      try { W(eng.RecordSelectionFormula); } catch (Exception e) { W("recsel ERR: " + e.Message); }

      W("");
      W("=== SubreportController.ImportSubreport signatures (debug) ===");
      try {
        foreach (var mm in typeof(CrystalDecisions.ReportAppServer.Controllers.ISCRSubreportController).GetMethods()) {
          if (mm.Name == "ImportSubreport" || mm.Name == "ImportSubreportEx") {
            var ps = mm.GetParameters();
            var sb = new StringBuilder(mm.Name + "(");
            foreach (var p in ps) sb.Append(p.ParameterType.Name + " " + p.Name + ", ");
            sb.Append(") -> " + mm.ReturnType.Name);
            W(sb.ToString());
          }
        }
      } catch (Exception e) { W("ERR:" + e.Message); }

      W("");
      W("=== SECTIONS / OBJECTS ===");
      DumpAreas(rcd.ReportDefinition);

      W("");
      W("=== SUBREPORTS ===");
      try {
        foreach (ISCRReportObject robj in AllObjects(rcd.ReportDefinition)) {
          if (robj.Kind == CrReportObjectKindEnum.crReportObjectKindSubreport) {
            var sr = (SubreportObject)robj;
            W("Subreport object: " + sr.Name + "  SubreportName=" + sr.SubreportName);
          }
        }
      } catch (Exception e) { W("subrep ERR: " + e.Message); }
      try {
        foreach (Eng.ReportDocument sub in eng.Subreports) {
          W("Subreport doc: " + sub.Name);
          foreach (var t in sub.Database.Tables) {
            var nameProp = t.GetType().GetProperty("Name");
            W("   subtable: " + (nameProp != null ? nameProp.GetValue(t, null) : t.ToString()));
          }
        }
      } catch (Exception e) { W("subrep doc ERR: " + e.Message); }

      eng.Close();
    } catch (Exception ex) {
      W("FATAL: " + ex.Message);
      W(ex.StackTrace);
    }
    return o.ToString();
  }

  static System.Collections.Generic.IEnumerable<ISCRReportObject> AllObjects(ReportDefinition rd) {
    var list = new System.Collections.Generic.List<ISCRReportObject>();
    foreach (ISCRArea a in AllAreas(rd)) foreach (Section s in a.Sections) foreach (ISCRReportObject ro in s.ReportObjects) list.Add(ro);
    return list;
  }

  static System.Collections.Generic.IEnumerable<ISCRArea> AllAreas(ReportDefinition rd) {
    var l = new System.Collections.Generic.List<ISCRArea>();
    l.Add(rd.ReportHeaderArea); l.Add(rd.PageHeaderArea); l.Add(rd.DetailArea);
    l.Add(rd.ReportFooterArea); l.Add(rd.PageFooterArea);
    for (int i = 0; i < 5; i++) {
      try { var a = rd.GroupHeaderArea[i]; if (a != null) l.Add(a); } catch {}
      try { var a = rd.GroupFooterArea[i]; if (a != null) l.Add(a); } catch {}
    }
    return l;
  }

  static void DumpAreas(ReportDefinition rd) {
    foreach (ISCRArea a in AllAreas(rd)) {
      if (a == null) continue;
      string areaName;
      try { areaName = a.Kind.ToString(); } catch { areaName = "?"; }
      foreach (Section s in a.Sections) {
        W("-- Section " + areaName + " / " + s.Name + " (Height=" + s.Height + ") --");
        foreach (ISCRReportObject ro in s.ReportObjects) {
          string extra = "";
          try {
            if (ro is FieldObject) extra = " DataSource=" + ((FieldObject)ro).DataSource + " Type=" + ((FieldObject)ro).FieldValueType;
            else if (ro is TextObject) extra = " Text=[" + ((TextObject)ro).Text + "]";
          } catch (Exception ex2) { extra = " extraERR:" + ex2.Message; }
          W("   [" + ro.Kind + "] Name=" + ro.Name + " L=" + ro.Left + " T=" + ro.Top + " W=" + ro.Width + " H=" + ro.Height + extra);
        }
      }
    }
  }
}
