// Diagnostico READ-ONLY, dedicado: compara byte-a-byte RecordFilter/SubreportLinks/Details section
// entre logo2/logo3 (originais que funcionam) e logoHdr2/logoHdr3 (novos, suspeitos).
// Nao grava nada. Carrega o .rpt indicado e so faz leituras.
using System;
using System.Text;
using System.Collections.Generic;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptDiagLogo {
  static StringBuilder o;
  static void W(string s){ o.AppendLine(s); }

  static string Esc(string s) {
    if (s == null) return "(null)";
    var sb = new StringBuilder();
    foreach (char c in s) {
      if (c == '\r') sb.Append("\\r");
      else if (c == '\n') sb.Append("\\n");
      else if (c == '\t') sb.Append("\\t");
      else if (c < 32 || c > 126) sb.Append("\\u" + ((int)c).ToString("X4"));
      else sb.Append(c);
    }
    return sb.ToString();
  }

  public static string Dump(string path) {
    o = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(path);
      var rcd = eng.ReportClientDocument;
      W("Loaded: " + path);

      string[] names = { "logo1", "logo2", "logo3", "logoHdr2", "logoHdr3" };
      var filters = new Dictionary<string, string>();

      foreach (var nm in names) {
        W("");
        W("=========== SUBREPORT: " + nm + " ===========");
        CrystalDecisions.ReportAppServer.Controllers.SubreportClientDocument sub;
        try { sub = rcd.SubreportController.GetSubreport(nm); }
        catch (Exception e) { W("  GetSubreport ERR: " + e.Message); continue; }
        if (sub == null) { W("  (null subreport doc)"); continue; }

        // 1) RecordFilter texto exato
        string filterTxt = null;
        try {
          var rf = sub.DataDefController.DataDefinition.RecordFilter;
          filterTxt = rf != null ? rf.FreeEditingText : "(RecordFilter null)";
          W("  RecordFilter.FreeEditingText (len=" + (filterTxt != null ? filterTxt.Length : -1) + ") = |" + Esc(filterTxt) + "|");
        } catch (Exception e) { W("  RecordFilter ERR: " + e.Message); }
        filters[nm] = filterTxt;

        // 1b) via engine layer tambem (RecordSelectionFormula), para dupla confirmacao
        try {
          foreach (Eng.ReportDocument sd in eng.Subreports) {
            if (sd.Name == nm) {
              W("  [engine] RecordSelectionFormula = |" + Esc(sd.RecordSelectionFormula) + "|");
            }
          }
        } catch (Exception e) { W("  [engine] RecordSelectionFormula ERR: " + e.Message); }

        // 2) Parametros dentro do subreport (Name, ID via reflexao, UseCount)
        try {
          W("  ParameterFields dentro do subreport:");
          foreach (ISCRParameterField pf in sub.DataDefController.DataDefinition.ParameterFields) {
            string useCount = "?";
            try {
              var prop = typeof(ISCRParameterField).GetProperty("UseCount");
              if (prop != null) useCount = "" + prop.GetValue(pf, null);
            } catch (Exception e2) { useCount = "ERR:" + e2.Message; }
            string idVal = "?";
            try {
              var propId = typeof(ISCRParameterField).GetProperty("ID");
              if (propId != null) idVal = "" + propId.GetValue(pf, null);
            } catch { idVal = "(no ID prop)"; }
            W("    Name=" + pf.Name + " Type=" + pf.Type + " ID=" + idVal + " UseCount=" + useCount);
          }
        } catch (Exception e) { W("  params ERR: " + e.Message); }

        // 3) Secoes/objetos do subreport (Details Height, CanGrow dos objetos)
        try {
          var srd = sub.ReportDefController.ReportDefinition;
          DumpAreas(srd);
        } catch (Exception e) { W("  sections ERR: " + e.Message); }

        // 3b) Tabelas do subreport (ligacao/DSN da tabela ABLOB dentro deste subreport)
        try {
          W("  Tabelas do subreport:");
          foreach (ISCRTable t in sub.DatabaseController.Database.Tables) {
            W("    Table: Name=" + t.Name + " Alias=" + t.Alias + " QualifiedName=" + t.QualifiedName + " ClassName=" + t.ClassName + " CLRType=" + t.GetType().FullName);
            if (t is CommandTableClass || t.ClassName == "CrystalCommandTable") {
              try { W("      *** COMMAND TABLE *** CommandText = " + ((CommandTableClass)t).CommandText); } catch (Exception ecmd) { W("      CommandText ERR: " + ecmd.Message); }
            }
            try {
              var flds = new StringBuilder();
              foreach (ISCRField f in t.DataFields) flds.Append(f.Name + " ");
              W("      DataFields = [" + flds + "]");
            } catch (Exception efld) { W("      DataFields ERR: " + efld.Message); }
            try {
              var attr = t.ConnectionInfo.Attributes;
              foreach (string k in new[]{"Database DLL","QE_DatabaseType","QE_ServerDescription","QE_DatabaseName","QE_SQLDB","QE_LogonProperties"}) {
                try {
                  var av = attr[k];
                  if (av != null && !(av is string) && av.GetType().Name.IndexOf("PropertyBag") >= 0) {
                    var sb2 = new StringBuilder();
                    var pbType = av.GetType();
                    var keysProp = pbType.GetProperty("Keys");
                    System.Collections.IEnumerable keys = keysProp != null ? (System.Collections.IEnumerable)keysProp.GetValue(av, null) : null;
                    var itemProp = pbType.GetProperty("Item");
                    if (keys != null) {
                      foreach (object kk in keys) {
                        object vv = null;
                        try { vv = itemProp.GetValue(av, new object[] { kk }); } catch { }
                        sb2.Append(kk + "=" + vv + "; ");
                      }
                    }
                    W("      attr " + k + " = { " + sb2 + "}");
                  } else {
                    W("      attr " + k + " = " + av);
                  }
                } catch (Exception e3) { W("      attr " + k + " ERR: " + e3.Message); }
              }
              W("      ConnectionInfo.UserName=" + t.ConnectionInfo.UserName);
            } catch (Exception e2) { W("    conninfo ERR: " + e2.Message); }
          }
        } catch (Exception e) { W("  tables ERR: " + e.Message); }
      }

      // 4) Diff direto de texto entre pares equivalentes
      W("");
      W("=========== DIFF FILTROS ===========");
      DiffPair(filters, "logo2", "logoHdr2");
      DiffPair(filters, "logo3", "logoHdr3");

      // 5) SubreportLinks nos objetos de colocacao no relatorio PRINCIPAL
      W("");
      W("=========== SUBREPORT PLACEMENT OBJECTS (main report) ===========");
      foreach (ISCRReportObject robj in AllObjects(rcd.ReportDefinition)) {
        if (robj.Kind == CrReportObjectKindEnum.crReportObjectKindSubreport) {
          var sr = (SubreportObject)robj;
          W("Placement: " + sr.Name + "  SubreportName=" + sr.SubreportName + "  L=" + robj.Left + " T=" + robj.Top + " W=" + robj.Width + " H=" + robj.Height);
          try {
            var links = sr.SubreportLinks;
            if (links == null) { W("   SubreportLinks = (null)"); }
            else {
              int cnt = 0;
              foreach (SubreportLink lk in links) {
                cnt++;
                W("   Link: MainReportFieldName=" + lk.MainReportFieldName + " SubreportFieldName=" + lk.SubreportFieldName + " LinkedParameterName=" + lk.LinkedParameterName);
              }
              W("   TotalLinks=" + cnt);
            }
          } catch (Exception e) { W("   SubreportLinks ERR: " + e.Message); }
        }
      }

      eng.Close();
    } catch (Exception ex) {
      W("FATAL: " + ex.Message);
      W(ex.StackTrace);
    }
    return o.ToString();
  }

  static void DiffPair(Dictionary<string, string> filters, string a, string b) {
    string fa, fb;
    filters.TryGetValue(a, out fa);
    filters.TryGetValue(b, out fb);
    if (fa == null || fb == null) { W(a + " vs " + b + ": um dos dois nao foi lido"); return; }
    if (fa == fb) { W(a + " vs " + b + ": IDENTICOS (len=" + fa.Length + ")"); return; }
    W(a + " vs " + b + ": DIFERENTES  (len " + a + "=" + fa.Length + ", len " + b + "=" + fb.Length + ")");
    int n = Math.Min(fa.Length, fb.Length);
    int firstDiff = -1;
    for (int i = 0; i < n; i++) { if (fa[i] != fb[i]) { firstDiff = i; break; } }
    if (firstDiff == -1 && fa.Length != fb.Length) firstDiff = n;
    W("   Primeira diferenca no indice " + firstDiff);
    int ctxStart = Math.Max(0, firstDiff - 15);
    W("   " + a + " contexto: ..." + Esc(fa.Substring(ctxStart, Math.Min(40, fa.Length - ctxStart))) + "...");
    W("   " + b + " contexto: ..." + Esc(fb.Substring(ctxStart, Math.Min(40, fb.Length - ctxStart))) + "...");
  }

  static void DumpAreas(ReportDefinition rd) {
    foreach (ISCRArea a in AllAreas(rd)) {
      if (a == null) continue;
      string areaName;
      try { areaName = a.Kind.ToString(); } catch { areaName = "?"; }
      foreach (Section s in a.Sections) {
        W("  -- Section " + areaName + " / " + s.Name + " (Height=" + s.Height + ") --");
        foreach (ISCRReportObject ro in s.ReportObjects) {
          string extra = "";
          try {
            var prop = ro.GetType().GetProperty("CanGrow");
            if (prop != null) extra += " CanGrow=" + prop.GetValue(ro, null);
          } catch (Exception e2) { extra += " CanGrowERR:" + e2.Message; }
          try {
            foreach (var t in ro.GetType().GetInterfaces()) {
              var prop2 = t.GetProperty("CanGrow");
              if (prop2 != null) {
                try { extra += " [" + t.Name + "].CanGrow=" + prop2.GetValue(ro, null); } catch {}
              }
            }
          } catch {}
          W("    [" + ro.Kind + "] Name=" + ro.Name + " L=" + ro.Left + " T=" + ro.Top + " W=" + ro.Width + " H=" + ro.Height + extra);
        }
      }
    }
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
}
