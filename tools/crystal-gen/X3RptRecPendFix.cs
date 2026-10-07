// X3RptRecPendFix -- TEB_REC: "Valor Pendente" a data do recibo SEM Command/SQL, SEM subreport, SEM
// parametro novo. Evolucao do X3RptSelfJoinCum (2026-07):
//  1) pago a data do recibo = GACCDUDATE.PAYCUR_0 (hoje) - linhas PAYMENTD de recibos criados DEPOIS
//     (CREDATTIM_0). Somar os recibos anteriores NAO serve: as conciliacoes parciais redistribuem o
//     pendente entre faturas sem linhas PAYMENTD (caso RTRF-26E01/03869: 013077 dava -653,83 e a
//     013107 ficava com 653,83 pendente quando o X3 a tem paga). Comparar NUM_0 como texto tambem nao
//     serve ("RPMB-..." vs "RTRF-..." nao tem ordem cronologica).
//  2) o self-join multiplica as linhas do relatorio (cada linha PAYMENTD x N linhas PAYMENTD_CUM).
//     @selfRow = 1 so na combinacao linha<->ela propria (existe sempre exatamente uma), e todas as
//     agregacoes sobre linhas do recibo (Valor Pago, descontos, retencoes, total) passam a contar so
//     essa; o acumulado historico e Sum(CUM)/Sum(selfRow) (recibo com RECEB+DFINA p/ a mesma fatura
//     teria o historico contado 2x).
// Tabela PAYMENTD_CUM: nativa (TableClass), ODBC crdb_odbc.dll -- igual as restantes do relatorio.
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptRecPendFix {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append("\r\n"); }

  static ISCRFormulaField Find(ISCDReportClientDocument rcd, string name) {
    foreach (ISCRFormulaField ff in rcd.DataDefController.DataDefinition.FormulaFields) if (ff.Name == name) return ff;
    return null;
  }
  static void Upsert(ISCDReportClientDocument rcd, string name, string text) {
    var cur = Find(rcd, name);
    if (cur == null) {
      rcd.DataDefController.FormulaFieldController.AddByName(name, text, CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      Lg("formula criada: " + name);
    } else {
      var nf = new FormulaFieldClass(); nf.Name = name; nf.Text = text;
      rcd.DataDefController.FormulaFieldController.Modify(cur, nf);
      Lg("formula alterada: " + name);
    }
  }
  static ConnectionInfoClass Ci(string dsn, string db, string user, string pass, bool withCreds) {
    var logon = new PropertyBagClass();
    logon.Add("DSN", dsn); logon.Add("Database", db); logon.Add("UseDSNProperties", "False");
    if (withCreds) { logon.Add("UID", user); logon.Add("PWD", pass); }
    var attr = new PropertyBagClass();
    attr.Add("Database DLL", "crdb_odbc.dll"); attr.Add("QE_DatabaseName", db);
    attr.Add("QE_DatabaseType", "ODBC (RDO)"); attr.Add("QE_ServerDescription", dsn);
    attr.Add("QE_SQLDB", "True"); attr.Add("SSO Enabled", "False"); attr.Add("QE_LogonProperties", logon);
    var ci = new ConnectionInfoClass();
    ci.Attributes = attr; ci.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
    if (withCreds) { ci.UserName = user; ci.Password = pass; }
    return ci;
  }

  public static string Build(string rptPath, string outPath, string dsn, string db, string user, string pass) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded " + rptPath);

      bool hasCum = false;
      foreach (ISCRTable t in rcd.DatabaseController.Database.Tables) if (t.Alias == "PAYMENTD_CUM") hasCum = true;
      if (hasCum) throw new Exception("PAYMENTD_CUM ja existe -- partir do ficheiro sem o fix");

      // 1) tabela nativa PAYMENTD_CUM (ODBC)
      var nt = new TableClass();
      nt.Name = "PAYMENTD"; nt.Alias = "PAYMENTD_CUM"; nt.QualifiedName = "TEB.PAYMENTD";
      nt.ConnectionInfo = Ci(dsn, db, user, pass, true);
      rcd.DatabaseController.AddTable(nt, null);
      Lg("AddTable PAYMENTD_CUM OK");

      // 2) link nativo VCRNUM_0+DUDNUM_0 (LeftOuter)
      var s = new StringsClass(); s.Add("VCRNUM_0"); s.Add("DUDNUM_0");
      var d = new StringsClass(); d.Add("VCRNUM_0"); d.Add("DUDNUM_0");
      var lk = new TableLinkClass();
      lk.SourceTableAlias = "PAYMENTD"; lk.TargetTableAlias = "PAYMENTD_CUM";
      lk.SourceFieldNames = s; lk.TargetFieldNames = d;
      lk.JoinType = CrTableJoinTypeEnum.crTableJoinTypeLeftOuterJoin;
      rcd.DatabaseController.AddTableLink(lk);
      Lg("AddTableLink PAYMENTD -> PAYMENTD_CUM OK");

      // 3) record selection: so linhas do proprio recibo ou de recibos criados DEPOIS (na impressao
      //    normal nao ha nenhum -> o join nao acrescenta linhas)
      string flt = rcd.DataDefController.DataDefinition.RecordFilter.FreeEditingText ?? "";
      flt = flt.TrimEnd() + "\r\nand (IsNull({PAYMENTD_CUM.NUM_0}) or {PAYMENTD_CUM.NUM_0} = {PAYMENTD.NUM_0} or {PAYMENTD_CUM.CREDATTIM_0} > {PAYMENTD.CREDATTIM_0})";
      rcd.DataDefController.RecordFilterController.SetFormulaText(flt);
      Lg("RecordFilter atualizado");

      // 4) formulas
      Upsert(rcd, "selfRow",
        "if IsNull({PAYMENTD_CUM.NUM_0}) or ({PAYMENTD_CUM.NUM_0} = {PAYMENTD.NUM_0} and {PAYMENTD_CUM.LIN_0} = {PAYMENTD.LIN_0}) then 1 else 0");
      Upsert(rcd, "amtLinSelf", "if {@selfRow} = 1 then {PAYMENTD.AMTLIN_0} else 0");
      Upsert(rcd, "amtLinLater",
        "if IsNull({PAYMENTD_CUM.NUM_0}) or {PAYMENTD_CUM.NUM_0} = {PAYMENTD.NUM_0} then 0 else {PAYMENTD_CUM.AMTLIN_0}");
      Upsert(rcd, "valorLiq",
        "if ({GACCDUDATE.SNS_0} = 1 or {GACCDUDATE.SNS_0} = -1) then\r\n" +
        "(Sum ({@amtLinSelf}, {PAYMENTD.VCRNUM_0})) * {GACCDUDATE.SNS_0}\r\n" +
        "else \r\n" +
        "Sum ({@amtLinSelf}, {PAYMENTD.VCRNUM_0})");
      Upsert(rcd, "valorLiqHistJoin",
        "local numbervar n := Sum ({@selfRow}, {PAYMENTD.VCRNUM_0});\r\n" +
        "local numbervar later := 0;\r\n" +
        "if n > 0 then later := Sum ({@amtLinLater}, {PAYMENTD.VCRNUM_0}) / n;\r\n" +
        "// pago a data deste recibo = pago hoje (inclui redistribuicoes de conciliacao) - recibos posteriores\r\n" +
        "local numbervar h := {GACCDUDATE.PAYCUR_0} - later;\r\n" +
        "if h < 0 then h := 0;\r\n" +
        "if ({GACCDUDATE.SNS_0} = 1 or {GACCDUDATE.SNS_0} = -1) then h * {GACCDUDATE.SNS_0} else h");
      Upsert(rcd, "valorpend", "{@valorDoc}-{@valorLiqHistJoin}");
      Upsert(rcd, "descontos",
        "if {AREPORTM_LIN.CLEN2_0}=3 and {@selfRow} = 1 then\r\n" +
        "{PAYMENTD.AMTLIN_0}-{PAYMENTD.AMTLIN2_0}");
      Upsert(rcd, "withholdingTax",
        "WhilePrintingRecords;\r\n\r\nShared NumberVar withholdingTax;\r\n\r\n" +
        "If ({PAYMENTD.ACC_0} like \"242*\") and {@selfRow} = 1 then \r\n" +
        "    withholdingTax := withholdingTax + {PAYMENTD.AMTLIN_0};\r\n\r\nabs(withholdingTax);");
      Upsert(rcd, "total",
        "shared numbervar total;\r\nshared numbervar total2;\r\n" +
        "if {@selfRow} = 1 then total:= total+{PAYMENTD.AMTLIN2_0};\r\ntotal;");

      // 5) limpar credenciais da tabela nova
      rcd.DatabaseController.ModifyTableConnectionInfo("PAYMENTD_CUM", Ci(dsn, db, user, pass, false));
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
