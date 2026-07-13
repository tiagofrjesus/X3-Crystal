// X3RptAddTariff — adiciona a lógica de tarifa de venda (SPRICCONF/SPRICFICH/SPRICLIST)
// ao TEB_ITM_ETIQx60.rpt: Command "TARIFA" (preço válido do PLI fixo) + LEFT OUTER JOIN
// a ITMMASTER + formula PrecoBase (fallback p/ ITMSALES.BASPRI_0) + reescreve PVPcIVA/lbl_PVP.
// Compilar via Add-Type em PowerShell 32-BIT.
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptAddTariff {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }
  static ISCRField Fld(ISCRTable t,string n){ foreach(ISCRField f in t.DataFields) if(f.Name==n) return f; return null; }

  // embedCreds=true SÓ para builds de teste local (o ExportToDisk local precisa de UID/PWD
  // no logon bag). Para o ficheiro FINAL de produção tem de ser false: o print engine do X3
  // fornece as credenciais em runtime, e UID/PWD embutidos causam "Falha de logon" no servidor
  // (o ZEXTRART.rpt, que funciona no X3, foi gravado com o logon bag limpo).
  static CrystalDecisions.ReportAppServer.DataDefModel.ConnectionInfo OdbcCi(string dsn,string db,string user,string pass,bool embedCreds){
    var logon=new PropertyBagClass(); logon.Add("DSN",dsn); logon.Add("Database",db); logon.Add("UseDSNProperties","False");
    if (embedCreds) { logon.Add("UID",user); logon.Add("PWD",pass); }
    var attr=new PropertyBagClass(); attr.Add("Database DLL","crdb_odbc.dll"); attr.Add("QE_DatabaseName",db); attr.Add("QE_DatabaseType","ODBC (RDO)"); attr.Add("QE_ServerDescription",dsn); attr.Add("QE_SQLDB","True"); attr.Add("SSO Enabled","False"); attr.Add("QE_LogonProperties",logon);
    var ci=new ConnectionInfoClass(); ci.Attributes=attr; ci.UserName=user; ci.Password=pass; ci.Kind=CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
    return ci;
  }

  public static string Build(string rptPath, string outPath,
                              string pli, string tariffSql,
                              string workServer, string workDb, string workUser, string workPass,
                              string dsn, string prodDb, string prodUser, string prodPass, bool doRepoint){
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded");

      // ligação ODBC (RDO) alcançável p/ build/teste local — DSN local (ex. TEST_TEB211) que
      // resolve para o mesmo servidor de produção (192.168.1.211/tebx3). OLE DB (SQLOLEDB) foi
      // tentado primeiro mas falhou a logon nesta máquina (provider ausente/obsoleto) — ODBC
      // é o que está confirmado a funcionar aqui (ver memória x3-crystal-rpt-pipeline).
      var ci = OdbcCi(workServer, workDb, workUser, workPass, true);

      // 0) Converter as tabelas nativas já existentes (ITMMASTER/TABUNIT/ITMSALES) em Command
      //    (SELECT * passthrough) — necessário porque SetTableLocation numa Table nativa tenta
      //    revalidar o schema no destino; numa CommandTable não revalida (só troca CommandText/conn),
      //    o que é o que permite o repoint final "cego" para o DSN de produção (inacessível daqui).
      string[] passthroughTables = { "ITMMASTER", "TABUNIT", "ITMSALES" };
      foreach (var tn in passthroughTables) {
        ISCRTable old=null; foreach (ISCRTable t in rcd.Database.Tables) if (t.Name==tn) old=t;
        if (old==null) { Lg("passthrough "+tn+" SKIP (não encontrada)"); continue; }
        var nt = new CommandTableClass(); nt.Name=tn; nt.Alias=old.Alias; nt.ConnectionInfo=ci; nt.CommandText="SELECT * FROM TEB."+tn;
        rcd.DatabaseController.SetTableLocation(old, nt);
        Lg("passthrough->Command "+tn);
      }

      // 1) Command TARIFA — mesma ligação OLE DB alcançável
      var cmdTbl=new CommandTableClass(); cmdTbl.Name="TARIFA"; cmdTbl.Alias="TARIFA"; cmdTbl.ConnectionInfo=ci; cmdTbl.CommandText=tariffSql;
      rcd.DatabaseController.AddTable(cmdTbl,null); Lg("addtable TARIFA");

      ISCRTable itmmaster=null, tarifa=null;
      foreach (ISCRTable t in rcd.Database.Tables) {
        if (t.Name=="ITMMASTER") itmmaster=t;
        if (t.Name=="TARIFA") tarifa=t;
      }
      if (itmmaster==null) throw new Exception("ITMMASTER não encontrada no .rpt");
      if (tarifa==null) throw new Exception("TARIFA não foi adicionada");

      // 2) LEFT OUTER JOIN ITMMASTER.ITMREF_0 -> TARIFA.ITMREF_0
      var link = new TableLinkClass();
      link.SourceTableAlias = itmmaster.Alias;
      link.TargetTableAlias = tarifa.Alias;
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

      // 4) Reescrever PVPcIVA e lbl_PVP para usarem {@PrecoBase} em vez de {ITMSALES.BASPRI_0}
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

      // 4b) Sub-report TEXTETRAD.rpt (AVWTEXTRA) — também aponta p/ um DSN local inacessível
      //     (ADX_X3DVLP); alinhar com a mesma ligação p/ conseguir testar/enviar coerentemente.
      try {
        var sub = rcd.SubreportController.GetSubreport("TEXTETRAD.rpt");
        ISCRTable oldAv=null; foreach (ISCRTable t in sub.DatabaseController.Database.Tables) if (t.Name=="AVWTEXTRA") oldAv=t;
        if (oldAv!=null) {
          var nt = new CommandTableClass(); nt.Name="AVWTEXTRA"; nt.Alias=oldAv.Alias; nt.ConnectionInfo=ci; nt.CommandText="SELECT * FROM TEB.AVWTEXTRA";
          sub.DatabaseController.SetTableLocation(oldAv, nt);
          Lg("subreport AVWTEXTRA passthrough->Command (build conn)");
        } else Lg("subreport AVWTEXTRA não encontrada");
      } catch (Exception e) { Lg("subreport fix ERR:"+e.Message); }

      // 5) Repoint de TODAS as tabelas p/ o DSN ODBC de produção (ADX_CRCNN_TEBX3)
      if (doRepoint) {
        ISCRTable[] all;
        { var lst = new System.Collections.Generic.List<ISCRTable>(); foreach (ISCRTable t in rcd.Database.Tables) lst.Add(t); all = lst.ToArray(); }
        foreach (var t in all) {
          try {
            string cmdText = (t.Name=="TARIFA") ? tariffSql : ("SELECT * FROM TEB."+t.Name);
            var nt = new CommandTableClass(); nt.Name=t.Name; nt.Alias=t.Alias; nt.CommandText=cmdText; nt.ConnectionInfo=OdbcCi(dsn,prodDb,prodUser,prodPass,false);
            rcd.DatabaseController.SetTableLocation(t, nt);
            Lg("repoint "+t.Name+" -> "+dsn);
          } catch (Exception e) { Lg("repoint "+t.Name+" ERR:"+e.Message); }
        }
        try {
          var sub = rcd.SubreportController.GetSubreport("TEXTETRAD.rpt");
          ISCRTable oldAv=null; foreach (ISCRTable t in sub.DatabaseController.Database.Tables) if (t.Name=="AVWTEXTRA") oldAv=t;
          if (oldAv!=null) {
            var nt = new CommandTableClass(); nt.Name="AVWTEXTRA"; nt.Alias=oldAv.Alias; nt.CommandText="SELECT * FROM TEB.AVWTEXTRA"; nt.ConnectionInfo=OdbcCi(dsn,prodDb,prodUser,prodPass,false);
            sub.DatabaseController.SetTableLocation(oldAv, nt);
            Lg("repoint subreport AVWTEXTRA -> "+dsn);
          }
        } catch (Exception e) { Lg("repoint subreport AVWTEXTRA ERR:"+e.Message); }
      } else Lg("repoint SKIPPED (modo local)");

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
