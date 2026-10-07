// X3RptSqlDiag -- diagnostico de "Erro no arquivo ..." sem detalhe: remapeia as tabelas como o
// X3RptExportFull e devolve o SQL gerado pelo Crystal (principal + cada subreport) para correr a parte.
using System;
using System.Text;
using CrystalDecisions.Shared;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.Controllers;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptSqlDiag {
  static void Remap(Eng.Tables tables, string dsn, string db, string schema, string user, string pass) {
    foreach (Eng.Table t in tables) {
      var li = t.LogOnInfo;
      li.ConnectionInfo.ServerName = dsn; li.ConnectionInfo.DatabaseName = db;
      li.ConnectionInfo.UserID = user; li.ConnectionInfo.Password = pass;
      t.ApplyLogOnInfo(li);
      string loc = t.Location; t.Location = schema + "." + loc.Substring(loc.LastIndexOf('.') + 1);
    }
  }
  public static string Run(string rpt, string dsn, string db, string schema, string user, string pass) {
    var sb = new StringBuilder();
    var eng = new Eng.ReportDocument();
    try {
      eng.Load(rpt);
      Remap(eng.Database.Tables, dsn, db, schema, user, pass);
      foreach (Eng.ReportDocument s in eng.Subreports) Remap(s.Database.Tables, dsn, db, schema, user, pass);
      string sql;
      try { eng.ReportClientDocument.RowsetController.GetSQLStatement(new GroupPathClass(), out sql); sb.Append("### MAIN\r\n").Append(sql).Append("\r\n"); }
      catch (Exception ex) { sb.Append("### MAIN ERR ").Append(ex.Message).Append("\r\n"); }
      foreach (string sn in eng.ReportClientDocument.SubreportController.GetSubreportNames()) {
        try {
          var sub = eng.ReportClientDocument.SubreportController.GetSubreport(sn);
          sub.RowsetController.GetSQLStatement(new GroupPathClass(), out sql);
          sb.Append("### SUB ").Append(sn).Append("\r\n").Append(sql).Append("\r\n");
        } catch (Exception ex) { sb.Append("### SUB ").Append(sn).Append(" ERR ").Append(ex.Message).Append("\r\n"); }
      }
    } catch (Exception ex) { sb.Append("FATAL ").Append(ex.Message); }
    finally { eng.Close(); }
    return sb.ToString();
  }
}
