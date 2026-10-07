// X3RptTexts -- dump JSON (read-only) de TODOS os objetos de texto do relatorio principal: nome, seccao,
// geometria, estilo, supressao estatica e o texto completo (campos embutidos como {campo}, quebras de linha
// como \n). Serve para converter textos fixos em menus locais (TextOfChapter).
using System;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptTexts {
  static string ParaText(ISCRTextObject t) {
    var sb = new StringBuilder();
    int pi = 0;
    foreach (Paragraph p in t.Paragraphs) {
      if (pi++ > 0) sb.Append("\n");
      foreach (ParagraphElement e in p.ParagraphElements) {
        var fe = e as ParagraphFieldElement;
        var te = e as ParagraphTextElement;
        if (fe != null) sb.Append(fe.DataSource);
        else if (te != null) sb.Append(te.Text);
        else if (e.Kind == CrParagraphElementKindEnum.crParagraphElementKindTab) sb.Append("\t");
      }
    }
    return sb.ToString();
  }
  public static string Dump(string rpt) {
    var list = new List<Dictionary<string, object>>();
    var eng = new Eng.ReportDocument();
    try {
      eng.Load(rpt);
      foreach (Area a in eng.ReportClientDocument.ReportDefController.ReportDefinition.Areas)
        foreach (Section s in a.Sections)
          foreach (ReportObject ro in s.ReportObjects) {
            var t = ro as ISCRTextObject;
            if (t == null) continue;
            var d = new Dictionary<string, object>();
            d["name"] = ro.Name; d["section"] = s.Name;
            d["l"] = ro.Left; d["t"] = ro.Top; d["w"] = ro.Width; d["h"] = ro.Height;
            d["hidden"] = s.Format.EnableSuppress || ro.Format.EnableSuppress;
            d["condSuppress"] = ro.Format.ConditionFormulas != null ? "" : "";
            if (t.FontColor != null && t.FontColor.Font != null) {
              d["font"] = t.FontColor.Font.Name; d["size"] = Convert.ToDouble(t.FontColor.Font.Size);
              d["bold"] = t.FontColor.Font.Bold; d["italic"] = t.FontColor.Font.Italic; d["underline"] = t.FontColor.Font.Underline;
              uint c = t.FontColor.Color;
              d["color"] = string.Format("#{0:X2}{1:X2}{2:X2}", c & 0xFF, (c >> 8) & 0xFF, (c >> 16) & 0xFF);
            }
            d["align"] = ro.Format.HorizontalAlignment.ToString();
            d["text"] = ParaText(t);
            list.Add(d);
          }
      // subreports (d["sub"] = nome do subreport)
      foreach (string sn in eng.ReportClientDocument.SubreportController.GetSubreportNames()) {
        var sub = eng.ReportClientDocument.SubreportController.GetSubreport(sn);
        foreach (Area a in sub.ReportDefController.ReportDefinition.Areas)
          foreach (Section s in a.Sections)
            foreach (ReportObject ro in s.ReportObjects) {
              var t = ro as ISCRTextObject;
              if (t == null) continue;
              var d = new Dictionary<string, object>();
              d["sub"] = sn; d["name"] = ro.Name; d["section"] = s.Name;
              d["l"] = ro.Left; d["t"] = ro.Top; d["w"] = ro.Width; d["h"] = ro.Height;
              d["hidden"] = s.Format.EnableSuppress || ro.Format.EnableSuppress;
              if (t.FontColor != null && t.FontColor.Font != null) {
                d["font"] = t.FontColor.Font.Name; d["size"] = Convert.ToDouble(t.FontColor.Font.Size);
                d["bold"] = t.FontColor.Font.Bold; d["italic"] = t.FontColor.Font.Italic; d["underline"] = t.FontColor.Font.Underline;
                uint c = t.FontColor.Color;
                d["color"] = string.Format("#{0:X2}{1:X2}{2:X2}", c & 0xFF, (c >> 8) & 0xFF, (c >> 16) & 0xFF);
              }
              d["align"] = ro.Format.HorizontalAlignment.ToString();
              d["text"] = ParaText(t);
              list.Add(d);
            }
      }
    } catch (Exception ex) { var d = new Dictionary<string, object>(); d["FATAL"] = ex.Message; list.Add(d); }
    finally { eng.Close(); }
    return new JavaScriptSerializer().Serialize(list);
  }
}
