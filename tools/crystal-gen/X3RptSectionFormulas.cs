// X3RptSectionFormulas -- lista as formulas condicionais das seccoes (suprimir, nova pagina, etc.) e as
// opcoes de formato relevantes (underlay, keepTogether). Read-only. Correr em 32-bit.
// DumpCert: so as seccoes que contem objetos certificados AT (nome PT* / fonte TMPSRPT.PT* / @PT_* / QR /
// atcud), com os parametros da seccao e a visibilidade (estatica + condicional) de cada objeto certificado.
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptSectionFormulas {
  static string SecFormulas(SectionFormat f) {
    var sb = new StringBuilder();
    foreach (CrSectionAreaFormatConditionFormulaTypeEnum k in Enum.GetValues(typeof(CrSectionAreaFormatConditionFormulaTypeEnum))) {
      string t = null;
      try { var cf = f.ConditionFormulas[k]; if (cf != null) t = cf.Text; } catch { }
      if (!string.IsNullOrEmpty(t)) sb.Append("\r\n    [").Append(k.ToString().Replace("crSectionAreaConditionFormulaType", "")).Append("] ").Append(t.Replace("\r\n", " "));
    }
    return sb.ToString();
  }
  static bool IsCert(ReportObject ro) {
    if (ro.Name.StartsWith("PT") || ro.Name.StartsWith("IMGQRC") || ro.Name.StartsWith("atcud")) return true;
    var fo = ro as ISCRFieldObject;
    return fo != null && fo.DataSource != null && (fo.DataSource.Contains("TMPSRPT.PT") || fo.DataSource.StartsWith("{@PT_") || fo.DataSource.StartsWith("{@atcud") || fo.DataSource.Contains("PORQRC."));
  }
  public static string Dump(string rpt) { return Run(rpt, false); }
  public static string DumpCert(string rpt) { return Run(rpt, true); }
  static string Run(string rpt, bool certOnly) {
    var sb = new StringBuilder();
    var eng = new Eng.ReportDocument();
    try {
      eng.Load(rpt);
      foreach (Area a in eng.ReportClientDocument.ReportDefController.ReportDefinition.Areas)
        foreach (Section s in a.Sections) {
          bool has = false;
          foreach (ReportObject ro in s.ReportObjects) if (IsCert(ro)) has = true;
          if (certOnly && !has) continue;
          var f = s.Format;
          sb.Append(s.Name).Append(" H=").Append(s.Height).Append(" supp=").Append(f.EnableSuppress)
            .Append(" underlay=").Append(f.EnableUnderlaySection).Append(" suppBlank=").Append(f.EnableSuppressIfBlank)
            .Append(" keepTogether=").Append(f.EnableKeepTogether).Append(" newPageBefore=").Append(f.EnableNewPageBefore)
            .Append(" newPageAfter=").Append(f.EnableNewPageAfter).Append(" printAtBottom=").Append(f.EnablePrintAtBottomOfPage);
          sb.Append(SecFormulas(f)).Append("\r\n");
          if (!certOnly) continue;
          foreach (ReportObject ro in s.ReportObjects) {
            if (!IsCert(ro)) continue;
            string cs = null;
            try { var c = ro.Format.ConditionFormulas[CrObjectFormatConditionFormulaTypeEnum.crObjectFormatConditionFormulaTypeEnableSuppress]; if (c != null) cs = c.Text; } catch { }
            var fo = ro as ISCRFieldObject;
            sb.Append("  ").Append(ro.Name).Append(" L=").Append(ro.Left).Append(" T=").Append(ro.Top).Append(" W=").Append(ro.Width).Append(" H=").Append(ro.Height)
              .Append(" src=").Append(fo != null ? fo.DataSource : ro.Kind.ToString()).Append(" supp=").Append(ro.Format.EnableSuppress);
            if (!string.IsNullOrEmpty(cs)) sb.Append(" cond=[").Append(cs.Replace("\r\n", " ")).Append("]");
            sb.Append("\r\n");
          }
        }
    } catch (Exception ex) { sb.Append("FATAL ").Append(ex.Message); }
    finally { eng.Close(); }
    return sb.ToString();
  }
}
