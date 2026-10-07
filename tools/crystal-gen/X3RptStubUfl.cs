// X3RptStubUfl -- cria uma COPIA DE TESTE de um relatorio X3 com as formulas que chamam UFLs do X3
// (TextOfChapter, AmountToWord...) substituidas por stubs, porque fora do print server os UFLs falham
// com "Local menus file not found". NUNCA publicar a copia gerada -- serve so para Export-Full.ps1.
// Se Texts estiver preenchido ("capitulo:numero" -> texto, de X3.APLSTD), cada chamada
// TextOfChapter(dos, lan, cap, num) e trocada pelo literal real (preview fiel); o resto da formula fica.
// Aplica-se ao relatorio principal E a todos os subreports.
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.Controllers;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptStubUfl {
  public static Dictionary<string, string> Texts = new Dictionary<string, string>();
  // TextOfChapter(dos, lan, capitulo, numero) -- numero literal ou dinamico ({campo}, expressao simples)
  static readonly Regex Toc = new Regex(@"TextOfChapter\s*\(\s*[^,()]+,\s*[^,()]+,\s*(\d+)\s*,\s*([^,()]+?)\s*\)", RegexOptions.IgnoreCase);
  static readonly Regex OtherUfl = new Regex(@"\b(X3)?TranslatedText\s*\([^()]*\)|\bAmountToWord\s*\([^()]*\)", RegexOptions.IgnoreCase);
  static readonly Regex Ufl =new Regex(@"TextOfChapter|AmountToWord|TranslatedText", RegexOptions.IgnoreCase);

  static string Lit(string s) { return "\"" + s.Replace("\"", "\"\"") + "\""; }

  static void StubAll(FormulaFieldController ffc, Fields formulas, string who, StringBuilder log) {
    var targets = new List<ISCRFormulaField>();
    foreach (ISCRFormulaField ff in formulas)
      if (ff.Text != null && Ufl.IsMatch(ff.Text)) targets.Add(ff);
    foreach (var ff in targets) {
      var nf = new FormulaFieldClass();
      nf.Name = ff.Name;
      string t = ff.Text;
      if (Texts.Count > 0) t = Toc.Replace(t, m => {
        string n = m.Groups[2].Value.Trim(); int dummy;
        if (!int.TryParse(n, out dummy)) n = "1";   // numero dinamico -> 1a entrada do capitulo (preview)
        string k = m.Groups[1].Value + ":" + n;
        return Lit(Texts.ContainsKey(k) ? Texts[k] : "?" + k); });
      // chamadas simples (sem parenteses aninhados) de TranslatedText/AmountToWord -> "" (preserva o resto da formula)
      t = OtherUfl.Replace(t, "\"\"");
      t = Regex.Replace(t, @"//[^\r\n]*", "");   // comentarios (podem conter nomes de UFL)
      if (Ufl.IsMatch(t))
        t = ff.Name == "desc" ? "uppercase(ToText({@soma}))" : "WhilePrintingRecords; \"\"";
      nf.Text = t;
      try { ffc.Modify(ff, nf); log.Append(who).Append(ff.Name).Append(" | "); }
      catch (Exception ex) { log.Append("ERRstub ").Append(who).Append(ff.Name).Append(": ").Append(ex.Message).Append(" | "); }
    }
  }

  public static string Build(string rptPath, string outPath) {
    var log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      StubAll(rcd.DataDefController.FormulaFieldController, rcd.DataDefController.DataDefinition.FormulaFields, "", log);
      foreach (string sn in rcd.SubreportController.GetSubreportNames()) {
        var sub = rcd.SubreportController.GetSubreport(sn);
        StubAll(sub.DataDefController.FormulaFieldController, sub.DataDefController.DataDefinition.FormulaFields, sn + "/", log);
      }
      string dir = System.IO.Path.GetDirectoryName(outPath); string nm = System.IO.Path.GetFileName(outPath); object od = dir;
      rcd.SaveAs(nm, ref od, 0);
      log.Append("saved -> ").Append(outPath);
      eng.Close();
    } catch (Exception ex) { log.Append("FATAL: ").Append(ex.Message); }
    return log.ToString();
  }
}

