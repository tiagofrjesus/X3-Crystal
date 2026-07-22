// X3RptPieceQaVisual — TESTE-ONLY, QA VISUAL: igual a X3RptPieceTestExport mas neutraliza a
// fórmula "typref" (usa TextOfChapter, que nunca resolve nesta máquina de dev) para permitir
// gerar um PDF/HTML local e confirmar visualmente o layout do cabeçalho. NUNCA usar esta
// alteração de fórmula no ficheiro final entregue — é só para inspeção visual local.
// Compilar via Add-Type em PowerShell 32-BIT.
using System;
using System.Text;
using System.Collections.Generic;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptPieceQaVisual {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  static CrystalDecisions.ReportAppServer.DataDefModel.ConnectionInfo OleDbCi(string workServer,string db,string user,string pass){
    var logon=new PropertyBagClass(); logon.Add("Provider","SQLOLEDB"); logon.Add("Data Source",workServer); logon.Add("Initial Catalog",db); logon.Add("Integrated Security","False");
    logon.Add("UID",user); logon.Add("PWD",pass);
    var attr=new PropertyBagClass(); attr.Add("Database DLL","crdb_ado.dll"); attr.Add("QE_DatabaseName",db); attr.Add("QE_DatabaseType","OLE DB (ADO)"); attr.Add("QE_ServerDescription",workServer); attr.Add("QE_SQLDB","True"); attr.Add("SSO Enabled","False"); attr.Add("QE_LogonProperties",logon);
    var ci=new ConnectionInfoClass(); ci.Attributes=attr; ci.UserName=user; ci.Password=pass; ci.Kind=CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
    return ci;
  }

  static Dictionary<string,string> BigTableFilter(string typDoc, string numDoc) {
    var d = new Dictionary<string,string>();
    string escT = typDoc.Replace("'", "''"), escN = numDoc.Replace("'", "''");
    d["GACCENTRY"]  = " WHERE TYP_0='" + escT + "' AND NUM_0='" + escN + "'";
    d["GACCENTRYD"] = " WHERE TYP_0='" + escT + "' AND NUM_0='" + escN + "'";
    d["GACCENTRYA"] = " WHERE TYP_0='" + escT + "' AND NUM_0='" + escN + "'";
    return d;
  }

  static void RepointAllTables(CrystalDecisions.ReportAppServer.Controllers.ISCRDatabaseController dbCtl, string workServer, string workDb, string workUser, string workPass, Dictionary<string,string> bigFilters) {
    var ci = OleDbCi(workServer, workDb, workUser, workPass);
    var all = new List<ISCRTable>();
    foreach (ISCRTable t in dbCtl.Database.Tables) all.Add(t);
    foreach (var t in all) {
      try {
        var nt = new CommandTableClass();
        nt.Name = t.Name; nt.Alias = t.Alias; nt.ConnectionInfo = ci;
        string filter; bigFilters.TryGetValue(t.Name, out filter);
        nt.CommandText = "SELECT * FROM TEB." + t.Name + (filter ?? "");
        dbCtl.SetTableLocation(t, nt);
        Lg("repoint " + t.Alias + " OK" + (filter != null ? " (filtrado)" : ""));
      } catch (Exception e) { Lg("repoint " + t.Alias + " ERR: " + e.Message); }
    }
  }

  public static string Export(string rptPath, string pdfPath, string workServer, string workDb, string workUser, string workPass, string typDoc, string numDoc) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded");

      var bigFilters = BigTableFilter(typDoc, numDoc);
      RepointAllTables(rcd.DatabaseController, workServer, workDb, workUser, workPass, bigFilters);

      ISCRDatabase db = rcd.Database;
      var linksToLoosen = new List<string[]>();
      linksToLoosen.Add(new[]{"GACCENTRY","AREPORTM"});
      linksToLoosen.Add(new[]{"COMPANY","AREPORTM"});
      linksToLoosen.Add(new[]{"GACCENTRYD","AFCTFCY"});
      linksToLoosen.Add(new[]{"AREPORTM","GJOURNAL"});
      foreach (var pair in linksToLoosen) {
        try {
          ISCRTableLink found = null;
          foreach (ISCRTableLink lk in db.TableLinks) {
            if (lk.SourceTableAlias == pair[0] && lk.TargetTableAlias == pair[1]) { found = lk; break; }
          }
          if (found == null) { Lg("link " + pair[0] + "->" + pair[1] + " não encontrado"); continue; }
          var srcNames = new StringsClass();
          foreach (string f in (System.Collections.IEnumerable)found.SourceFieldNames) srcNames.Add(f);
          var dstNames = new StringsClass();
          foreach (string f in (System.Collections.IEnumerable)found.TargetFieldNames) dstNames.Add(f);
          var nl = new TableLinkClass();
          nl.SourceTableAlias = found.SourceTableAlias;
          nl.TargetTableAlias = found.TargetTableAlias;
          nl.SourceFieldNames = srcNames;
          nl.TargetFieldNames = dstNames;
          nl.JoinType = CrTableJoinTypeEnum.crTableJoinTypeLeftOuterJoin;
          rcd.DatabaseController.ModifyTableLink((TableLink)found, (TableLink)nl);
          Lg("link " + pair[0] + "->" + pair[1] + " -> LEFT OUTER");
        } catch (Exception e) { Lg("loosen " + pair[0] + "->" + pair[1] + " ERR: " + e.Message); }
      }

      foreach (var lname in new[] { "logo1", "logo2", "logo3", "logoHdr2", "logoHdr3" }) {
        try {
          var sub = rcd.SubreportController.GetSubreport(lname);
          RepointAllTables(sub.DatabaseController, workServer, workDb, workUser, workPass, bigFilters);
          Lg("subreport " + lname + " repoint OK");
        } catch (Exception e) { Lg("subreport " + lname + " ERR (ignorado): " + e.Message); }
      }

      // QA-ONLY: neutralizar TODAS as formulas que usam TextOfChapter (nunca resolve localmente)
      try {
        var toFix = new List<ISCRFormulaField>();
        foreach (ISCRFormulaField ff in rcd.DataDefController.DataDefinition.FormulaFields) {
          if (ff.Text != null && ff.Text.IndexOf("TextOfChapter", StringComparison.OrdinalIgnoreCase) >= 0) toFix.Add(ff);
        }
        foreach (var ff in toFix) {
          var newF = new FormulaFieldClass();
          newF.Name = ff.Name;
          newF.Text = "'[QA:" + ff.Name + "]'";
          rcd.DataDefController.FormulaFieldController.Modify(ff, newF);
          Lg("formula " + ff.Name + " neutralizada p/ QA visual");
        }
        if (toFix.Count == 0) Lg("nenhuma formula com TextOfChapter encontrada");
      } catch (Exception e) { Lg("neutralizar TextOfChapter ERR: " + e.Message); }

      eng.RecordSelectionFormula = "{GACCENTRY.TYP_0} = '" + typDoc + "' and {GACCENTRY.NUM_0} = '" + numDoc + "'";
      Lg("record selection simplificado");

      Action<string,object> setP = (n,v) => { try { eng.SetParameterValue(n, v); Lg("param " + n + " OK"); } catch (Exception e) { Lg("param " + n + " ERR: " + e.Message); } };
      setP("X3ETA", "TEBX3"); setP("X3DOS", "TEBX3;teb-sagesql;X3"); setP("X3OPE", "TESTE");
      setP("X3TIT", "Extrato de Peca Contabilistica"); setP("X3EDT", "1"); setP("X3CLI", "TEBX3");
      setP("societe", "TEB"); setP("sitedeb", ""); setP("sitefin", "ZZZZZZ");
      setP("datedeb", new DateTime(2000,1,1)); setP("datefin", new DateTime(2099,12,31));
      setP("nbaxe", 5.0); setP("detana", true); setP("saut", false);
      setP("pcedeb", numDoc); setP("pcefin", numDoc);
      setP("X3FCT", "GESATX"); setP("X3PRF", "ADMIN"); setP("X3USR", "ADMIN"); setP("X3SIT", "TEBX3");
      setP("X3LAN", "POR"); setP("touref", true); setP("referentiel", 1.0);
      setP("impselections", 1.0); setP("numedt", 1.0);
      setP("typpcedeb", typDoc); setP("typpcefin", typDoc);

      eng.ExportToDisk(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat, pdfPath);
      Lg("exportado -> " + pdfPath);
      eng.Close();
    } catch (Exception ex) {
      Lg("FATAL: " + ex.Message);
      Lg(ex.StackTrace);
      if (ex.InnerException != null) Lg("INNER: " + ex.InnerException.Message);
    }
    return log.ToString();
  }
}
