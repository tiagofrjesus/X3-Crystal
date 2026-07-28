// X3RptBuildRecPendHist — constrói um .rpt standalone "carrier" para o subreport auxiliar
// RecPendHist: soma o valor pago (PAYMENTD.AMTLIN_0) de TODOS os recibos já emitidos contra uma
// fatura (open item GACCDUDATE, identificado por VCRNUM_0+DUDNUM_0) cujo numero de recibo (NUM_0)
// seja <= ao recibo atualmente impresso (p_NUM) — i.e. "quanto estava pago logo apos ESTE recibo".
// O resultado fica numa shared numbervar gValorLiqHist, para o relatorio principal (TEB_REC.rpt)
// ler na formula valorLiq em vez de recalcular a partir das suas proprias linhas PAYMENTD (que so
// contêm o recibo atual, nunca os anteriores — essa é a causa do bug original).
//
// Padrao AddTable(CommandTable via OLEDB alcancavel) -> SetTableLocation(CommandTable via ODBC DSN)
// replicado de X3RptBuildLogoSub.cs (unico jeito confirmado de nao disparar validacao de
// conectividade no SetTableLocation quando o DSN do X3 nao resolve nesta maquina).
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptBuildRecPendHist {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }
  static ISCRField Fld(ISCRTable t,string n){ foreach(ISCRField f in t.DataFields) if(f.Name==n) return f; return null; }

  public static string Build(string seedPath, string outPath, string dsnName,
                              string workServer, string workDb, string workUser, string workPass) {
    log = new StringBuilder();
    const string sql = "SELECT NUM_0, VCRNUM_0, DUDNUM_0, AMTLIN_0 FROM TEB.PAYMENTD";
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(seedPath);
      var rcd = eng.ReportClientDocument;
      Lg("seed loaded");

      // 1) AddTable via OLE DB alcancavel (so para obter os campos)
      var logon = new PropertyBagClass(); logon.Add("Provider", "SQLOLEDB"); logon.Add("Data Source", workServer); logon.Add("Initial Catalog", workDb); logon.Add("Integrated Security", "False");
      var attr = new PropertyBagClass(); attr.Add("Database DLL", "crdb_ado.dll"); attr.Add("QE_DatabaseName", workDb); attr.Add("QE_DatabaseType", "OLE DB (ADO)"); attr.Add("QE_ServerDescription", workServer); attr.Add("QE_SQLDB", "True"); attr.Add("SSO Enabled", "False"); attr.Add("QE_LogonProperties", logon);
      var ci = new ConnectionInfoClass(); ci.Attributes = attr; ci.UserName = workUser; ci.Password = workPass; ci.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
      var liveTbl = new CommandTableClass(); liveTbl.Name = "PAYMENTD"; liveTbl.Alias = "PAYMENTD"; liveTbl.ConnectionInfo = ci; liveTbl.CommandText = sql;
      rcd.DatabaseController.AddTable(liveTbl, null);
      Lg("AddTable(live PAYMENTD via OLEDB) OK");
      ISCRTable liveAdded = null;
      foreach (ISCRTable t in rcd.Database.Tables) if (t.Name == "PAYMENTD") liveAdded = t;

      // 2) parametros
      Action<string,CrFieldValueTypeEnum,object> addParam = (pname,ty,defval) => {
        var pf = new ParameterFieldClass(); pf.Name = pname; pf.Type = ty;
        pf.ParameterType = CrParameterFieldTypeEnum.crParameterFieldTypeReportParameter; pf.AllowNullValue = true;
        var dv1 = new ParameterFieldDiscreteValueClass(); dv1.Value = defval; pf.DefaultValues.Add(dv1);
        var dv2 = new ParameterFieldDiscreteValueClass(); dv2.Value = defval; pf.CurrentValues.Add(dv2);
        rcd.DataDefController.ParameterFieldController.Add(pf);
        Lg("param " + pname + " added");
      };
      addParam("p_VCRNUM", CrFieldValueTypeEnum.crFieldValueTypeStringField, "");
      addParam("p_DUDNUM", CrFieldValueTypeEnum.crFieldValueTypeNumberField, 0.0);
      addParam("p_NUM",    CrFieldValueTypeEnum.crFieldValueTypeStringField, "");

      // 3) record selection (nivel Engine — comprovado persistente em X3Rpt.cs)
      eng.RecordSelectionFormula =
        "{PAYMENTD.VCRNUM_0} = {?p_VCRNUM} and {PAYMENTD.DUDNUM_0} = {?p_DUDNUM} and {PAYMENTD.NUM_0} <= {?p_NUM}";
      Lg("recordselection set");

      // 4) formula que acumula o total pago (WhilePrintingRecords) numa shared var, colocada
      //    (invisivel, tamanho minimo) no ReportFooter para garantir que é avaliada apos todos os
      //    registos do subreport serem processados.
      rcd.DataDefController.FormulaFieldController.AddByName(
        "setShared",
        "WhilePrintingRecords;\r\nshared numbervar gValorLiqHist;\r\ngValorLiqHist := Sum({PAYMENTD.AMTLIN_0});\r\ngValorLiqHist",
        CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      Lg("formula setShared added");

      var rd = rcd.ReportDefinition;
      Section ftrSec = null;
      foreach (Section s in rd.ReportFooterArea.Sections) { ftrSec = s; break; }
      var fo = new FieldObjectClass();
      fo.DataSource = "{@setShared}"; fo.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeNumberField;
      fo.Kind = CrReportObjectKindEnum.crReportObjectKindField;
      fo.Left = 0; fo.Top = 0; fo.Width = 50; fo.Height = 50; fo.Name = "setSharedFld";
      rcd.ReportDefController.ReportObjectController.Add(fo, ftrSec, -1);
      Lg("setShared field placed in ReportFooter");

      // 5) repoint p/ ODBC DSN do X3 (mesma logica de X3RptBuildLogoSub: CommandTable->CommandTable
      //    nao valida conectividade no SetTableLocation, ao contrario de uma tabela ligada normal)
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
      dsnCi.Attributes = dsnAttr; dsnCi.UserName = workUser; dsnCi.Password = "";
      dsnCi.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
      var dsnCmdTbl = new CommandTableClass();
      dsnCmdTbl.Name = "PAYMENTD"; dsnCmdTbl.Alias = "PAYMENTD"; dsnCmdTbl.ConnectionInfo = dsnCi; dsnCmdTbl.CommandText = sql;
      rcd.DatabaseController.SetTableLocation(liveAdded, dsnCmdTbl);
      Lg("SetTableLocation -> ODBC DSN " + dsnName + " OK");

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
