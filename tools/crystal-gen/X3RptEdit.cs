// X3RptEdit -- editor GENERICO de .rpt guiado por um ficheiro JSON de operacoes (evita escrever um
// script C# novo para cada alteracao de layout). Aplica as operacoes por ordem e grava noutro ficheiro.
// Incorpora as licoes de LESSONS.md: Clone+Modify para mexer em objetos, texto estatico como formula +
// FieldObject, caixas/linhas como FieldObject com Border, subreports por ImportSubreportEx + links.
//
// Operacoes suportadas ("op"):
//   remove        name                                  remove objeto (qualquer seccao)
//   set           name [l t w h font size bold italic color align border number]   Clone+Modify
//   label         section name text [l t w h ...estilo]   texto estatico (formula 'texto' + campo)
//   field         section name source [type] [l t w h ...estilo]   campo de BD/formula/special
//   box           section name [l t w h border color bg]  caixa/linha (campo vazio com borda)
//   formula       name text                             cria ou altera formula
//   sectionHeight section h
//   suppress      section value                         suprime/mostra seccao
//   group         index field                           muda o campo de agrupamento do grupo N
//   areaFormat    area [repeatHeader keepTogether]       opcoes de area de grupo
//   importSubreport name file section l t w h links:[[main,sub],...] filter
//   selection     text                                  record selection do relatorio principal
//   formulaReplace find replace [required]              regex (ignore case) em todas as formulas (principal+subreports)
//
// Estilo: font (nome), size, bold, italic, color "#RRGGBB", bg "#RRGGBB", align left|right|center,
// border none|box|top|bottom, number true (2 casas; separadores ficam os do servidor), suppressZero.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.Controllers;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.ReportAppServer.DataDefModel;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptEdit {
  static StringBuilder log;
  static ISCDReportClientDocument RCD;
  // controladores do documento alvo da operacao: relatorio principal ou subreport (op com "sub": nome)
  static DataDefController DDC;
  static ReportDefController2 RDC;
  static void Lg(string m) { log.Append(m).Append("\r\n"); }

  // ---------- helpers de leitura do JSON ----------
  static bool Has(Dictionary<string, object> o, string k) { return o.ContainsKey(k) && o[k] != null; }
  static string S(Dictionary<string, object> o, string k) { return Has(o, k) ? Convert.ToString(o[k]) : null; }
  static int I(Dictionary<string, object> o, string k, int d) { return Has(o, k) ? Convert.ToInt32(o[k]) : d; }
  static bool B(Dictionary<string, object> o, string k, bool d) { return Has(o, k) ? Convert.ToBoolean(o[k]) : d; }

  static uint Rgb(string hex) { // "#RRGGBB" -> COLORREF (0x00BBGGRR)
    hex = hex.TrimStart('#');
    uint r = Convert.ToUInt32(hex.Substring(0, 2), 16), g = Convert.ToUInt32(hex.Substring(2, 2), 16), b = Convert.ToUInt32(hex.Substring(4, 2), 16);
    return (b << 16) | (g << 8) | r;
  }

  // ---------- localizar seccoes / objetos (sempre "frescos") ----------
  static Section FindSection(string name) {
    foreach (Area a in RDC.ReportDefinition.Areas)
      foreach (Section s in a.Sections) if (s.Name == name) return s;
    throw new Exception("Seccao nao encontrada: " + name);
  }
  static Area FindArea(string name) {
    foreach (Area a in RDC.ReportDefinition.Areas) if (a.Name == name) return a;
    throw new Exception("Area nao encontrada: " + name);
  }
  static ReportObject FindObj(string name) {
    foreach (Area a in RDC.ReportDefinition.Areas)
      foreach (Section s in a.Sections)
        foreach (ReportObject ro in s.ReportObjects) if (ro.Name == name) return ro;
    return null;
  }

  // ---------- estilo ----------
  static void ApplyStyle(ISCRReportObject ro, Dictionary<string, object> o) {
    if (Has(o, "l")) ro.Left = I(o, "l", 0);
    if (Has(o, "t")) ro.Top = I(o, "t", 0);
    if (Has(o, "w")) ro.Width = I(o, "w", 0);
    if (Has(o, "h")) ro.Height = I(o, "h", 0);
    var fo = ro as ISCRFieldObject;
    var txo = ro as ISCRTextObject;
    if (txo != null && txo.FontColor != null && txo.FontColor.Font != null && (Has(o, "font") || Has(o, "size") || Has(o, "bold") || Has(o, "italic") || Has(o, "underline") || Has(o, "color"))) {
      var tfc = (FontColor)txo.FontColor.Clone(true);
      if (Has(o, "font")) tfc.Font.Name = S(o, "font");
      if (Has(o, "size")) tfc.Font.Size = Convert.ToDecimal(o["size"]);
      if (Has(o, "bold")) tfc.Font.Bold = B(o, "bold", false);
      if (Has(o, "italic")) tfc.Font.Italic = B(o, "italic", false);
      if (Has(o, "underline")) tfc.Font.Underline = B(o, "underline", false);
      if (Has(o, "color")) tfc.Color = Rgb(S(o, "color"));
      txo.FontColor = tfc;
    }
    if (fo != null && (Has(o, "font") || Has(o, "size") || Has(o, "bold") || Has(o, "italic") || Has(o, "underline") || Has(o, "color"))) {
      FontColor fc;
      if (fo.FontColor != null && fo.FontColor.Font != null) fc = (FontColor)fo.FontColor.Clone(true);
      else {   // objeto acabado de criar: ainda sem FontColor
        fc = new FontColorClass(); var fnt = new FontClass(); fnt.Name = "Arial"; fnt.Size = 10; fc.Font = fnt; fc.Color = 0;
      }
      if (Has(o, "font")) fc.Font.Name = S(o, "font");
      if (Has(o, "size")) fc.Font.Size = Convert.ToDecimal(o["size"]);
      if (Has(o, "bold")) fc.Font.Bold = B(o, "bold", false);
      if (Has(o, "italic")) fc.Font.Italic = B(o, "italic", false);
      if (Has(o, "underline")) fc.Font.Underline = B(o, "underline", false);
      if (Has(o, "color")) fc.Color = Rgb(S(o, "color"));
      fo.FontColor = fc;
    }
    if (Has(o, "hide")) {   // supressao estatica do objeto (true = escondido)
      var hf = (ObjectFormat)ro.Format.Clone(true);
      hf.EnableSuppress = B(o, "hide", false);
      ro.Format = hf;
    }
    if (Has(o, "align")) {
      var f = (ObjectFormat)ro.Format.Clone(true);
      string a = S(o, "align");
      f.HorizontalAlignment = a == "right" ? CrAlignmentEnum.crAlignmentRight :
                              a == "center" ? CrAlignmentEnum.crAlignmentHorizontalCenter : CrAlignmentEnum.crAlignmentLeft;
      ro.Format = f;
    }
    if (Has(o, "border") || Has(o, "bg")) {
      var b = (Border)ro.Border.Clone(true);
      if (Has(o, "border")) {
        string m = S(o, "border");
        var none = CrLineStyleEnum.crLineStyleNoLine; var one = CrLineStyleEnum.crLineStyleSingle;
        b.LeftLineStyle = m == "box" ? one : none; b.RightLineStyle = m == "box" ? one : none;
        b.TopLineStyle = (m == "box" || m == "top") ? one : none; b.BottomLineStyle = (m == "box" || m == "bottom") ? one : none;
        if (Has(o, "bordercolor")) b.BorderColor = Rgb(S(o, "bordercolor"));
      }
      if (Has(o, "bg")) b.BackgroundColor = Rgb(S(o, "bg"));
      else if (Has(o, "bgRaw")) b.BackgroundColor = 0xFFFFFFFF;   // sem fundo (transparente)
      ro.Border = b;
    }
    if (fo != null && Has(o, "number")) {
      var ff = (FieldFormat)fo.FieldFormat.Clone(true);
      var nf = ff.NumericFormat;
      nf.NDecimalPlaces = 2; nf.RoundingFormat = CrRoundingTypeEnum.crRoundingTypeRoundToHundredth;
      nf.EnableSuppressIfZero = B(o, "suppressZero", false);
      ff.NumericFormat = nf;
      fo.FieldFormat = ff;
    }
  }

  static CrFieldValueTypeEnum Typ(string t) {
    switch (t) {
      case "number": return CrFieldValueTypeEnum.crFieldValueTypeNumberField;
      case "date": return CrFieldValueTypeEnum.crFieldValueTypeDateField;
      case "datetime": return CrFieldValueTypeEnum.crFieldValueTypeDateTimeField;
      case "int": return CrFieldValueTypeEnum.crFieldValueTypeInt32sField;
      default: return CrFieldValueTypeEnum.crFieldValueTypeStringField;
    }
  }

  static void AddField(Dictionary<string, object> o, string source, CrFieldValueTypeEnum type) {
    var f = new FieldObjectClass();
    if (source.StartsWith("{@") && S(o, "type") == "auto")   // tipo real da formula (ex.: memo)
      foreach (ISCRFormulaField ff in DDC.DataDefinition.FormulaFields)
        if ("{@" + ff.Name + "}" == source) { type = ff.Type; Lg("    tipo de " + source + " = " + type); }
    f.DataSource = source; f.FieldValueType = type;
    f.Kind = CrReportObjectKindEnum.crReportObjectKindField;
    f.Name = S(o, "name");
    f.Left = I(o, "l", 0); f.Top = I(o, "t", 0); f.Width = I(o, "w", 1000); f.Height = I(o, "h", 220);
    // "back": true -> indice 0 da seccao (desenhado por baixo dos restantes objetos: faixas de fundo)
    RDC.ReportObjectController.Add(f, FindSection(S(o, "section")), B(o, "back", false) ? 0 : -1);
    // estilo via Clone+Modify (propriedades de fonte nao persistem bem no Add)
    var ro = FindObj(S(o, "name"));
    var clone = (ISCRReportObject)ro.Clone(true);
    if (Has(o, "copyFormatFrom")) {   // herda o formato do objeto substituido (supressao/condicoes, alinhamento...)
      var srcObj = FindObj(S(o, "copyFormatFrom"));
      if (srcObj == null) throw new Exception("copyFormatFrom: objeto nao encontrado " + S(o, "copyFormatFrom"));
      clone.Format = (ObjectFormat)srcObj.Format.Clone(true);
    }
    ApplyStyle(clone, o);
    RDC.ReportObjectController.Modify(ro, (ReportObject)clone);
    // o Add nasce com Arial 10 e o RAS estica a altura a essa fonte; com a fonte final ja aplicada, repor a geometria pedida
    var ro2 = FindObj(S(o, "name"));
    if (ro2.Height != I(o, "h", 220) || ro2.Top != I(o, "t", 0)) {
      var c2 = (ISCRReportObject)ro2.Clone(true);
      c2.Top = I(o, "t", 0); c2.Height = I(o, "h", 220); c2.Width = I(o, "w", 1000); c2.Left = I(o, "l", 0);
      RDC.ReportObjectController.Modify(ro2, (ReportObject)c2);
    }
  }

  static void SetFormula(string name, string text) {
    var ffc = DDC.FormulaFieldController;
    foreach (ISCRFormulaField ff in DDC.DataDefinition.FormulaFields)
      if (ff.Name == name) {
        var nf = new FormulaFieldClass(); nf.Name = name; nf.Text = text; nf.Syntax = ff.Syntax;
        ffc.Modify(ff, nf); return;
      }
    ffc.AddByName(name, text, CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
  }

  // tema: troca de cor de letra em massa ({ map: {"#330035":"#1F5FA8"}, bold, size, font }) em Field/Text;
  // NUNCA toca objetos certificados (nome PT* ou fonte TMPSRPT.PT*) nem os listados em "skip".
  static void Theme(Dictionary<string, object> o) {
    var map = new Dictionary<uint, uint>();
    if (Has(o, "map")) foreach (var kv in (Dictionary<string, object>)o["map"]) map[Rgb(kv.Key)] = Rgb(Convert.ToString(kv.Value));
    var skip = new HashSet<string>();
    if (Has(o, "skip")) foreach (object s in (IList)o["skip"]) skip.Add((string)s);
    string fontAll = S(o, "fontAll");
    var todo = new List<string>();
    foreach (Area a in RDC.ReportDefinition.Areas)
      foreach (Section s in a.Sections)
        foreach (ReportObject ro in s.ReportObjects) {
          if (skip.Contains(ro.Name) || ro.Name.StartsWith("PT")) continue;
          var fo = ro as ISCRFieldObject; var to = ro as ISCRTextObject;
          if (fo != null && fo.DataSource != null && fo.DataSource.Contains("TMPSRPT.PT")) continue;
          FontColor fc = fo != null ? fo.FontColor : (to != null ? to.FontColor : null);
          if (fc == null || fc.Font == null) continue;
          if (map.ContainsKey(fc.Color) || (fontAll != null && fc.Font.Name != fontAll)) todo.Add(ro.Name);
        }
    int n = 0;
    foreach (string nm in todo) {
      var ro = FindObj(nm);
      var clone = (ISCRReportObject)ro.Clone(true);
      var fo = clone as ISCRFieldObject; var to = clone as ISCRTextObject;
      var fc = (FontColor)(fo != null ? fo.FontColor : to.FontColor).Clone(true);
      if (map.ContainsKey(fc.Color)) { fc.Color = map[fc.Color]; if (Has(o, "bold")) fc.Font.Bold = B(o, "bold", false); }
      if (fontAll != null) fc.Font.Name = fontAll;
      if (fo != null) fo.FontColor = fc; else to.FontColor = fc;
      RDC.ReportObjectController.Modify(ro, (ReportObject)clone); n++;
    }
    Lg("    theme: " + n + " objeto(s)");
  }

  // substituicao por regex (ignore case) no texto de TODAS as formulas, do principal e dos subreports
  // objetos certificados AT (nome PT*, fonte TMPSRPT.PT* / @PT_*) nao se alteram nem removem, salvo "force": true
  static void GuardCert(ReportObject ro, Dictionary<string, object> o) {
    var fo = ro as ISCRFieldObject;
    bool cert = ro.Name.StartsWith("PT") || (fo != null && fo.DataSource != null && (fo.DataSource.Contains("TMPSRPT.PT") || fo.DataSource.StartsWith("{@PT_")));
    if (cert && !B(o, "force", false)) throw new Exception("objeto certificado AT (" + ro.Name + ") -- nao alterar (usar force so com autorizacao)");
  }
  // formulas de certificacao AT (PT_MENTION_*, isPt*, atcud*) NUNCA sao alteradas por substituicoes em massa
  static readonly System.Text.RegularExpressions.Regex CertFormula = new System.Text.RegularExpressions.Regex(@"^(PT_|isPt|atcud)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
  static string OnlyFormula;   // formulaReplace com "formula": so essa formula
  static int ReplaceInFormulas(FormulaFieldController ffc, Fields formulas, System.Text.RegularExpressions.Regex rx, string rep) {
    var hits = new List<ISCRFormulaField>();
    foreach (ISCRFormulaField ff in formulas)
      if (ff.Text != null && !CertFormula.IsMatch(ff.Name) && (OnlyFormula == null || ff.Name == OnlyFormula) && rx.IsMatch(ff.Text)) hits.Add(ff);
    foreach (var ff in hits) {
      var nf = new FormulaFieldClass(); nf.Name = ff.Name; nf.Syntax = ff.Syntax; nf.Text = rx.Replace(ff.Text, rep);
      ffc.Modify(ff, nf);
    }
    return hits.Count;
  }
  static void FormulaReplace(Dictionary<string, object> o) {
    var rx = new System.Text.RegularExpressions.Regex(S(o, "find"), System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    string rep = S(o, "replace") ?? "";
    OnlyFormula = S(o, "formula");
    int n =ReplaceInFormulas(DDC.FormulaFieldController, DDC.DataDefinition.FormulaFields, rx, rep);
    if (!B(o, "mainOnly", false)) foreach (string sn in RCD.SubreportController.GetSubreportNames()) {
      var sub = RCD.SubreportController.GetSubreport(sn);
      n += ReplaceInFormulas(sub.DataDefController.FormulaFieldController, sub.DataDefController.DataDefinition.FormulaFields, rx, rep);
    }
    Lg("    formulaReplace '" + S(o, "find") + "' -> " + n + " formula(s)");
    if (n == 0 && B(o, "required", false)) throw new Exception("nenhuma formula corresponde");
  }

  static ISCRField FindDbField(string formulaForm) {
    foreach (ISCRTable t in RCD.DatabaseController.Database.Tables)
      foreach (ISCRField f in t.DataFields) if (f.FormulaForm == formulaForm) return f;
    throw new Exception("Campo nao encontrado: " + formulaForm);
  }

  // ---------- tabela NATIVA nova (nunca Command) + link; credenciais limpas no fim ----------
  // { op:addTable, table:"BPADDRESS", alias:"BPADDRESS_BPR", schema:"TEB", dsn:"TEST_TEB211", db:"tebx3",
  //   user:"sa", pass:"...", from:"BPARTNER_HDR", fromFields:[..], toFields:[..], join:"leftouter|equal" }
  static PropertyBag Attrs(string dsn, string db, string user, string pass) {
    var logon = new PropertyBagClass(); logon.Add("DSN", dsn); logon.Add("Database", db); logon.Add("UseDSNProperties", "False");
    if (user != null) { logon.Add("UID", user); logon.Add("PWD", pass); }
    var attr = new PropertyBagClass(); attr.Add("Database DLL", "crdb_odbc.dll");
    attr.Add("QE_DatabaseName", db); attr.Add("QE_DatabaseType", "ODBC (RDO)");
    attr.Add("QE_ServerDescription", dsn); attr.Add("QE_SQLDB", "True"); attr.Add("SSO Enabled", "False");
    attr.Add("QE_LogonProperties", logon);
    return attr;
  }
  static void AddTable(Dictionary<string, object> o) {
    string dsn = S(o, "dsn"), db = S(o, "db"), alias = S(o, "alias"), pass = S(o, "pass");
    if (pass != null && pass.StartsWith("env:")) pass = Environment.GetEnvironmentVariable(pass.Substring(4)); // nao gravar passwords no spec
    var ci = new ConnectionInfoClass(); ci.Attributes = Attrs(dsn, db, S(o, "user"), pass);
    ci.UserName = S(o, "user"); ci.Password = pass; ci.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
    var nt = new TableClass();
    nt.Name = S(o, "table"); nt.Alias = alias; nt.QualifiedName = S(o, "schema") + "." + S(o, "table");
    nt.ConnectionInfo = ci;
    var lk = new TableLinkClass();
    lk.SourceTableAlias = S(o, "from"); lk.TargetTableAlias = alias;
    var src = new StringsClass(); foreach (object f in (IList)o["fromFields"]) src.Add((string)f);
    var dst = new StringsClass(); foreach (object f in (IList)o["toFields"]) dst.Add((string)f);
    lk.SourceFieldNames = src; lk.TargetFieldNames = dst;
    lk.JoinType = S(o, "join") == "equal" ? CrTableJoinTypeEnum.crTableJoinTypeEqualJoin : CrTableJoinTypeEnum.crTableJoinTypeLeftOuterJoin;
    var links = new TableLinksClass(); links.Add(lk);
    RCD.DatabaseController.AddTable(nt, links);
    var clean = new ConnectionInfoClass(); clean.Attributes = Attrs(dsn, db, null, null);
    clean.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
    RCD.DatabaseController.ModifyTableConnectionInfo(alias, clean);
  }

  // Copia temporaria do .rpt a importar SEM parametros "Pm-*" nem record selection: se o ficheiro ja
  // os trouxer, ao ligar o subreport o RAS cria "Pm-X??01" e deixa os antigos orfaos -> "Valores de
  // parametro ausentes" no export.
  static string CleanCarrier(string file) {
    var e = new Eng.ReportDocument();
    e.Load(file);
    var r = e.ReportClientDocument;
    r.DataDefController.RecordFilterController.SetFormulaText("");
    foreach (Area a in r.ReportDefController.ReportDefinition.Areas)
      if (a.Kind != CrAreaSectionKindEnum.crAreaSectionKindDetail)
        foreach (Section s in a.Sections) {
          var sf = (SectionFormat)s.Format.Clone(true); sf.EnableSuppress = true;
          r.ReportDefController.ReportSectionController.SetProperty(s, CrReportSectionPropertyEnum.crReportSectionPropertyFormat, sf);
        }
    var pms = new List<ISCRParameterField>();
    foreach (ISCRParameterField pf in r.DataDefController.DataDefinition.ParameterFields) if (pf.Name.StartsWith("Pm-")) pms.Add(pf);
    foreach (var pf in pms) r.DataDefController.ParameterFieldController.Remove(pf);
    string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "carrier_" + System.IO.Path.GetFileName(file));
    if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp);
    object od = System.IO.Path.GetDirectoryName(tmp);
    r.SaveAs(System.IO.Path.GetFileName(tmp), ref od, 0);
    e.Close();
    Lg("  carrier limpo: " + tmp + " (removidos " + pms.Count + " Pm-*)");
    return tmp;
  }

  // ---------- operacoes ----------
  static void Do(Dictionary<string, object> o) {
    string op = S(o, "op");
    if (Has(o, "sub")) { var sd = RCD.SubreportController.GetSubreport(S(o, "sub")); DDC = sd.DataDefController; RDC = sd.ReportDefController; }
    else { DDC = RCD.DataDefController; RDC = RCD.ReportDefController; }
    var roc = RDC.ReportObjectController;
    switch (op) {
      case "remove": {
        var ro = FindObj(S(o, "name"));
        if (ro == null) { Lg("  (nao existe) " + S(o, "name")); return; }
        GuardCert(ro, o);
        roc.Remove(ro); break;
      }
      case "set": {
        var ro = FindObj(S(o, "name"));
        if (ro == null) throw new Exception("Objeto nao encontrado: " + S(o, "name"));
        GuardCert(ro, o);
        var clone = (ISCRReportObject)ro.Clone(true);
        ApplyStyle(clone, o);
        roc.Modify(ro, (ReportObject)clone); break;
      }
      case "label": {
        string fn = "lbl_" + S(o, "name");
        SetFormula(fn, "'" + S(o, "text").Replace("'", "''") + "'");
        AddField(o, "{@" + fn + "}", CrFieldValueTypeEnum.crFieldValueTypeStringField); break;
      }
      case "field": AddField(o, S(o, "source"), Typ(S(o, "type"))); break;
      case "box": {
        SetFormula("fBoxEmpty", "ChrW(160)");
        if (!Has(o, "border")) o["border"] = "box";
        if (!Has(o, "bg")) o["bgRaw"] = true;
        // fonte minima: senao o Crystal estica a caixa a altura minima da fonte por omissao (Arial 10) e a seccao cresce
        if (!Has(o, "size")) { o["size"] = 1; o["font"] = "Arial"; }
        AddField(o, "{@fBoxEmpty}", CrFieldValueTypeEnum.crFieldValueTypeStringField); break;
      }
      case "formula": SetFormula(S(o, "name"), S(o, "text")); break;
      case "sectionHeight": {
        var s = FindSection(S(o, "section"));
        RDC.ReportSectionController.SetProperty(s, CrReportSectionPropertyEnum.crReportSectionPropertyHeight, I(o, "h", 220));
        break;
      }
      case "suppress": {
        var s = FindSection(S(o, "section"));
        var f = (SectionFormat)s.Format.Clone(true);
        f.EnableSuppress = B(o, "value", true);
        RDC.ReportSectionController.SetProperty(s, CrReportSectionPropertyEnum.crReportSectionPropertyFormat, f);
        break;
      }
      case "group": {
        int idx = I(o, "index", 0);
        var g = (Group)DDC.DataDefinition.Groups[idx];
        var ng = (Group)g.Clone(true);
        ng.ConditionField = (Field)FindDbField(S(o, "field"));
        DDC.GroupController.Modify(g, ng); break;
      }
      case "areaFormat": {
        var a = FindArea(S(o, "area"));
        var f = a.Format.Clone(true);
        if (Has(o, "repeatHeader")) f.GetType().InvokeMember("EnableRepeatGroupHeader", System.Reflection.BindingFlags.SetProperty, null, f, new object[] { B(o, "repeatHeader", true) });
        if (Has(o, "keepTogether")) f.GetType().InvokeMember("EnableKeepGroupTogether", System.Reflection.BindingFlags.SetProperty, null, f, new object[] { B(o, "keepTogether", false) });
        RDC.ReportAreaController.SetProperty(a, CrReportAreaPropertyEnum.crReportAreaPropertyFormat, f);
        break;
      }
      case "importSubreport": {
        string nm = S(o, "name");
        string file = S(o, "file");
        if (Has(o, "links")) file = CleanCarrier(file);
        RCD.SubreportController.ImportSubreportEx(nm, file, FindSection(S(o, "section")),
          I(o, "l", 0), I(o, "t", 0), I(o, "w", 1000), I(o, "h", 1000));
        if (Has(o, "links")) {
          var links = new SubreportLinksClass();
          foreach (object lo in (IList)o["links"]) {
            var pair = (IList)lo;
            var lk = new SubreportLinkClass();
            lk.MainReportFieldName = (string)pair[0];
            lk.SubreportFieldName = (string)pair[1];
            lk.LinkedParameterName = pair.Count > 2 ? (string)pair[2] : "{?Pm-" + ((string)pair[0]).Trim('{', '}') + "}";
            links.Add(lk);
          }
          // NAO usar SubreportController.SetSubreportLinks: cria parametros duplicados "Pm-...??01"
          // quando o subreport ja traz os Pm-* (caso do logo.rpt). Clone+Modify do objeto de colocacao.
          SubreportObject place = null;
          foreach (Area a in RDC.ReportDefinition.Areas)
            foreach (Section s in a.Sections)
              foreach (ReportObject ro in s.ReportObjects) {
                var so = ro as SubreportObject;
                if (so != null && so.SubreportName == nm) place = so;
              }
          var cl = (SubreportObject)((ISCRReportObject)place).Clone(true);
          cl.SubreportLinks = links;
          var nb = (Border)cl.Border.Clone(true);
          nb.LeftLineStyle = nb.RightLineStyle = nb.TopLineStyle = nb.BottomLineStyle = CrLineStyleEnum.crLineStyleNoLine;
          cl.Border = nb;
          RDC.ReportObjectController.Modify((ReportObject)place, (ReportObject)cl);
        }
        // O RAS da nomes novos aos parametros ligados ("{?Pm-X??01}") se o subreport ja tinha "{?Pm-X}".
        // Ler os nomes reais, usa-los no filtro e remover os Pm-* antigos que ficaram orfaos.
        var real = new Dictionary<string, string>();
        foreach (SubreportLink lk in RCD.SubreportController.GetSubreportLinks(nm)) {
          string ln = lk.LinkedParameterName; int q = ln.IndexOf("??");
          real[q > 0 ? ln.Substring(0, q) + "}" : ln] = ln;
        }
        var subDoc = RCD.SubreportController.GetSubreport(nm);
        if (Has(o, "filter")) {
          string flt = S(o, "filter");
          foreach (var kv in real) flt = flt.Replace(kv.Key, kv.Value);
          subDoc.DataDefController.RecordFilterController.SetFormulaText(flt);
          Lg("  filtro: " + flt);
        }
        var orphans = new List<ISCRParameterField>();
        foreach (ISCRParameterField pf in subDoc.DataDefController.DataDefinition.ParameterFields) {
          string ff = "{?" + pf.Name + "}";
          if (real.ContainsKey(ff) && real[ff] != ff) orphans.Add(pf);
        }
        foreach (var pf in orphans) {
          try { subDoc.DataDefController.ParameterFieldController.Remove(pf); Lg("  removido parametro orfao " + pf.Name); }
          catch (Exception e) { Lg("  (nao removido " + pf.Name + ": " + e.Message + ")"); }
        }
        break;
      }
      case "selection": DDC.RecordFilterController.SetFormulaText(S(o, "text")); break;
      case "addTable": AddTable(o); break;
      case "removeTable": {   // remove a tabela e os links dela
        ISCRTable tb = null;
        foreach (ISCRTable t in RCD.DatabaseController.Database.Tables) if (t.Alias == S(o, "alias")) tb = t;
        if (tb == null) throw new Exception("Tabela nao encontrada: " + S(o, "alias"));
        RCD.DatabaseController.RemoveTable(tb.Alias); break;
      }
      case "formulaReplace": FormulaReplace(o); break;   // { find (regex), replace, required }
      case "theme": Theme(o); break;                     // { map:{de:para}, bold, fontAll, skip:[..] }
      case "removeLink": {    // { from, to } -- remove a(s) ligacao(oes) entre as duas tabelas
        var rm = new List<TableLink>();
        foreach (TableLink l in RCD.DatabaseController.Database.TableLinks)
          if (l.SourceTableAlias == S(o, "from") && l.TargetTableAlias == S(o, "to")) rm.Add(l);
        if (rm.Count == 0) throw new Exception("ligacao nao encontrada");
        foreach (var l in rm) RCD.DatabaseController.RemoveTableLink(l);
        break;
      }
      case "addLink": {       // { from, to, fromFields:[..], toFields:[..], join }
        var lk = new TableLinkClass();
        lk.SourceTableAlias = S(o, "from"); lk.TargetTableAlias = S(o, "to");
        var src = new StringsClass(); foreach (object f in (IList)o["fromFields"]) src.Add((string)f);
        var dst = new StringsClass(); foreach (object f in (IList)o["toFields"]) dst.Add((string)f);
        lk.SourceFieldNames = src; lk.TargetFieldNames = dst;
        lk.JoinType = S(o, "join") == "leftouter" ? CrTableJoinTypeEnum.crTableJoinTypeLeftOuterJoin : CrTableJoinTypeEnum.crTableJoinTypeEqualJoin;
        RCD.DatabaseController.AddTableLink(lk); break;
      }
      default: throw new Exception("op desconhecida: " + op);
    }
  }

  public static string Run(string rptIn, string jsonPath, string rptOut) {
    log = new StringBuilder();
    var eng = new Eng.ReportDocument();
    try {
      eng.Load(rptIn);
      RCD = eng.ReportClientDocument;
      var ser = new JavaScriptSerializer();
      var ops = (IList)ser.DeserializeObject(System.IO.File.ReadAllText(jsonPath, Encoding.UTF8)) ;
      int n = 0, err = 0;
      foreach (object x in ops) {
        n++;
        var o = (Dictionary<string, object>)x;
        if (Has(o, "comment") && !Has(o, "op")) continue;
        string desc = S(o, "op") + " " + (S(o, "name") ?? S(o, "section") ?? S(o, "area") ?? "");
        try { Do(o); Lg("ok  " + n + " " + desc); }
        catch (Exception ex) { err++; Lg("ERR " + n + " " + desc + " : " + ex.Message); }
      }
      if (err > 0) { Lg("ABORTADO: " + err + " erro(s) -- nada gravado"); return log.ToString(); }
      string dir = System.IO.Path.GetDirectoryName(rptOut); string fn = System.IO.Path.GetFileName(rptOut); object od = dir;
      RCD.SaveAs(fn, ref od, 0);
      Lg("GRAVADO -> " + rptOut);
    } catch (Exception ex) { Lg("FATAL: " + ex.Message); }
    finally { eng.Close(); }
    return log.ToString();
  }
}
