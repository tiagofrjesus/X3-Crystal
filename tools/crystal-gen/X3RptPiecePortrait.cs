// X3RptPiecePortrait — converte o TEB_PIECE.rpt de paisagem para retrato A4 e acrescenta
// a informação da empresa no rodapé (nome, morada, código postal/cidade, NIF), replicando
// a lógica já usada em TEB_PAG.rpt (fórmula lbl_companyinfo + tabela BPADDRESS ligada à
// FACILITY do estabelecimento sob o alias BPADDRESS_FCY).
//
// A tabela nova entra como TABELA NATIVA (não Command) — lição já registada em
// Reports-TEB/TEB_ITM_ETIQx60.txt: o print engine do X3 REMAPEIA em runtime a ligação de
// tabelas nativas para o DSN do folder (por isso funciona mesmo com um DSN de build local),
// mas NÃO remapeia CommandTables (ficam coladas ao DSN gravado e falham o logon fora do
// ambiente onde foram gravadas). Mesmo padrão comprovado em X3RptAddTariffView.cs.
// Compilar via Add-Type em PowerShell 32-BIT.
using System;
using System.Text;
using System.Collections.Generic;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptPiecePortrait {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

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

  public static string Build(string rptPath, string outPath, string workDsn, string workDb, string workUser, string workPass){
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded");

      ISCRTable facility = null;
      foreach (ISCRTable t in rcd.Database.Tables) if (t.Name == "FACILITY" && t.Alias == "FACILITY") facility = t;
      if (facility == null) throw new Exception("FACILITY não encontrada no .rpt");

      // 1) BPADDRESS como tabela NATIVA (alias BPADDRESS_FCY) — ligação de build via DSN local
      //    alcançável, só para o AddTable descobrir os campos; em runtime o engine do X3
      //    substitui a ligação pela do folder, como faz às restantes tabelas do relatório.
      var logon = new PropertyBagClass(); logon.Add("DSN", workDsn); logon.Add("Database", workDb); logon.Add("UseDSNProperties", "False"); logon.Add("UID", workUser); logon.Add("PWD", workPass);
      var attr = new PropertyBagClass(); attr.Add("Database DLL", "crdb_odbc.dll"); attr.Add("QE_DatabaseName", workDb); attr.Add("QE_DatabaseType", "ODBC (RDO)"); attr.Add("QE_ServerDescription", workDsn); attr.Add("QE_SQLDB", "True"); attr.Add("SSO Enabled", "False"); attr.Add("QE_LogonProperties", logon);
      var ci = new ConnectionInfoClass(); ci.Attributes = attr; ci.UserName = workUser; ci.Password = workPass; ci.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;

      var nt = new TableClass();
      nt.Name = "BPADDRESS";
      nt.Alias = "BPADDRESS_FCY";               // as fórmulas referem {BPADDRESS_FCY.*}
      nt.QualifiedName = "TEB.BPADDRESS";        // schema.objeto, sem BD -> portável dev/prod
      nt.ConnectionInfo = ci;
      rcd.DatabaseController.AddTable(nt, null);
      Lg("addtable BPADDRESS_FCY (nativa)");

      ISCRTable bpaFcy = null;
      foreach (ISCRTable t in rcd.Database.Tables) if (t.Name == "BPADDRESS" && t.Alias == "BPADDRESS_FCY") bpaFcy = t;
      if (bpaFcy == null) throw new Exception("BPADDRESS_FCY não foi adicionada");

      // 2) LEFT OUTER JOIN FACILITY(BPAADD_0,FCY_0) -> BPADDRESS_FCY(BPAADD_0,BPANUM_0) — igual ao TEB_PAG
      var link = new TableLinkClass();
      link.SourceTableAlias = "FACILITY";
      link.TargetTableAlias = "BPADDRESS_FCY";
      var src = new StringsClass(); src.Add("BPAADD_0"); src.Add("FCY_0");
      var dst = new StringsClass(); dst.Add("BPAADD_0"); dst.Add("BPANUM_0");
      link.SourceFieldNames = src;
      link.TargetFieldNames = dst;
      link.JoinType = CrTableJoinTypeEnum.crTableJoinTypeLeftOuterJoin;
      rcd.DatabaseController.AddTableLink(link);
      Lg("link FACILITY->BPADDRESS_FCY");

      // 3) fórmula lbl_companyinfo (texto idêntico ao TEB_PAG.rpt)
      rcd.DataDefController.FormulaFieldController.AddByName(
        "lbl_companyinfo",
        "WhilePrintingRecords;\r\n" +
        "local stringVar txtformejuridique;\r\n\r\n" +
        "txtformejuridique := {COMPANY.CPYNAM_0} + \" - \" + {BPADDRESS_FCY.BPAADDLIG_0} + \" - \" + {BPADDRESS_FCY.POSCOD_0} + \"-\" + {BPADDRESS_FCY.CTY_0} + \" - \" + {COMPANY.NID_0};",
        CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      Lg("formula lbl_companyinfo");

      // 4) Retrato A4 (dissociado da impressora, tal como o padrão "landscape robusto" mas invertido)
      var po = rcd.PrintOutputController.GetPrintOptions();
      po.PaperSize = CrPaperSizeEnum.crPaperSizePaperA4;
      po.PaperOrientation = CrPaperOrientationEnum.crPaperOrientationPortrait;
      po.DissociatePageSizeAndPrinterPaperSize = true;
      rcd.PrintOutputController.ModifyPrintOptions(po);
      rcd.PrintOutputController.ModifyUserPaperSize(16838, 11906); // (altura,largura) twips A4 retrato
      rcd.PrintOutputController.ModifyPaperOrientation(CrPaperOrientationEnum.crPaperOrientationPortrait);
      Lg("portrait A4");

      // 5) reescalar horizontalmente TODOS os objetos (Field/Text/Box/Line/Subreport) via Clone+Modify.
      //    Nota: para objetos Line e Box, o RAS SDK mantém a margem direita ORIGINAL (Right = OldLeft+
      //    OldWidth) e só desloca o Left; a Width fica recalculada como OldRight-NewLeft em vez do valor
      //    pedido (Add() de uma Line nova também falha sempre nesta versão do SDK, testado). Sem efeito
      //    visual real aqui: são só réguas/fundos decorativos que, ou já nasciam com Right dentro da
      //    nova largura de página (ficam OK), ou tinham Right>11906 mas Left=0 — ficam simplesmente
      //    cortados na margem da página retrato, visualmente idênticos a um objeto já reescalado.
      double R = 11906.0 / 16838.0;
      int nObj = 0, nErr = 0;
      var allObjs = new List<ISCRReportObject>();
      foreach (var area in AllAreas(rcd.ReportDefinition)) {
        if (area == null) continue;
        foreach (Section s in area.Sections) foreach (ISCRReportObject ro in s.ReportObjects) allObjs.Add(ro);
      }
      foreach (var ro in allObjs) {
        try {
          var clone = (ISCRReportObject)ro.Clone(true);
          clone.Left = (int)Math.Round(ro.Left * R);
          // Texto estático (legendas): o design original já vinha com a caixa no limite justo
          // do texto (ex. "Estab." em 480 twips) — encolher a largura corta o texto ("Esta").
          // Só reposicionar (Left), manter a largura original; há folga de sobra entre colunas
          // no layout de origem para absorver isto sem sobrepor o objeto seguinte.
          if (ro.Kind != CrReportObjectKindEnum.crReportObjectKindText) {
            clone.Width = (int)Math.Round(ro.Width * R);
          }
          rcd.ReportDefController.ReportObjectController.Modify(ro, clone);
          nObj++;
        } catch (Exception e) { nErr++; Lg("scale ERR " + ro.Name + ": " + e.Message); }
      }
      Lg("scaled " + nObj + " objects total (" + nErr + " erros) R=" + R.ToString("0.0000"));

      // 6) campo da info da empresa no rodapé (espaço livre acima do Subreport3/logo, que começa em T=280)
      Section pfSec = null; foreach (Section s in rcd.ReportDefinition.PageFooterArea.Sections) { pfSec = s; break; }
      var fo = new FieldObjectClass();
      fo.DataSource = "{@lbl_companyinfo}";
      fo.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeStringField;
      fo.Kind = CrReportObjectKindEnum.crReportObjectKindField;
      fo.Left = 105; fo.Top = 0; fo.Width = 11700; fo.Height = 220; fo.Name = "lblcompanyinfo1";
      try { fo.Format.HorizontalAlignment = CrAlignmentEnum.crAlignmentLeft; } catch {}
      rcd.ReportDefController.ReportObjectController.Add(fo, pfSec, -1);
      Lg("field lbl_companyinfo no rodapé");

      // 7) Higiene: limpar as credenciais da ligação da BPADDRESS_FCY antes de gravar (o engine do
      //    X3 sobrepõe a ligação em runtime; não queremos a password gravada no ficheiro final).
      try {
        var cleanLogon = new PropertyBagClass(); cleanLogon.Add("DSN", workDsn); cleanLogon.Add("Database", workDb); cleanLogon.Add("UseDSNProperties", "False");
        var cleanAttr = new PropertyBagClass(); cleanAttr.Add("Database DLL", "crdb_odbc.dll"); cleanAttr.Add("QE_DatabaseName", workDb); cleanAttr.Add("QE_DatabaseType", "ODBC (RDO)"); cleanAttr.Add("QE_ServerDescription", workDsn); cleanAttr.Add("QE_SQLDB", "True"); cleanAttr.Add("SSO Enabled", "False"); cleanAttr.Add("QE_LogonProperties", cleanLogon);
        var cleanCi = new ConnectionInfoClass(); cleanCi.Attributes = cleanAttr; cleanCi.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
        rcd.DatabaseController.ModifyTableConnectionInfo("BPADDRESS_FCY", cleanCi);
        Lg("limpeza creds BPADDRESS_FCY");
      } catch (Exception e) { Lg("limpeza creds ERR:" + e.Message); }

      string dir = System.IO.Path.GetDirectoryName(outPath); string nm = System.IO.Path.GetFileName(outPath); object od = dir;
      rcd.SaveAs(nm, ref od, 0);
      Lg("saved -> " + outPath);
      eng.Close();
    } catch (Exception ex) {
      Lg("FATAL: " + ex.Message);
      Lg(ex.StackTrace);
    }
    return log.ToString();
  }
}
