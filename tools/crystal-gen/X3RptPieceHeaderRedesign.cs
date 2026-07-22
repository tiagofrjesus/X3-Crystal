// X3RptPieceHeaderRedesign — redesenha o cabeçalho do documento no TEB_PIECE.rpt (já em retrato):
// bloco da empresa à esquerda, terceiro (1ª linha da peça) à direita, e liberta a coluna
// "Terceiro" da tabela de detalhe (fica só Descrição + valores).
// Compilar via Add-Type em PowerShell 32-BIT.
using System;
using System.Text;
using System.Collections.Generic;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptPieceHeaderRedesign {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }
  static CrystalDecisions.ReportAppServer.ClientDoc.ISCDReportClientDocument RCD;

  static ISCRReportObject Find(ReportDefinition rd, string name) {
    foreach (var area in AllAreas(rd)) {
      if (area == null) continue;
      foreach (Section s in area.Sections) foreach (ISCRReportObject ro in s.ReportObjects) if (ro.Name == name) return ro;
    }
    return null;
  }
  static Section FindSection(ReportDefinition rd, string name) {
    foreach (var area in AllAreas(rd)) {
      if (area == null) continue;
      foreach (Section s in area.Sections) if (s.Name == name) return s;
    }
    return null;
  }
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

  static void Move(ReportDefinition rd, string name, int l, int t, int w) {
    var ro = Find(rd, name);
    if (ro == null) { Lg("move " + name + " NAO ENCONTRADO"); return; }
    var clone = (ISCRReportObject)ro.Clone(true);
    clone.Left = l; clone.Top = t; clone.Width = w;
    RCD.ReportDefController.ReportObjectController.Modify(ro, clone);
  }

  static void AddField(Section sec, string dataSource, CrFieldValueTypeEnum ty, int l, int t, int w, int h, string name) {
    var fo = new FieldObjectClass();
    fo.DataSource = dataSource; fo.FieldValueType = ty; fo.Kind = CrReportObjectKindEnum.crReportObjectKindField;
    fo.Left = l; fo.Top = t; fo.Width = w; fo.Height = h; fo.Name = name;
    RCD.ReportDefController.ReportObjectController.Add(fo, sec, -1);
  }

  // texto estático: SimpleTextObject/Paragraphs fazem CRASH (lição já registada no projeto) ->
  // usar sempre formula-field com literal + FieldObject, como em X3Rpt.cs.
  static int lblN = 0;
  static void AddText(Section sec, string text, int l, int t, int w, int h) {
    string fn = "hdrLbl" + (lblN++);
    string esc = text.Replace("'", "''");
    RCD.DataDefController.FormulaFieldController.AddByName(fn, "'" + esc + "'", CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
    AddField(sec, "{@" + fn + "}", CrFieldValueTypeEnum.crFieldValueTypeStringField, l, t, w, h, fn);
  }

  public static string Build(string rptPath, string outPath, string workDsn, string workDb, string workUser, string workPass) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      RCD = eng.ReportClientDocument;
      Lg("loaded");

      // 1) tabela nativa BPARTNER (alias BPARTNER_HDR) — LEFT OUTER JOIN a partir de GACCENTRYD.BPR_0
      var logon = new PropertyBagClass(); logon.Add("DSN", workDsn); logon.Add("Database", workDb); logon.Add("UseDSNProperties", "False"); logon.Add("UID", workUser); logon.Add("PWD", workPass);
      var attr = new PropertyBagClass(); attr.Add("Database DLL", "crdb_odbc.dll"); attr.Add("QE_DatabaseName", workDb); attr.Add("QE_DatabaseType", "ODBC (RDO)"); attr.Add("QE_ServerDescription", workDsn); attr.Add("QE_SQLDB", "True"); attr.Add("SSO Enabled", "False"); attr.Add("QE_LogonProperties", logon);
      var ci = new ConnectionInfoClass(); ci.Attributes = attr; ci.UserName = workUser; ci.Password = workPass; ci.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;

      var nt = new TableClass();
      nt.Name = "BPARTNER";
      nt.Alias = "BPARTNER_HDR";
      nt.QualifiedName = "TEB.BPARTNER";
      nt.ConnectionInfo = ci;
      RCD.DatabaseController.AddTable(nt, null);
      Lg("addtable BPARTNER_HDR (nativa)");

      var link = new TableLinkClass();
      link.SourceTableAlias = "GACCENTRYD";
      link.TargetTableAlias = "BPARTNER_HDR";
      var src = new StringsClass(); src.Add("BPR_0");
      var dst = new StringsClass(); dst.Add("BPRNUM_0");
      link.SourceFieldNames = src;
      link.TargetFieldNames = dst;
      link.JoinType = CrTableJoinTypeEnum.crTableJoinTypeLeftOuterJoin;
      RCD.DatabaseController.AddTableLink(link);
      Lg("link GACCENTRYD->BPARTNER_HDR");

      // higiene: limpar credenciais (o print engine do X3 fornece-as em runtime)
      try {
        var cleanLogon = new PropertyBagClass(); cleanLogon.Add("DSN", workDsn); cleanLogon.Add("Database", workDb); cleanLogon.Add("UseDSNProperties", "False");
        var cleanAttr = new PropertyBagClass(); cleanAttr.Add("Database DLL", "crdb_odbc.dll"); cleanAttr.Add("QE_DatabaseName", workDb); cleanAttr.Add("QE_DatabaseType", "ODBC (RDO)"); cleanAttr.Add("QE_ServerDescription", workDsn); cleanAttr.Add("QE_SQLDB", "True"); cleanAttr.Add("SSO Enabled", "False"); cleanAttr.Add("QE_LogonProperties", cleanLogon);
        var cleanCi = new ConnectionInfoClass(); cleanCi.Attributes = cleanAttr; cleanCi.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
        RCD.DatabaseController.ModifyTableConnectionInfo("BPARTNER_HDR", cleanCi);
        Lg("limpeza creds BPARTNER_HDR");
      } catch (Exception e) { Lg("limpeza creds ERR:" + e.Message); }

      // 2) fórmulas do cabeçalho
      var fc = RCD.DataDefController.FormulaFieldController;
      fc.AddByName("hdrCompCidade", "{BPADDRESS_FCY.POSCOD_0} + \"-\" + {BPADDRESS_FCY.CTY_0}", CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      fc.AddByName("hdrCompNif", "\"NIF \" + {COMPANY.NID_0}", CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      fc.AddByName("hdrTerceiro", "{BPARTNER_HDR.BPRNUM_0} + \" - \" + {BPARTNER_HDR.BPRNAM_0}", CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      fc.AddByName("hdrTerceiroNif", "if {BPARTNER_HDR.EECNUM_0} <> \"\" then \"NIF \" + {BPARTNER_HDR.EECNUM_0} else \"\"", CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      Lg("formulas cabecalho");

      var rd = RCD.ReportDefinition;

      // 3) remover a coluna "Terceiro" da tabela de detalhe (subreport por linha + legenda) e alargar Descrição
      var subrep4 = Find(rd, "Subreport4");
      if (subrep4 != null) { RCD.ReportDefController.ReportObjectController.Remove(subrep4); Lg("removido Subreport4 (BPARTER.rpt por linha)"); }
      var texte30 = Find(rd, "Texte30");
      if (texte30 != null) { RCD.ReportDefController.ReportObjectController.Remove(texte30); Lg("removido Texte30 (legenda Terceiro)"); }
      Move(rd, "Texte31", 600, 90, 4300);   // legenda "Descrição" mais larga
      Move(rd, "Champ35", 600, 15, 4300);   // {GACCENTRYD.DES_0} mais largo
      Lg("Descricao alargada");

      // 4) reposicionar os campos existentes do cabeçalho do documento numa grelha de 2 linhas
      Move(rd, "Texte18", 105, 980, 850);    // "Doc. nr. :"
      Move(rd, "Champ22", 955, 980, 450);    // TYP_0
      Move(rd, "Champ23", 1405, 980, 2000);  // NUM_0
      Move(rd, "Texte21", 3550, 980, 750);   // "Diário :"
      Move(rd, "Champ26", 4300, 980, 500);   // JOU_0
      Move(rd, "Texte26", 4950, 980, 750);   // "Estab. :"
      Move(rd, "Champ31", 5700, 980, 450);   // FCY_0
      Move(rd, "Champ32", 6150, 980, 1600);  // FACILITY.FCYSHO_0
      Move(rd, "Texte23", 7900, 980, 750);   // "Divisa :"
      Move(rd, "Champ28", 8650, 980, 450);   // CUR_0
      Move(rd, "Texte24", 9250, 980, 1150);  // "Categoria :"
      Move(rd, "Champ29", 10400, 980, 1300); // @categ

      Move(rd, "Texte19", 105, 1210, 1000);   // "Data doc. :"
      Move(rd, "Champ24", 1105, 1210, 900);   // ACCDAT_0
      Move(rd, "Texte20", 2150, 1210, 1100);  // "Data de vencim. :"
      Move(rd, "Champ25", 3250, 1210, 900);   // DUDDAT_0
      Move(rd, "Texte22", 4300, 1210, 1000);  // "Referência :"
      Move(rd, "Champ27", 5300, 1210, 3100);  // REF_0
      Move(rd, "Texte25", 8550, 1210, 800);   // "Status :"
      Move(rd, "Champ30", 9350, 1210, 2350);  // @Etat
      Lg("grelha de metadados reposicionada");

      // 5) bloco NOVO: empresa à esquerda, terceiro (1ª linha) à direita
      var sec13 = FindSection(rd, "Section13");
      if (sec13 == null) throw new Exception("Section13 (cabecalho do documento) nao encontrada");

      AddField(sec13, "{COMPANY.CPYNAM_0}", CrFieldValueTypeEnum.crFieldValueTypeStringField, 105, 0, 5500, 220, "hdrCompNome");
      AddField(sec13, "{BPADDRESS_FCY.BPAADDLIG_0}", CrFieldValueTypeEnum.crFieldValueTypeStringField, 105, 230, 5500, 220, "hdrCompMorada");
      AddField(sec13, "{@hdrCompCidade}", CrFieldValueTypeEnum.crFieldValueTypeStringField, 105, 460, 5500, 220, "hdrCompCidadeF");
      AddField(sec13, "{@hdrCompNif}", CrFieldValueTypeEnum.crFieldValueTypeStringField, 105, 690, 5500, 220, "hdrCompNifF");

      AddText(sec13, "Terceiro:", 6200, 0, 1000, 220);
      AddField(sec13, "{@hdrTerceiro}", CrFieldValueTypeEnum.crFieldValueTypeStringField, 7250, 0, 4550, 220, "hdrTerceiroF");
      AddField(sec13, "{@hdrTerceiroNif}", CrFieldValueTypeEnum.crFieldValueTypeStringField, 6200, 230, 5600, 220, "hdrTerceiroNifF");
      Lg("bloco empresa/terceiro adicionado");

      // 6) aumentar a altura da secção para caber o novo bloco + grelha
      sec13.Height = 1550;
      Lg("altura Section13 = " + sec13.Height);

      string dir = System.IO.Path.GetDirectoryName(outPath); string nm = System.IO.Path.GetFileName(outPath); object od = dir;
      RCD.SaveAs(nm, ref od, 0);
      Lg("saved -> " + outPath);
      eng.Close();
    } catch (Exception ex) {
      Lg("FATAL: " + ex.Message);
      Lg(ex.StackTrace);
    }
    return log.ToString();
  }
}
