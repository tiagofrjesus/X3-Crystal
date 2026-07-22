// X3RptBuildLogoSub — tenta construir um .rpt standalone "carrier" para um subreport logo (ABLOB)
// clonando a tabela ABLOB (com a ConnectionInfo já definida) do subreport embutido (logo2/logo3)
// para dentro de um novo documento (a partir do seed em branco), e gravar esse novo documento em
// disco, para depois ser importado via ImportSubreportEx no documento principal.
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptBuildLogoSub {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  public static string Build(string sourceRptPath, string subName, string seedPath, string outPath,
                             string workServer, string workDb, string workUser, string workPass) {
    log = new StringBuilder();
    try {
      var srcEng = new Eng.ReportDocument();
      srcEng.Load(sourceRptPath);
      var srcRcd = srcEng.ReportClientDocument;
      var subDoc = srcRcd.SubreportController.GetSubreport(subName);
      var srcTable = subDoc.DatabaseController.Database.Tables[0];
      var srcFilterText = subDoc.DataDefController.DataDefinition.RecordFilter.FreeEditingText;
      Lg("got source table " + srcTable.Name + " filter=[" + srcFilterText + "]");

      var newEng = new Eng.ReportDocument();
      newEng.Load(seedPath);
      var newRcd = newEng.ReportClientDocument;
      Lg("seed loaded");

      // 1) add a LIVE table (reachable OLE DB) just to satisfy AddTable's connectivity check
      var logon = new PropertyBagClass(); logon.Add("Provider", "SQLOLEDB"); logon.Add("Data Source", workServer); logon.Add("Initial Catalog", workDb); logon.Add("Integrated Security", "False");
      var attr = new PropertyBagClass(); attr.Add("Database DLL", "crdb_ado.dll"); attr.Add("QE_DatabaseName", workDb); attr.Add("QE_DatabaseType", "OLE DB (ADO)"); attr.Add("QE_ServerDescription", workServer); attr.Add("QE_SQLDB", "True"); attr.Add("SSO Enabled", "False"); attr.Add("QE_LogonProperties", logon);
      var ci = new ConnectionInfoClass(); ci.Attributes = attr; ci.UserName = workUser; ci.Password = workPass; ci.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
      var liveTbl = new CommandTableClass();
      liveTbl.Name = "ABLOB"; liveTbl.Alias = "ABLOB"; liveTbl.ConnectionInfo = ci;
      liveTbl.CommandText = "SELECT UPDTICK_0,CODBLB_0,IDENT1_0,IDENT2_0,IDENT3_0,NAMBLB_0,TYPBLB_0,CREUSR_0,CREDAT_0,UPDUSR_0,CREDATTIM_0,UPDDATTIM_0,AUUID_0,CNTTYP_0,BLOB_0 FROM TEB.ABLOB";
      newRcd.DatabaseController.AddTable(liveTbl, null);
      Lg("AddTable(live ABLOB via OLEDB) OK");
      ISCRTable liveAdded = null;
      foreach (ISCRTable t in newRcd.Database.Tables) if (t.Name == "ABLOB") liveAdded = t;

      // 2) repoint to a CommandTable pointing at the ORIGINAL design-time DSN (ADX_CS_X3V7) — matches
      //    the connection the rest of the main report already uses. SetTableLocation between two
      //    CommandTable objects does NOT validate connectivity (unlike a plain linked Table), so this
      //    works even though ADX_CS_X3V7 isn't reachable/registered on this dev machine.
      var srcAttr = srcTable.ConnectionInfo.Attributes;
      string dsnName = (string)srcAttr["QE_ServerDescription"];
      var dsnLogon = new PropertyBagClass(); dsnLogon.Add("DSN", dsnName); dsnLogon.Add("Database", ""); dsnLogon.Add("UseDSNProperties", "False");
      var dsnAttr = new PropertyBagClass();
      dsnAttr.Add("Database DLL", "crdb_odbc.dll");
      dsnAttr.Add("QE_DatabaseName", "");
      dsnAttr.Add("QE_DatabaseType", "ODBC (RDO)");
      dsnAttr.Add("QE_ServerDescription", dsnName);
      dsnAttr.Add("QE_SQLDB", "True");
      dsnAttr.Add("SSO Enabled", "False");
      dsnAttr.Add("QE_LogonProperties", dsnLogon);
      var dsnCi = new ConnectionInfoClass();
      dsnCi.Attributes = dsnAttr; dsnCi.UserName = srcTable.ConnectionInfo.UserName; dsnCi.Password = "";
      dsnCi.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
      var dsnCmdTbl = new CommandTableClass();
      dsnCmdTbl.Name = "ABLOB"; dsnCmdTbl.Alias = "ABLOB"; dsnCmdTbl.ConnectionInfo = dsnCi;
      dsnCmdTbl.CommandText = liveTbl.CommandText;
      newRcd.DatabaseController.SetTableLocation(liveAdded, dsnCmdTbl);
      Lg("SetTableLocation -> ODBC DSN " + (string)srcAttr["QE_ServerDescription"] + " OK");

      // 3) add a BlobField bound to ABLOB.BLOB_0 in the detail section
      ISCRTable finalTbl = null;
      foreach (ISCRTable t in newRcd.Database.Tables) finalTbl = t; // only one table
      ISCRField blobField = null;
      foreach (ISCRField f in finalTbl.DataFields) if (f.Name == "BLOB_0") blobField = f;
      var rd = newRcd.ReportDefinition;
      Section detailSec = null;
      foreach (Section s in rd.DetailArea.Sections) { detailSec = s; break; }
      var bf = new BlobFieldObjectClass();
      bf.DataSourceName = blobField.FormulaForm; bf.Kind = CrReportObjectKindEnum.crReportObjectKindBlobField;
      bf.Left = 0; bf.Top = 0; bf.Width = 2101; bf.Height = 852; bf.Name = "BLOB01";
      newRcd.ReportDefController.ReportObjectController.Add(bf, detailSec, -1);
      Lg("BlobField added to detail section");

      // 4) declare the X3DOS parameter (same as source) and set the record filter (same formula)
      try {
        var pf = new ParameterFieldClass();
        pf.Name = "X3DOS"; pf.Type = CrFieldValueTypeEnum.crFieldValueTypeStringField;
        pf.ParameterType = CrParameterFieldTypeEnum.crParameterFieldTypeReportParameter; pf.AllowNullValue = true;
        var dv1 = new ParameterFieldDiscreteValueClass(); dv1.Value = "";
        pf.DefaultValues.Add(dv1);
        newRcd.DataDefController.ParameterFieldController.Add(pf);
        Lg("parameter X3DOS added");
      } catch (Exception ex) { Lg("parameter X3DOS FAIL: " + ex.Message); }
      try {
        newRcd.DataDefController.DataDefinition.RecordFilter.FreeEditingText = srcFilterText;
        Lg("record filter set");
      } catch (Exception ex) { Lg("record filter FAIL: " + ex.Message); }

      string dir = System.IO.Path.GetDirectoryName(outPath);
      string nm = System.IO.Path.GetFileName(outPath);
      object od = dir;
      newRcd.SaveAs(nm, ref od, 0);
      Lg("SaveAs OK -> " + outPath + " exists=" + System.IO.File.Exists(outPath));

      newEng.Close();
      srcEng.Close();
    } catch (Exception ex) {
      Lg("FATAL: " + ex.Message);
      Lg(ex.StackTrace);
    }
    return log.ToString();
  }
}
