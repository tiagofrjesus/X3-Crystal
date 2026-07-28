// X3RptFixLogoHdrTable — CAUSA RAIZ REAL do bug de produção (imagem empilhada/repetida dezenas de
// vezes, caixa a ocupar a pagina inteira): logoHdr2/logoHdr3 (criados por
// tools/crystal-gen/X3RptBuildLogoSub.cs) usam uma tabela ABLOB como CommandTableClass com SQL
// SEM WHERE ("SELECT ... FROM TEB.ABLOB"), violando a licao "Tabelas novas — SEMPRE nativas, nunca
// Command" (LESSONS.md). Isto faz o Command devolver a tabela ABLOB INTEIRA (todas as
// imagens/selos de todas as empresas) e a secao Detail do subreport repete uma vez por linha
// devolvida no motor de impressao real. logo1/logo2/logo3 (originais Sage, sempre funcionaram)
// usam uma tabela NATIVA genuina (ClassName=CrystalReports.Table) — confirmado por diagnostico
// read-only nesta sessao.
//
// FIX: substituir a CommandTable de logoHdr2/logoHdr3 por uma tabela NATIVA nova, construida com o
// MESMO padrao documentado em LESSONS.md ("Tabelas novas — SEMPRE nativas, nunca Command") para o
// relatorio principal: DSN reachable do dev (TEST_TEB211) so para passar a validacao de
// conectividade do SetTableLocation/AddTable, QualifiedName "TEB.ABLOB" (mesmo padrao das outras
// tabelas nativas novas deste relatorio, ex. BPADDRESS_FCY/BPARTNER_HDR) — o motor de impressao X3
// remapeia tabelas NATIVAS para o DSN do folder em runtime independentemente do DSN de build, por
// isso o DSN exato usado aqui e' irrelevante em produção, so importa ser um TableClass genuino
// (nao Command). Depois de SetTableLocation, credenciais sao limpas (ModifyTableConnectionInfo)
// antes do SaveAs final. NAO tocar em RecordFilter/SubreportLinks — ja confirmados corretos e
// identicos aos originais (logo2/logo3) por diagnostico nesta sessao.
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptFixLogoHdrTable {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  static TableClass BuildNativeAblob(string workDsn, string workDb, string workUser, string workPass, bool withCreds) {
    var logon = new PropertyBagClass();
    logon.Add("DSN", workDsn); logon.Add("Database", workDb); logon.Add("UseDSNProperties", "False");
    if (withCreds) { logon.Add("UID", workUser); logon.Add("PWD", workPass); }
    var attr = new PropertyBagClass();
    attr.Add("Database DLL", "crdb_odbc.dll");
    attr.Add("QE_DatabaseName", workDb);
    attr.Add("QE_DatabaseType", "ODBC (RDO)");
    attr.Add("QE_ServerDescription", workDsn);
    attr.Add("QE_SQLDB", "True");
    attr.Add("SSO Enabled", "False");
    attr.Add("QE_LogonProperties", logon);
    var ci = new ConnectionInfoClass();
    ci.Attributes = attr;
    ci.UserName = withCreds ? workUser : "";
    ci.Password = withCreds ? workPass : "";
    ci.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
    var nt = new TableClass();
    nt.Name = "ABLOB";
    nt.Alias = "ABLOB";
    nt.QualifiedName = "TEB.ABLOB";
    nt.ConnectionInfo = ci;
    return nt;
  }

  static void FixOne(CrystalDecisions.ReportAppServer.ClientDoc.ISCDReportClientDocument rcd, string subName,
                      string workDsn, string workDb, string workUser, string workPass) {
    var subDoc = rcd.SubreportController.GetSubreport(subName);
    ISCRTable oldTbl = subDoc.DatabaseController.Database.Tables[0];
    Lg(subName + " tabela ANTES: ClassName=" + oldTbl.ClassName + " Qualified=" + oldTbl.QualifiedName);

    var newTblWithCreds = BuildNativeAblob(workDsn, workDb, workUser, workPass, true);
    rcd.SubreportController.SetTableLocation(subName, oldTbl, newTblWithCreds);
    Lg(subName + " SetTableLocation -> tabela nativa (com credenciais) OK");

    // limpar credenciais antes de gravar (padrao "Tabelas novas" do LESSONS.md)
    var subDoc2 = rcd.SubreportController.GetSubreport(subName);
    ISCRTable liveTbl = subDoc2.DatabaseController.Database.Tables[0];
    var cleanCi = new ConnectionInfoClass();
    var cleanLogon = new PropertyBagClass();
    cleanLogon.Add("DSN", workDsn); cleanLogon.Add("Database", workDb); cleanLogon.Add("UseDSNProperties", "False");
    var cleanAttr = new PropertyBagClass();
    cleanAttr.Add("Database DLL", "crdb_odbc.dll");
    cleanAttr.Add("QE_DatabaseName", workDb);
    cleanAttr.Add("QE_DatabaseType", "ODBC (RDO)");
    cleanAttr.Add("QE_ServerDescription", workDsn);
    cleanAttr.Add("QE_SQLDB", "True");
    cleanAttr.Add("SSO Enabled", "False");
    cleanAttr.Add("QE_LogonProperties", cleanLogon);
    cleanCi.Attributes = cleanAttr;
    cleanCi.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
    try {
      subDoc2.DatabaseController.ModifyTableConnectionInfo("ABLOB", cleanCi);
      Lg(subName + " credenciais limpas via ModifyTableConnectionInfo");
    } catch (Exception ex) {
      Lg(subName + " ModifyTableConnectionInfo FAIL: " + ex.Message + " -- tentando SetTableLocation com tabela sem creds");
      var subDoc3 = rcd.SubreportController.GetSubreport(subName);
      ISCRTable liveTbl2 = subDoc3.DatabaseController.Database.Tables[0];
      var newTblNoCreds = BuildNativeAblob(workDsn, workDb, workUser, workPass, false);
      rcd.SubreportController.SetTableLocation(subName, liveTbl2, newTblNoCreds);
      Lg(subName + " SetTableLocation (sem credenciais) OK");
    }
  }

  public static string Build(string rptPath, string outPath, string workDsn, string workDb, string workUser, string workPass) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded");

      FixOne(rcd, "logoHdr2", workDsn, workDb, workUser, workPass);
      FixOne(rcd, "logoHdr3", workDsn, workDb, workUser, workPass);

      // Confirmar depois do SetTableLocation (ainda na sessao viva, antes do SaveAs)
      foreach (var nm in new[] { "logoHdr2", "logoHdr3" }) {
        var d = rcd.SubreportController.GetSubreport(nm);
        ISCRTable t = d.DatabaseController.Database.Tables[0];
        Lg(nm + " tabela DEPOIS: ClassName=" + t.ClassName + " Qualified=" + t.QualifiedName + " User=" + t.ConnectionInfo.UserName);
        string filt = d.DataDefController.DataDefinition.RecordFilter.FreeEditingText;
        Lg(nm + " RecordFilter DEPOIS (len=" + (filt != null ? filt.Length : -1) + ")");
      }

      string dir = System.IO.Path.GetDirectoryName(outPath); string nm2 = System.IO.Path.GetFileName(outPath); object od = dir;
      rcd.SaveAs(nm2, ref od, 0);
      Lg("saved -> " + outPath);
      eng.Close();
    } catch (Exception ex) {
      Lg("FATAL: " + ex.Message);
      Lg(ex.StackTrace);
    }
    return log.ToString();
  }
}
