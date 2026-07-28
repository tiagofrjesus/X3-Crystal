// X3RptDiagTableConn -- dump completo (todos os atributos do PropertyBag da tabela e da
// ConnectionInfo) para comparar PAYMENTD (original) vs PAYMENTD_CUM (novo, self-join).
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptDiagTableConn {
  static StringBuilder o;
  static void W(string s){ o.AppendLine(s); }

  static void DumpPropertyBag(string label, PropertyBag bag) {
    if (bag == null) { W("    " + label + " = (null)"); return; }
    try {
      var ids = (System.Collections.IEnumerable)bag.PropertyIDs;
      foreach (string id in ids) {
        object val = null;
        try { val = bag[id]; } catch (Exception e) { val = "ERR:" + e.Message; }
        W("    " + label + "[" + id + "] = " + val);
      }
    } catch (Exception e) { W("    " + label + " ERR: " + e.Message); }
  }

  static void DumpTable(string label, ISCRTable t) {
    W("-- Table " + label + " --");
    W("  Name=" + t.Name + " Alias=" + t.Alias + " ClassName=" + t.ClassName);
    W("  QualifiedName=" + t.QualifiedName);
    try { W("  Description=" + t.Description); } catch (Exception e) { W("  Description ERR:" + e.Message); }
    DumpPropertyBag("Table.Attributes", t.Attributes);
    try {
      var ci = t.ConnectionInfo;
      W("  ConnectionInfo.ClassName=" + ci.ClassName + " Kind=" + ci.Kind + " UserName=[" + ci.UserName + "] Password=[" + ci.Password + "]");
      DumpPropertyBag("ConnectionInfo.Attributes", ci.Attributes);
    } catch (Exception e) { W("  ConnectionInfo ERR: " + e.Message); }
  }

  public static string Dump(string path) {
    o = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(path);
      var rcd = eng.ReportClientDocument;
      foreach (ISCRTable t in rcd.Database.Tables) {
        if (t.Alias == "PAYMENTD" || t.Alias == "PAYMENTD_CUM") DumpTable(t.Alias, t);
      }
      eng.Close();
    } catch (Exception ex) {
      W("FATAL: " + ex.Message);
      W(ex.StackTrace);
    }
    return o.ToString();
  }
}
