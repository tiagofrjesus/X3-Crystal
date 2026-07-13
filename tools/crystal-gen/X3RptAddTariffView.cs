// X3RptAddTariffView — v2 da lógica de tarifa no TEB_ITM_ETIQx60.rpt.
// Lição do trace do print server: o engine do X3 REMAPEIA em runtime a ligação de
// TABELAS NATIVAS para o DSN do folder (por isso o original funcionava com DSNs
// bogus), mas NÃO consegue remapear CommandTables (a v1, toda em Commands, ficou
// colada ao DSN de produção e falhou o logon no ambiente de dev).
// v2: parte do .rpt ORIGINAL intacto (tabelas nativas + subreport sem repoint) e
// só ACRESCENTA a view TEB.ZETIQTARIFA como tabela nativa (alias TARIFA) + LEFT
// OUTER JOIN + fórmulas PrecoBase/PVPcIVA/lbl_PVP (iguais às já validadas).
// Compilar via Add-Type em PowerShell 32-BIT.
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptAddTariffView {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  public static string Build(string rptPath, string outPath,
                              string workDsn, string workDb, string workUser, string workPass){
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded");

      ISCRTable itmmaster=null;
      foreach (ISCRTable t in rcd.Database.Tables) if (t.Name=="ITMMASTER") itmmaster=t;
      if (itmmaster==null) throw new Exception("ITMMASTER não encontrada no .rpt");

      // 1) Tabela NATIVA para a view — ligação de build via DSN local alcançável
      //    (só para o AddTable descobrir os campos; em runtime o engine do X3
      //    substitui a ligação pela do folder, como faz às restantes tabelas).
      //    UID/PWD no logon bag são necessários para o AddTable autenticar.
      var logon=new PropertyBagClass(); logon.Add("DSN",workDsn); logon.Add("Database",workDb); logon.Add("UseDSNProperties","False"); logon.Add("UID",workUser); logon.Add("PWD",workPass);
      var attr=new PropertyBagClass(); attr.Add("Database DLL","crdb_odbc.dll"); attr.Add("QE_DatabaseName",workDb); attr.Add("QE_DatabaseType","ODBC (RDO)"); attr.Add("QE_ServerDescription",workDsn); attr.Add("QE_SQLDB","True"); attr.Add("SSO Enabled","False"); attr.Add("QE_LogonProperties",logon);
      var ci=new ConnectionInfoClass(); ci.Attributes=attr; ci.UserName=workUser; ci.Password=workPass; ci.Kind=CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;

      var nt = new TableClass();
      nt.Name = "ZETIQTARIFA";
      nt.Alias = "TARIFA";                     // as fórmulas referem {TARIFA.PRI_0}
      nt.QualifiedName = "TEB.ZETIQTARIFA";    // schema.objeto, sem BD -> portável dev/prod
      nt.ConnectionInfo = ci;
      rcd.DatabaseController.AddTable(nt, null);
      Lg("addtable TARIFA (view nativa)");

      // 2) LEFT OUTER JOIN ITMMASTER.ITMREF_0 -> TARIFA.ITMREF_0
      var link = new TableLinkClass();
      link.SourceTableAlias = itmmaster.Alias;
      link.TargetTableAlias = "TARIFA";
      var srcNames = new StringsClass(); srcNames.Add("ITMREF_0");
      var dstNames = new StringsClass(); dstNames.Add("ITMREF_0");
      link.SourceFieldNames = srcNames;
      link.TargetFieldNames = dstNames;
      link.JoinType = CrTableJoinTypeEnum.crTableJoinTypeLeftOuterJoin;
      rcd.DatabaseController.AddTableLink(link);
      Lg("link ITMMASTER->TARIFA");

      // 3) Formula PrecoBase = preço da tarifa se existir, senão BASPRI
      rcd.DataDefController.FormulaFieldController.AddByName(
        "PrecoBase",
        "if not isnull({TARIFA.PRI_0}) then {TARIFA.PRI_0} else {ITMSALES.BASPRI_0}",
        CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      Lg("formula PrecoBase");

      // 4) Reescrever PVPcIVA e lbl_PVP para usarem {@PrecoBase}
      ISCRFormulaField ffPVPcIVA=null, ffLblPVP=null;
      foreach (ISCRFormulaField ff in rcd.DataDefController.DataDefinition.FormulaFields) {
        if (ff.Name=="PVPcIVA") ffPVPcIVA=ff;
        if (ff.Name=="lbl_PVP") ffLblPVP=ff;
      }
      if (ffPVPcIVA==null) throw new Exception("formula PVPcIVA não encontrada");
      if (ffLblPVP==null) throw new Exception("formula lbl_PVP não encontrada");

      var newPVPcIVA = new FormulaFieldClass();
      newPVPcIVA.Name = "PVPcIVA";
      newPVPcIVA.Text =
        "select {ITMMASTER.VACITM_0} \r\n" +
        "    case \"NOR\" : round({@PrecoBase}*1.23,2)\r\n" +
        "    case \"INT\" : round({@PrecoBase}*1.13,2)\r\n" +
        "    case \"RED\" : round({@PrecoBase}*1.06,2)\r\n" +
        "    default: {@PrecoBase}";
      rcd.DataDefController.FormulaFieldController.Modify(ffPVPcIVA, newPVPcIVA);
      Lg("formula PVPcIVA reescrita");

      var newLblPVP = new FormulaFieldClass();
      newLblPVP.Name = "lbl_PVP";
      newLblPVP.Text = "if {?civa} = true then totext({@PVPcIVA}) + \" € c/IVA \" else totext({@PrecoBase}) + \" € s/IVA\"";
      rcd.DataDefController.FormulaFieldController.Modify(ffLblPVP, newLblPVP);
      Lg("formula lbl_PVP reescrita");

      // 5) Higiene: tentar limpar as credenciais da ligação da TARIFA antes de gravar
      //    (o engine do X3 sobrepõe a ligação em runtime, mas não queremos a password
      //    gravada no ficheiro). ModifyTableConnectionInfo pode não persistir — o
      //    runner verifica o resultado no ficheiro gravado.
      try {
        var cleanLogon=new PropertyBagClass(); cleanLogon.Add("DSN",workDsn); cleanLogon.Add("Database",workDb); cleanLogon.Add("UseDSNProperties","False");
        var cleanAttr=new PropertyBagClass(); cleanAttr.Add("Database DLL","crdb_odbc.dll"); cleanAttr.Add("QE_DatabaseName",workDb); cleanAttr.Add("QE_DatabaseType","ODBC (RDO)"); cleanAttr.Add("QE_ServerDescription",workDsn); cleanAttr.Add("QE_SQLDB","True"); cleanAttr.Add("SSO Enabled","False"); cleanAttr.Add("QE_LogonProperties",cleanLogon);
        var cleanCi=new ConnectionInfoClass(); cleanCi.Attributes=cleanAttr; cleanCi.Kind=CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
        rcd.DatabaseController.ModifyTableConnectionInfo("TARIFA", cleanCi);
        Lg("limpeza creds TARIFA");
      } catch (Exception e) { Lg("limpeza creds ERR:"+e.Message); }

      string dir=System.IO.Path.GetDirectoryName(outPath); string nm=System.IO.Path.GetFileName(outPath); object od=dir;
      rcd.SaveAs(nm, ref od, 0);
      Lg("saved -> "+outPath);
      eng.Close();
    } catch (Exception ex) {
      Lg("FATAL: " + ex.Message);
      Lg(ex.StackTrace);
    }
    return log.ToString();
  }
}
