// X3RptSelfJoinCum -- fix SEM subreport, SEM parametro novo: self-join nativo da tabela PAYMENTD
// (adicionada uma 2a vez como PAYMENTD_CUM) ligada por EQUAL (VCRNUM_0+DUDNUM_0) via TableLink, e
// por <= (NUM_0) via RecordSelectionFormula (nao via TableLink, para evitar a incerteza de 2 links
// nativos entre o mesmo par de tabelas com JoinTypes diferentes -- 1 EqualJoin + 1 condicao extra
// na formula de selecao e o padrao mais convencional e testado do Crystal). Sum({PAYMENTD_CUM.
// AMTLIN_0}, {PAYMENTD.VCRNUM_0}) da o acumulado historico ate e incluindo o recibo atual.
//
// v2 (CORRECAO CRITICA): a v1 usava OLEDB (crdb_ado.dll) apontando DIRETAMENTE ao servidor de
// teste (192.168.1.204\SQLDEV) -- isto FALHOU em producao real (Job 40) com "Connection error:
// Table:PAYMENTD_CUM - Location teb.TEB.PAYMENTD ... Falha ao abrir a conexao" porque o motor de
// impressao X3 SO remapeia tabelas nativas ligadas via ODBC (crdb_odbc.dll), exatamente como TODAS
// as outras tabelas nativas deste relatorio (PAYMENTD original, GACCENTRY, GACCDUDATE -- todas
// Database DLL=crdb_odbc.dll, QE_DatabaseType=ODBC (RDO)). Uma tabela ligada via OLEDB/ADO fica
// LITERALMENTE presa ao servidor de build (sem remapear), daí a tentativa de ligacao ao servidor
// de dev a partir do print server de producao. Fix: usar SEMPRE ODBC (crdb_odbc.dll) via um DSN
// alcancavel do dev (TEST_TEB211) -- o NOME do DSN e IRRELEVANTE em producao (confirmado: GACCENTRY/
// GACCDUDATE usam um DSN "X3TEB" que nem existe nesta maquina, e mesmo assim funcionam), so importa
// ser ODBC.
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptSelfJoinCum {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  public static string Build(string rptPath, string outPath,
                              string workDsn, string workDb, string workUser, string workPass) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded " + rptPath);

      // 1) tabela nativa PAYMENTD, 2a vez, alias PAYMENTD_CUM, via ODBC (crdb_odbc.dll) -- MESMO
      //    padrao/driver de TODAS as outras tabelas nativas deste relatorio (PAYMENTD original,
      //    GACCENTRY, GACCDUDATE), so o DSN de build difere (irrelevante em producao).
      var logon = new PropertyBagClass();
      logon.Add("DSN", workDsn); logon.Add("Database", workDb); logon.Add("UseDSNProperties", "False");
      logon.Add("UID", workUser); logon.Add("PWD", workPass);
      var attr = new PropertyBagClass();
      attr.Add("Database DLL", "crdb_odbc.dll"); attr.Add("QE_DatabaseName", workDb);
      attr.Add("QE_DatabaseType", "ODBC (RDO)"); attr.Add("QE_ServerDescription", workDsn);
      attr.Add("QE_SQLDB", "True"); attr.Add("SSO Enabled", "False"); attr.Add("QE_LogonProperties", logon);
      var ci = new ConnectionInfoClass();
      ci.Attributes = attr; ci.UserName = workUser; ci.Password = workPass;
      ci.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;

      var nt = new TableClass();
      nt.Name = "PAYMENTD"; nt.Alias = "PAYMENTD_CUM"; nt.QualifiedName = "TEB.PAYMENTD";
      nt.ConnectionInfo = ci;
      rcd.DatabaseController.AddTable(nt, null);
      Lg("AddTable(PAYMENTD_CUM via ODBC " + workDsn + "/" + workDb + ") OK");

      // 2) link EQUAL (VCRNUM_0+DUDNUM_0) entre PAYMENTD_CUM e PAYMENTD
      var srcNames = new StringsClass(); srcNames.Add("VCRNUM_0"); srcNames.Add("DUDNUM_0");
      var tgtNames = new StringsClass(); tgtNames.Add("VCRNUM_0"); tgtNames.Add("DUDNUM_0");
      var lk = new TableLinkClass();
      lk.SourceTableAlias = "PAYMENTD_CUM"; lk.TargetTableAlias = "PAYMENTD";
      lk.SourceFieldNames = srcNames; lk.TargetFieldNames = tgtNames;
      lk.JoinType = CrTableJoinTypeEnum.crTableJoinTypeLeftOuterJoin;
      rcd.DatabaseController.AddTableLink(lk);
      Lg("AddTableLink (PAYMENTD_CUM <-> PAYMENTD, Equal VCRNUM_0+DUDNUM_0, LeftOuter) OK");

      // 3) condicao <= via RecordSelectionFormula (append ao texto existente)
      string existingFilter = rcd.DataDefController.DataDefinition.RecordFilter.FreeEditingText;
      Lg("RecordFilter (antes, len=" + (existingFilter != null ? existingFilter.Length : -1) + ")");
      string newFilter = (existingFilter ?? "").TrimEnd() +
        "\r\nand ({PAYMENTD_CUM.NUM_0} <= {PAYMENTD.NUM_0} or IsNull({PAYMENTD_CUM.NUM_0}))";
      rcd.DataDefController.RecordFilterController.SetFormulaText(newFilter);
      Lg("RecordFilter (depois, len=" + newFilter.Length + ")");

      // 4) formula do acumulado historico (so criar se ainda nao existir -- reentrancia)
      bool hasFormula = false;
      foreach (ISCRFormulaField ffx in rcd.DataDefController.DataDefinition.FormulaFields) if (ffx.Name == "valorLiqHistJoin") hasFormula = true;
      if (!hasFormula) {
        rcd.DataDefController.FormulaFieldController.AddByName(
          "valorLiqHistJoin",
          "if ({GACCDUDATE.SNS_0} = 1 or {GACCDUDATE.SNS_0} = -1) then\r\n" +
          "Sum({PAYMENTD_CUM.AMTLIN_0}, {PAYMENTD.VCRNUM_0}) * {GACCDUDATE.SNS_0}\r\n" +
          "else\r\n" +
          "Sum({PAYMENTD_CUM.AMTLIN_0}, {PAYMENTD.VCRNUM_0})",
          CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
        Lg("formula valorLiqHistJoin criada");
      } else { Lg("formula valorLiqHistJoin ja existia (reentrancia) -- nao recriada"); }

      ISCRFormulaField pendTarget = null;
      foreach (ISCRFormulaField ff in rcd.DataDefController.DataDefinition.FormulaFields) {
        if (ff.Name == "valorpend") { pendTarget = ff; break; }
      }
      if (pendTarget == null) throw new Exception("formula valorpend nao encontrada");
      if (pendTarget.Text.IndexOf("valorLiqHistJoin") < 0) {
        var newPend = new FormulaFieldClass();
        newPend.Name = pendTarget.Name;
        newPend.Text = "{@valorDoc}-{@valorLiqHistJoin}";
        rcd.DataDefController.FormulaFieldController.Modify(pendTarget, newPend);
        Lg("valorpend apontado para valorLiqHistJoin");
      } else { Lg("valorpend ja apontava para valorLiqHistJoin"); }

      // 5) limpar credenciais
      var cleanLogon = new PropertyBagClass();
      cleanLogon.Add("DSN", workDsn); cleanLogon.Add("Database", workDb); cleanLogon.Add("UseDSNProperties", "False");
      var cleanAttr = new PropertyBagClass();
      cleanAttr.Add("Database DLL", "crdb_odbc.dll"); cleanAttr.Add("QE_DatabaseName", workDb);
      cleanAttr.Add("QE_DatabaseType", "ODBC (RDO)"); cleanAttr.Add("QE_ServerDescription", workDsn);
      cleanAttr.Add("QE_SQLDB", "True"); cleanAttr.Add("SSO Enabled", "False"); cleanAttr.Add("QE_LogonProperties", cleanLogon);
      var cleanCi = new ConnectionInfoClass();
      cleanCi.Attributes = cleanAttr; cleanCi.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
      rcd.DatabaseController.ModifyTableConnectionInfo("PAYMENTD_CUM", cleanCi);
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
