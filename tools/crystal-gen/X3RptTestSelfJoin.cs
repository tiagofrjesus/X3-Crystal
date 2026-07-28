// X3RptTestSelfJoin -- teste isolado: constroi um .rpt minimo so com PAYMENTD/PAYMENTD_CUM (self
// join, LeftOuter Equal + <= via RecordSelectionFormula) apontando ao servidor .204/teb onde o
// caso real ja foi confirmado por SQL, filtra para o recibo RPMB-26E01/01682, e tenta EXPORTAR
// (refresh real de dados) para confirmar o valor exato do Sum(AMTLIN_0 acumulado).
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptTestSelfJoin {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  public static string Build(string seedPath, string outPath,
                              string workServer, string workDb, string workUser, string workPass) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(seedPath);
      var rcd = eng.ReportClientDocument;
      Lg("seed loaded");

      var logon = new PropertyBagClass();
      logon.Add("Provider", "SQLOLEDB"); logon.Add("Data Source", workServer);
      logon.Add("Initial Catalog", workDb); logon.Add("Integrated Security", "False");
      logon.Add("User ID", workUser); logon.Add("Password", workPass);
      var attr = new PropertyBagClass();
      attr.Add("Database DLL", "crdb_ado.dll"); attr.Add("QE_DatabaseName", workDb);
      attr.Add("QE_DatabaseType", "OLE DB (ADO)"); attr.Add("QE_ServerDescription", workServer);
      attr.Add("QE_SQLDB", "True"); attr.Add("SSO Enabled", "False"); attr.Add("QE_LogonProperties", logon);
      var ci = new ConnectionInfoClass();
      ci.Attributes = attr; ci.UserName = workUser; ci.Password = workPass;
      ci.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;

      var t1 = new TableClass();
      t1.Name = "PAYMENTD"; t1.Alias = "PAYMENTD"; t1.QualifiedName = "TEB.PAYMENTD";
      t1.ConnectionInfo = ci;
      rcd.DatabaseController.AddTable(t1, null);
      Lg("AddTable PAYMENTD OK");

      var ci2 = new ConnectionInfoClass(); ci2.Attributes = attr; ci2.UserName = workUser; ci2.Password = workPass; ci2.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
      var t2 = new TableClass();
      t2.Name = "PAYMENTD"; t2.Alias = "PAYMENTD_CUM"; t2.QualifiedName = "TEB.PAYMENTD";
      t2.ConnectionInfo = ci2;
      rcd.DatabaseController.AddTable(t2, null);
      Lg("AddTable PAYMENTD_CUM OK");

      var srcNames = new StringsClass(); srcNames.Add("VCRNUM_0"); srcNames.Add("DUDNUM_0");
      var tgtNames = new StringsClass(); tgtNames.Add("VCRNUM_0"); tgtNames.Add("DUDNUM_0");
      var lk = new TableLinkClass();
      lk.SourceTableAlias = "PAYMENTD_CUM"; lk.TargetTableAlias = "PAYMENTD";
      lk.SourceFieldNames = srcNames; lk.TargetFieldNames = tgtNames;
      lk.JoinType = CrTableJoinTypeEnum.crTableJoinTypeLeftOuterJoin;
      rcd.DatabaseController.AddTableLink(lk);
      Lg("AddTableLink OK");

      rcd.DataDefController.RecordFilterController.SetFormulaText(
        "{PAYMENTD.VCRNUM_0} = 'FTR-E0126/002850' and {PAYMENTD.NUM_0} = 'RPMB-26E01/01682'" +
        " and ({PAYMENTD_CUM.NUM_0} <= {PAYMENTD.NUM_0} or IsNull({PAYMENTD_CUM.NUM_0}))");
      Lg("RecordFilter set (filtra para o caso real: recibo 01682, fatura FTR-E0126/002850)");

      // Group por VCRNUM_0 (para o Sum(..., {PAYMENTD.VCRNUM_0}) ser sintaticamente valido)
      rcd.DataDefController.GroupController.AddByName(-1, "{PAYMENTD.VCRNUM_0}", (CrDateConditionEnum)0);
      Lg("Group por PAYMENTD.VCRNUM_0 adicionado");

      rcd.DataDefController.FormulaFieldController.AddByName(
        "cumAmt", "Sum({PAYMENTD_CUM.AMTLIN_0}, {PAYMENTD.VCRNUM_0})", CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      Lg("formula cumAmt criada");

      // campo no detail para o valor sair no export
      Section detSec = null;
      foreach (Section s in rcd.ReportDefinition.DetailArea.Sections) { detSec = s; break; }
      var df = new FieldObjectClass();
      df.DataSource = "{@cumAmt}"; df.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeNumberField;
      df.Kind = CrReportObjectKindEnum.crReportObjectKindField;
      df.Left = 0; df.Top = 0; df.Width = 2000; df.Height = 240; df.Name = "cumAmtFld";
      rcd.ReportDefController.ReportObjectController.Add(df, detSec, -1);

      var df2 = new FieldObjectClass();
      df2.DataSource = "{PAYMENTD.NUM_0}"; df2.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeStringField;
      df2.Kind = CrReportObjectKindEnum.crReportObjectKindField;
      df2.Left = 2100; df2.Top = 0; df2.Width = 2000; df2.Height = 240; df2.Name = "numFld";
      rcd.ReportDefController.ReportObjectController.Add(df2, detSec, -1);
      Lg("campos de teste adicionados ao detail");

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

  public static string ExportText(string rptPath, string txtOutPath, string workUser, string workPass) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      foreach (Eng.Table t in eng.Database.Tables) {
        var logon = t.LogOnInfo;
        logon.ConnectionInfo.UserID = workUser;
        logon.ConnectionInfo.Password = workPass;
        t.ApplyLogOnInfo(logon);
      }
      Lg("logon aplicado a " + eng.Database.Tables.Count + " tabela(s)");
      var opts = new CrystalDecisions.Shared.ExportOptions();
      var diskOpts = new CrystalDecisions.Shared.DiskFileDestinationOptions();
      diskOpts.DiskFileName = txtOutPath;
      opts.ExportDestinationType = CrystalDecisions.Shared.ExportDestinationType.DiskFile;
      opts.ExportFormatType = CrystalDecisions.Shared.ExportFormatType.Text;
      opts.DestinationOptions = diskOpts;
      eng.Export(opts);
      Lg("Export TEXT OK -> " + txtOutPath + " exists=" + System.IO.File.Exists(txtOutPath));
      eng.Close();
    } catch (Exception ex) {
      Lg("FATAL: " + ex.Message);
      if (ex.InnerException != null) Lg("inner: " + ex.InnerException.Message);
      Lg(ex.StackTrace);
    }
    return log.ToString();
  }
}
