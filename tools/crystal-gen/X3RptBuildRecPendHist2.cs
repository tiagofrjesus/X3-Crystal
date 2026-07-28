// X3RptBuildRecPendHist2 — v2: reconstroi o RecPendHist.rpt usando uma tabela NATIVA (TableClass),
// nao uma CommandTableClass. Licao critica ja documentada em LESSONS.md ("Tabelas novas — SEMPRE
// nativas, nunca Command"): o motor de impressao do X3 so REMAPEIA para o DSN real do folder, em
// runtime, tabelas NATIVAS — uma CommandTableClass fica colada ao DSN de build e falha ("Falha de
// logon"/sem dados) no print server de producao. A v1 (X3RptBuildRecPendHist.cs) usava Command,
// replicando por engano o padrao de X3RptBuildLogoSub.cs (que e so para *reaproveitar* a ligacao
// de uma tabela ja existente via CommandTable — cenario diferente de "tabela nova").
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptBuildRecPendHist2 {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  public static string Build(string seedPath, string outPath,
                              string workDsn, string workDb, string workUser, string workPass) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(seedPath);
      var rcd = eng.ReportClientDocument;
      Lg("seed loaded");

      // 1) AddTable NATIVA via DSN ODBC alcancavel a partir desta maquina (TEST_TEB211)
      var logon = new PropertyBagClass(); logon.Add("DSN", workDsn); logon.Add("Database", workDb); logon.Add("UseDSNProperties", "False"); logon.Add("UID", workUser); logon.Add("PWD", workPass);
      var attr = new PropertyBagClass(); attr.Add("Database DLL", "crdb_odbc.dll"); attr.Add("QE_DatabaseName", workDb); attr.Add("QE_DatabaseType", "ODBC (RDO)"); attr.Add("QE_ServerDescription", workDsn); attr.Add("QE_SQLDB", "True"); attr.Add("SSO Enabled", "False"); attr.Add("QE_LogonProperties", logon);
      var ci = new ConnectionInfoClass(); ci.Attributes = attr; ci.UserName = workUser; ci.Password = workPass; ci.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;

      var nt = new TableClass();
      nt.Name = "PAYMENTD";
      nt.Alias = "PAYMENTD";
      nt.QualifiedName = "TEB.PAYMENTD";
      nt.ConnectionInfo = ci;
      rcd.DatabaseController.AddTable(nt, null);
      Lg("AddTable(native PAYMENTD via ODBC " + workDsn + ") OK");

      // 2)/3) SEM parametros nem record selection aqui de proposito. Confirmado por tentativa e
      //    erro comparando com o subreport IVAC (ja existente/funcional neste TEB_REC.rpt): os
      //    parametros "Pm-<CampoDoRelatorioPrincipal>" sao criados automaticamente pelo PROPRIO
      //    SubreportLink no momento em que e adicionado (X3RptAddRecPendHist.cs) — pre-criar aqui
      //    um parametro com esse nome de antemao causa COLISAO: o link acaba por criar um SEGUNDO
      //    parametro com sufixo "??01" (o unico que fica realmente ligado), deixando o original
      //    orfao (PromptToUser=True, nunca alimentado) -> ERR 504 "Valores de parametro ausentes"
      //    em runtime real. O RecordFilter so pode ser escrito DEPOIS de o link existir (proximo
      //    passo, ja dentro do relatorio principal), quando o parametro "Pm-..." limpo ja existe.

      var rd = rcd.ReportDefinition;
      ISCRField amtlinField = null;
      foreach (ISCRField f in nt.DataFields) if (f.Name == "AMTLIN_0") amtlinField = f;
      // fallback: reler da tabela tal como ficou no Database apos AddTable
      if (amtlinField == null) {
        foreach (ISCRTable t in rcd.Database.Tables)
          foreach (ISCRField f in t.DataFields) if (f.Name == "AMTLIN_0") amtlinField = f;
      }

      // 4a) campo de detalhe ligado a AMTLIN_0 — forca o Crystal a de facto iterar as linhas.
      //    Sem NENHUM campo do Detail vinculado a tabela, um Sum()/WhilePrintingRecords ad-hoc
      //    (tentativa anterior) pode nao percorrer registo a registo. Minusculo, nao visivel de
      //    forma pratica (o subreport inteiro fica 50x50 no relatorio principal).
      Section detSec = null;
      foreach (Section s in rd.DetailArea.Sections) { detSec = s; break; }
      var df = new FieldObjectClass();
      df.DataSource = amtlinField.FormulaForm; df.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeNumberField;
      df.Kind = CrReportObjectKindEnum.crReportObjectKindField;
      df.Left = 0; df.Top = 0; df.Width = 50; df.Height = 50; df.Name = "detAMTLIN";
      rcd.ReportDefController.ReportObjectController.Add(df, detSec, -1);
      Lg("campo de detalhe AMTLIN_0 adicionado (forca iteracao)");

      // 4b) formula que acumula o total pago (WhilePrintingRecords) numa shared var. Confirmado
      //    (por engano fértil): a sintaxe de referencia de um SummaryField grao-total do Crystal
      //    e literalmente "Sum ({Tabela.Campo})" — o MESMO que escrever Sum(...) diretamente numa
      //    formula. Nao ha diferenca de mecanismo; a mudanca real desta versao e o campo de
      //    detalhe (4a) que forca a iteracao das linhas.
      rcd.DataDefController.FormulaFieldController.AddByName(
        "setShared",
        "WhilePrintingRecords;\r\nshared numbervar gValorLiqHist;\r\ngValorLiqHist := Sum({PAYMENTD.AMTLIN_0});\r\ngValorLiqHist",
        CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      Lg("formula setShared added");

      Section rptFtrSec = null;
      foreach (Section s in rd.ReportFooterArea.Sections) { rptFtrSec = s; break; }
      var fo = new FieldObjectClass();
      fo.DataSource = "{@setShared}"; fo.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeNumberField;
      fo.Kind = CrReportObjectKindEnum.crReportObjectKindField;
      fo.Left = 100; fo.Top = 0; fo.Width = 50; fo.Height = 50; fo.Name = "setSharedFld";
      rcd.ReportDefController.ReportObjectController.Add(fo, rptFtrSec, -1);
      Lg("setShared field placed in ReportFooter");

      // 5) limpar credenciais antes de gravar (o print engine do X3 fornece-as em runtime e
      //    REMAPEIA a tabela nativa para o DSN real do folder — nao gravar a password no ficheiro)
      var cleanLogon = new PropertyBagClass(); cleanLogon.Add("DSN", workDsn); cleanLogon.Add("Database", workDb); cleanLogon.Add("UseDSNProperties", "False");
      var cleanAttr = new PropertyBagClass(); cleanAttr.Add("Database DLL", "crdb_odbc.dll"); cleanAttr.Add("QE_DatabaseName", workDb); cleanAttr.Add("QE_DatabaseType", "ODBC (RDO)"); cleanAttr.Add("QE_ServerDescription", workDsn); cleanAttr.Add("QE_SQLDB", "True"); cleanAttr.Add("SSO Enabled", "False"); cleanAttr.Add("QE_LogonProperties", cleanLogon);
      var cleanCi = new ConnectionInfoClass(); cleanCi.Attributes = cleanAttr; cleanCi.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
      rcd.DatabaseController.ModifyTableConnectionInfo("PAYMENTD", cleanCi);
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
