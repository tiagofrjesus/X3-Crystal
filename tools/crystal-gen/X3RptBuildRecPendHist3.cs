// X3RptBuildRecPendHist3 -- v3: identico ao v2, EXCETO que a tabela nativa e importada com
// Alias="PAYMENTD_HIST" em vez de "PAYMENTD". Motivo (hipotese de causa raiz do ERR 504 "Missing
// parameter values" em producao real com TEB_REC.rpt): o RecPendHist e o UNICO subreport neste
// .rpt cuja tabela PROPRIA tem o MESMO Name+Alias ("PAYMENTD") de uma tabela ja existente no
// relatorio PRINCIPAL -- IVA/IVAC usam "PAYVAT" (nome unico, sem colisao), logo.rpt usa "ABLOB"
// (nome unico, sem colisao). So RecPendHist colide. Suspeita: o motor de impressao/RAS pode ficar
// confuso a propagar o VALOR do SubreportLink (MainReportFieldName {PAYMENTD.X} do relatorio
// PRINCIPAL) para o parametro do subreport quando o subreport TAMBEM tem uma tabela com o MESMO
// alias -- falha silenciosa de binding, o parametro fica sem valor (PromptToUser=True nunca
// alimentado) -> "Missing parameter values". Fix: renomear o alias da tabela DENTRO do subreport
// para nao colidir, mantendo MainReportFieldName inalterado (continua a apontar para a tabela
// PAYMENTD do relatorio PRINCIPAL, que e a correta).
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptBuildRecPendHist3 {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  public static string Build(string seedPath, string outPath,
                              string workDsn, string workDb, string workUser, string workPass) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(seedPath);
      var rcd = eng.ReportClientDocument;
      Lg("seed loaded");

      var logon = new PropertyBagClass(); logon.Add("DSN", workDsn); logon.Add("Database", workDb); logon.Add("UseDSNProperties", "False"); logon.Add("UID", workUser); logon.Add("PWD", workPass);
      var attr = new PropertyBagClass(); attr.Add("Database DLL", "crdb_odbc.dll"); attr.Add("QE_DatabaseName", workDb); attr.Add("QE_DatabaseType", "ODBC (RDO)"); attr.Add("QE_ServerDescription", workDsn); attr.Add("QE_SQLDB", "True"); attr.Add("SSO Enabled", "False"); attr.Add("QE_LogonProperties", logon);
      var ci = new ConnectionInfoClass(); ci.Attributes = attr; ci.UserName = workUser; ci.Password = workPass; ci.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;

      var nt = new TableClass();
      nt.Name = "PAYMENTD";
      nt.Alias = "PAYMENTD_HIST";              // <-- MUDANCA v3: alias sem colisao com o principal
      nt.QualifiedName = "TEB.PAYMENTD";
      nt.ConnectionInfo = ci;
      rcd.DatabaseController.AddTable(nt, null);
      Lg("AddTable(native PAYMENTD via ODBC " + workDsn + ", Alias=PAYMENTD_HIST) OK");

      var rd = rcd.ReportDefinition;
      ISCRField amtlinField = null;
      foreach (ISCRField f in nt.DataFields) if (f.Name == "AMTLIN_0") amtlinField = f;
      if (amtlinField == null) {
        foreach (ISCRTable t in rcd.Database.Tables)
          foreach (ISCRField f in t.DataFields) if (f.Name == "AMTLIN_0") amtlinField = f;
      }

      Section detSec = null;
      foreach (Section s in rd.DetailArea.Sections) { detSec = s; break; }
      var df = new FieldObjectClass();
      df.DataSource = amtlinField.FormulaForm; df.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeNumberField;
      df.Kind = CrReportObjectKindEnum.crReportObjectKindField;
      df.Left = 0; df.Top = 0; df.Width = 50; df.Height = 50; df.Name = "detAMTLIN";
      rcd.ReportDefController.ReportObjectController.Add(df, detSec, -1);
      Lg("campo de detalhe AMTLIN_0 adicionado (forca iteracao), FormulaForm=" + amtlinField.FormulaForm);

      rcd.DataDefController.FormulaFieldController.AddByName(
        "setShared",
        "WhilePrintingRecords;\r\nshared numbervar gValorLiqHist;\r\ngValorLiqHist := Sum({PAYMENTD_HIST.AMTLIN_0});\r\ngValorLiqHist",
        CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      Lg("formula setShared added (referencia PAYMENTD_HIST)");

      Section rptFtrSec = null;
      foreach (Section s in rd.ReportFooterArea.Sections) { rptFtrSec = s; break; }
      var fo = new FieldObjectClass();
      fo.DataSource = "{@setShared}"; fo.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeNumberField;
      fo.Kind = CrReportObjectKindEnum.crReportObjectKindField;
      fo.Left = 100; fo.Top = 0; fo.Width = 50; fo.Height = 50; fo.Name = "setSharedFld";
      rcd.ReportDefController.ReportObjectController.Add(fo, rptFtrSec, -1);
      Lg("setShared field placed in ReportFooter");

      var cleanLogon = new PropertyBagClass(); cleanLogon.Add("DSN", workDsn); cleanLogon.Add("Database", workDb); cleanLogon.Add("UseDSNProperties", "False");
      var cleanAttr = new PropertyBagClass(); cleanAttr.Add("Database DLL", "crdb_odbc.dll"); cleanAttr.Add("QE_DatabaseName", workDb); cleanAttr.Add("QE_DatabaseType", "ODBC (RDO)"); cleanAttr.Add("QE_ServerDescription", workDsn); cleanAttr.Add("QE_SQLDB", "True"); cleanAttr.Add("SSO Enabled", "False"); cleanAttr.Add("QE_LogonProperties", cleanLogon);
      var cleanCi = new ConnectionInfoClass(); cleanCi.Attributes = cleanAttr; cleanCi.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
      rcd.DatabaseController.ModifyTableConnectionInfo("PAYMENTD_HIST", cleanCi);
      Lg("credenciais limpas");

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
