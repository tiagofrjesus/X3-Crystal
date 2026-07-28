// X3RptFixRecPendente — corrige a formula "valorLiq" do TEB_REC.rpt (recibo de pagamentos).
//
// Bug: valorLiq somava Sum({PAYMENTD.AMTLIN_0}, {PAYMENTD.VCRNUM_0}). O dataset do report
// (AREPORTM -> PAYMENTH -> PAYMENTD, filtrado por CLEA1_0 = numero do recibo atual) so contem
// as linhas PAYMENTD do recibo que esta a ser impresso, mesmo o VCRNUM_0 sendo o identificador
// da fatura (open item), nao do recibo. Por isso o Sum nunca via as linhas de recibos anteriores
// contra a mesma fatura: em cada recibo, valorLiq = so o valor pago NESSE recibo, nunca o
// acumulado. valorpend = valorDoc - valorLiq ficava sempre igual ao 1o pagamento parcial.
//
// Fix: usar GACCDUDATE.PAYCUR_0 (valor pago acumulado do open item, mantido pelo proprio X3 a
// cada reconciliacao) em vez de recalcular a partir de PAYMENTD.
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptFixRecPendente {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  public static string Build(string rptPath, string outPath) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded " + rptPath);

      ISCRFormulaField target = null;
      foreach (ISCRFormulaField ff in rcd.DataDefController.DataDefinition.FormulaFields) {
        if (ff.Name == "valorLiq") { target = ff; break; }
      }
      if (target == null) throw new Exception("formula valorLiq nao encontrada");
      Lg("valorLiq (antes) = " + target.Text.Replace("\r\n", " \\n "));

      var newF = new FormulaFieldClass();
      newF.Name = target.Name;
      newF.Text =
        "if ({GACCDUDATE.SNS_0} = 1 or {GACCDUDATE.SNS_0} = -1) then\r\n" +
        "{GACCDUDATE.PAYCUR_0} * {GACCDUDATE.SNS_0}\r\n" +
        "else\r\n" +
        "{GACCDUDATE.PAYCUR_0}";
      rcd.DataDefController.FormulaFieldController.Modify(target, newF);
      Lg("valorLiq (depois) = " + newF.Text.Replace("\r\n", " \\n "));

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
