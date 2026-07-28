// X3RptFixValorPagoLinha -- corrige o bug de dados confirmado em producao real (Job 33, recibo
// RPMB-26E01/01682): a coluna "Valor Pago" da linha de detalhe (campo valorLiq1, DataSource=
// {@valorLiq}) estava a mostrar o ACUMULADO de TODOS os recibos contra a mesma fatura ate
// HOJE (GACCDUDATE.PAYCUR_0 = 0.25, soma de RPMB-26E01/01682=0.10 + RPMB-26E01/01683=0.15),
// em vez do valor que ESTE recibo especificamente pagou a este documento (0.10). O rodape
// ("Total liquidado"/"Valor recebido", formula total_liquidado = {PAYMENTH.AMTBAN_0}+...) usa o
// header do recibo diretamente e SEMPRE esteve correto (nao mexido por este fix).
//
// Causa raiz: a formula "valorLiq" e usada para DOIS fins diferentes que precisam de valores
// DIFERENTES:
//   1) o campo VISIVEL "Valor Pago" da linha -- deve ser o valor pago por ESTE recibo a ESTE
//      documento (granularidade de linha) = {PAYMENTD.AMTLIN_0}.
//   2) o calculo de "valorpend" (valorDoc - valorLiq) -- para reflectir corretamente faturas
//      pagas em varias prestacoes, precisa do ACUMULADO (GACCDUDATE.PAYCUR_0, "a data de hoje").
// A sessao anterior tinha os dois a usar a MESMA formula "valorLiq", quebrando o (1).
//
// Fix: criar formula NOVA "valorLiqLinha" (= so o AMTLIN_0 deste recibo/linha, mesmo padrao de
// sinal ja usado em valorDoc/valorLiq) e apontar o campo visivel valorLiq1 para ela. A formula
// "valorLiq" original (PAYCUR_0-based) mantem-se inalterada, usada so por "valorpend".
using System;
using System.Text;
using System.Collections.Generic;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptFixValorPagoLinha {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  static IEnumerable<ISCRArea> AllAreas(ReportDefinition rd) {
    var l = new List<ISCRArea>();
    l.Add(rd.ReportHeaderArea); l.Add(rd.PageHeaderArea); l.Add(rd.DetailArea);
    l.Add(rd.ReportFooterArea); l.Add(rd.PageFooterArea);
    for (int i = 0; i < 8; i++) {
      try { var a = rd.GroupHeaderArea[i]; if (a != null) l.Add(a); } catch {}
      try { var a = rd.GroupFooterArea[i]; if (a != null) l.Add(a); } catch {}
    }
    return l;
  }
  static ISCRReportObject FindByName(ReportDefinition rd, string name) {
    foreach (var area in AllAreas(rd)) {
      if (area == null) continue;
      foreach (Section s in area.Sections) foreach (ISCRReportObject ro in s.ReportObjects) if (ro.Name == name) return ro;
    }
    return null;
  }

  public static string Build(string rptPath, string outPath) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded " + rptPath);

      // 1) formula nova, so com o valor deste recibo/linha (mesmo padrao de sinal de valorLiq/valorDoc)
      rcd.DataDefController.FormulaFieldController.AddByName(
        "valorLiqLinha",
        "if ({GACCDUDATE.SNS_0} = 1 or {GACCDUDATE.SNS_0} = -1) then\r\n" +
        "{PAYMENTD.AMTLIN_0} * {GACCDUDATE.SNS_0}\r\n" +
        "else\r\n" +
        "{PAYMENTD.AMTLIN_0}",
        CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      Lg("formula valorLiqLinha criada (PAYMENTD.AMTLIN_0 desta linha/recibo, nao acumulado)");

      // 2) repontar o campo visivel valorLiq1 para a formula nova. Confirmado nesta sessao:
      //    Modify() REJEITA mudar o DataSource de um FieldObject ("Impossivel alterar a origem de
      //    dados de objetos de campo") -- limitacao de TIPO adicional ao ja documentado em
      //    LESSONS.md (Modify() nao suporta tudo para todos os tipos). Contorno: Remove() do
      //    campo antigo + Add() de um FieldObjectClass NOVO na MESMA posicao/tamanho com a
      //    formula nova (Add() de FieldObjectClass funciona sempre, ver LESSONS.md).
      var rd = rcd.ReportDefinition;
      var fld = FindByName(rd, "valorLiq1");
      if (fld == null) throw new Exception("campo valorLiq1 nao encontrado");
      var oldFo = (FieldObject)fld;
      Lg("valorLiq1 (antes) DataSource=" + oldFo.DataSource + " L=" + oldFo.Left + " T=" + oldFo.Top + " W=" + oldFo.Width + " H=" + oldFo.Height);
      int l = oldFo.Left, t = oldFo.Top, w = oldFo.Width, h = oldFo.Height;
      Section ownerSec = null;
      foreach (var area in AllAreas(rd)) {
        if (area == null) continue;
        foreach (Section s in area.Sections) foreach (ISCRReportObject ro in s.ReportObjects) if (ro.Name == "valorLiq1") { ownerSec = s; }
      }
      if (ownerSec == null) throw new Exception("seccao de valorLiq1 nao encontrada");
      rcd.ReportDefController.ReportObjectController.Remove(fld);
      Lg("valorLiq1 (antigo, DataSource={@valorLiq}) removido");
      var newFld = new FieldObjectClass();
      newFld.DataSource = "{@valorLiqLinha}";
      newFld.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeNumberField;
      newFld.Kind = CrReportObjectKindEnum.crReportObjectKindField;
      newFld.Left = l; newFld.Top = t; newFld.Width = w; newFld.Height = h;
      newFld.Name = "valorLiqLinha1";
      rcd.ReportDefController.ReportObjectController.Add(newFld, ownerSec, -1);
      Lg("valorLiqLinha1 (novo, DataSource={@valorLiqLinha}) adicionado na mesma posicao L=" + l + " T=" + t + " W=" + w + " H=" + h);

      // 3) "valorLiq" (PAYCUR_0-based) e "valorpend" (valorDoc-valorLiq) NAO SAO TOCADOS -- ficam
      //    a usar o acumulado "a data de hoje" so para o calculo de valorpend, como antes.

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
