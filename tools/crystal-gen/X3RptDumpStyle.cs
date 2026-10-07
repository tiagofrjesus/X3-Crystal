// X3RptDumpStyle -- dump read-only (Engine API) de grupos, ordenacoes, running totals, formatacao de
// seccoes (suppress/new page) e estilo de cada objeto (fonte, cor, borda, fundo). Complementa
// X3RptInspect (que nao mostra grupos nem estilo). Uso: comparar o aspeto de dois relatorios.
using System;
using System.Text;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptDumpStyle {
  static string C(System.Drawing.Color c) { return c.IsEmpty ? "-" : (c.IsNamedColor ? c.Name : ("#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2") + (c.A < 255 ? "/a" + c.A : ""))); }

  public static string Dump(string rptPath) {
    var sb = new StringBuilder();
    var eng = new Eng.ReportDocument();
    eng.Load(rptPath);
    try {
      sb.Append("=== PAGE ===\r\n");
      var po = eng.PrintOptions;
      sb.AppendFormat("Paper={0} Orient={1} Margins L={2} R={3} T={4} B={5}\r\n", po.PaperSize, po.PaperOrientation,
        po.PageMargins.leftMargin, po.PageMargins.rightMargin, po.PageMargins.topMargin, po.PageMargins.bottomMargin);
      sb.Append("=== GROUPS ===\r\n");
      int gi = 0;
      foreach (Eng.Group g in eng.DataDefinition.Groups)
        sb.AppendFormat("Group{0}: {1}  cond={2}\r\n", gi++, g.ConditionField.FormulaName, g.GroupOptions.Condition);
      sb.Append("=== SORTS ===\r\n");
      foreach (Eng.SortField s in eng.DataDefinition.SortFields)
        sb.AppendFormat("{0} {1} {2}\r\n", s.Field.FormulaName, s.SortDirection, s.SortType);
      sb.Append("=== RUNNING TOTALS ===\r\n");
      foreach (Eng.RunningTotalFieldDefinition r in eng.DataDefinition.RunningTotalFields) {
        string ev = "", rs = "";
        try { ev = r.EvaluationConditionType + ":" + (r.EvaluationCondition == null ? "" : r.EvaluationCondition.ToString()); } catch (Exception e) { ev = "?" + e.Message; }
        try { rs = r.ResetConditionType + ":" + (r.ResetCondition == null ? "" : r.ResetCondition.ToString()); } catch (Exception e) { rs = "?" + e.Message; }
        sb.AppendFormat("{0}: {1}({2}) eval={3} reset={4}\r\n", r.Name, r.Operation, r.SummarizedField.FormulaName, ev, rs);
        try { var gf = r.EvaluationCondition as Eng.Group; if (gf != null) sb.Append("   evalGroup=" + gf.ConditionField.FormulaName + "\r\n"); } catch { }
        try { var gf = r.ResetCondition as Eng.Group; if (gf != null) sb.Append("   resetGroup=" + gf.ConditionField.FormulaName + "\r\n"); } catch { }
      }
      sb.Append("=== SUMMARY FIELDS ===\r\n");
      foreach (Eng.SummaryFieldDefinition s in eng.DataDefinition.SummaryFields)
        sb.AppendFormat("{0}: {1}({2}) grp={3}\r\n", s.Name, s.Operation, s.SummarizedField.FormulaName, s.Group == null ? "report" : s.Group.ConditionField.FormulaName);
      sb.Append("=== SECTIONS ===\r\n");
      foreach (Eng.Section sec in eng.ReportDefinition.Sections) {
        var f = sec.SectionFormat;
        sb.AppendFormat("-- {0} kind={1} H={2} suppress={3} newPageBefore={4} newPageAfter={5} keepTogether={6} bg={7}\r\n",
          sec.Name, sec.Kind, sec.Height, f.EnableSuppress, f.EnableNewPageBefore, f.EnableNewPageAfter, f.EnableKeepTogether, C(f.BackgroundColor));
        foreach (Eng.ReportObject o in sec.ReportObjects) {
          var ob = o.Border;
          string font = "", col = "";
          var fo = o as Eng.FieldObject; var to = o as Eng.TextObject;
          if (fo != null) { font = fo.Font.Name + " " + fo.Font.Size + (fo.Font.Bold ? " B" : "") + (fo.Font.Italic ? " I" : ""); col = C(fo.Color); }
          if (to != null) { font = to.Font.Name + " " + to.Font.Size + (to.Font.Bold ? " B" : "") + (to.Font.Italic ? " I" : ""); col = C(to.Color); }
          string extra = "";
          var bo = o as Eng.BoxObject; if (bo != null) extra = " fill=" + C(bo.FillColor) + " line=" + C(bo.LineColor) + " lw=" + bo.LineThickness + " ls=" + bo.LineStyle + " Right=" + bo.Right + " Bottom=" + bo.Bottom;
          var lo = o as Eng.LineObject; if (lo != null) extra = " line=" + C(lo.LineColor) + " lw=" + lo.LineThickness + " ls=" + lo.LineStyle + " Right=" + lo.Right;
          string hal = "";
          try { if (fo != null) hal = " align=" + fo.ObjectFormat.HorizontalAlignment; if (to != null) hal = " align=" + to.ObjectFormat.HorizontalAlignment; } catch { }
          sb.AppendFormat("   {0} {1} L={2} T={3} W={4} H={5} font=[{6}] color={7} bg={8} border={9}/{10}/{11}/{12}{13}{14} supp={15}\r\n",
            o.Kind, o.Name, o.Left, o.Top, o.Width, o.Height, font, col, C(ob.BackgroundColor),
            ob.LeftLineStyle, ob.TopLineStyle, ob.RightLineStyle, ob.BottomLineStyle, extra, hal, o.ObjectFormat.EnableSuppress);
        }
      }
    } catch (Exception ex) { sb.Append("FATAL: " + ex.Message + "\r\n" + ex.StackTrace); }
    eng.Close();
    return sb.ToString();
  }
}
