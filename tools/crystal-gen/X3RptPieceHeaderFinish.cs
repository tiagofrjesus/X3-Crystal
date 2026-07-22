// X3RptPieceHeaderFinish — remata o redesenho do cabeçalho do TEB_PIECE.rpt: remove o bloco de
// texto da empresa (agora redundante com o logótipo gráfico já importado) e envolve a grelha de
// metadados numa caixa com borda, como no TEB_PAG.rpt.
//
// NOTA (investigação desta sessão): ReportObjectController.Add() NÃO suporta BoxObjectClass nesta
// versão do RAS SDK — confirmado que falha SEMPRE com "Seção de relatório não localizada", mesmo
// como primeiríssima operação num seed em branco recém-carregado (ver tools/crystal-gen/BoxTest.ps1
// caso TestBlank). Não é um problema de secção stale nem de ordem de operações — é uma limitação de
// TIPO, na mesma família da limitação já documentada para Subreport/Line em LESSONS.md. O contorno
// validado é usar um FieldObjectClass com uma fórmula vazia ('') e a propriedade .Border preenchida
// — Field objects suportam Add() de forma fiável em qualquer secção/momento, e o Border desenha a
// mesma moldura retangular visualmente idêntica a uma Box.
//
// Compilar via Add-Type em PowerShell 32-BIT.
using System;
using System.Text;
using System.Collections.Generic;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptPieceHeaderFinish {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }
  static CrystalDecisions.ReportAppServer.ClientDoc.ISCDReportClientDocument RCD;

  static ISCRReportObject Find(ReportDefinition rd, string name) {
    foreach (var area in AllAreas(rd)) {
      if (area == null) continue;
      foreach (Section s in area.Sections) foreach (ISCRReportObject ro in s.ReportObjects) if (ro.Name == name) return ro;
    }
    return null;
  }
  static Section FindSection(ReportDefinition rd, string name) {
    foreach (var area in AllAreas(rd)) {
      if (area == null) continue;
      foreach (Section s in area.Sections) if (s.Name == name) return s;
    }
    return null;
  }
  static IEnumerable<ISCRArea> AllAreas(ReportDefinition rd) {
    var l = new List<ISCRArea>();
    l.Add(rd.ReportHeaderArea); l.Add(rd.PageHeaderArea); l.Add(rd.DetailArea);
    l.Add(rd.ReportFooterArea); l.Add(rd.PageFooterArea);
    for (int i = 0; i < 5; i++) {
      try { var a = rd.GroupHeaderArea[i]; if (a != null) l.Add(a); } catch {}
      try { var a = rd.GroupFooterArea[i]; if (a != null) l.Add(a); } catch {}
    }
    return l;
  }

  public static string Build(string rptPath, string outPath) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      RCD = eng.ReportClientDocument;
      Lg("loaded");

      var rd = RCD.ReportDefinition;

      // 0) reposicionar Subreport5 (selo "22 ANOS", logoHdr3) — estava empilhado por baixo do
      //    logo principal (T=900..1799), invadindo verticalmente a grelha de metadados que começa
      //    em T=980 (confirmado visualmente num PDF de QA: a moldura do subreport atravessava o
      //    texto "Doc. nr." / "Data doc."). Só há 980 twips de folga antes da grelha, insuficiente
      //    para o selo empilhado (899 de altura). Solução: colocar o selo ao lado do logo principal
      //    (Subreport4 termina em L=1605), não por baixo — mantendo o aspect ratio original
      //    (750x899 -> 700x839, mesmo rácio 0.834) e ficando dentro do espaço livre acima da
      //    grelha (T=0..839, folga de 141 antes de T=980) e antes do bloco Terceiro (L=6200).
      try {
        var sec13pre = FindSection(rd, "Section13");
        ISCRReportObject sub5 = null;
        if (sec13pre != null) foreach (ISCRReportObject ro in sec13pre.ReportObjects) if (ro.Name == "Subreport5") sub5 = ro;
        if (sub5 != null) {
          var clone = (ISCRReportObject)sub5.Clone(true);
          clone.Left = 1755; clone.Top = 0; clone.Width = 700; clone.Height = 839;
          RCD.ReportDefController.ReportObjectController.Modify(sub5, clone);
          Lg("Subreport5 (selo) reposicionado ao lado do logo, sem sobrepor a grelha");
        } else Lg("Subreport5 nao encontrado para reposicionar");
      } catch (Exception e) { Lg("reposicionar Subreport5 ERR: " + e.Message); }

      // 1) caixa com borda à volta da grelha de metadados — via FieldObject vazio + Border
      //    (BoxObjectClass.Add() não é suportado por este SDK — ver nota no topo do ficheiro).
      var sec13 = FindSection(rd, "Section13");
      if (sec13 == null) throw new Exception("Section13 nao encontrada");

      RCD.DataDefController.FormulaFieldController.AddByName("fHdrMetaBox", "''", CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
      var boxFld = new FieldObjectClass();
      boxFld.DataSource = "{@fHdrMetaBox}";
      boxFld.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeStringField;
      boxFld.Kind = CrReportObjectKindEnum.crReportObjectKindField;
      boxFld.Left = 35; boxFld.Top = 955; boxFld.Width = 11750; boxFld.Height = 495;
      boxFld.Name = "hdrMetaBox";
      var brd = new BorderClass();
      brd.LeftLineStyle = CrLineStyleEnum.crLineStyleSingle;
      brd.RightLineStyle = CrLineStyleEnum.crLineStyleSingle;
      brd.TopLineStyle = CrLineStyleEnum.crLineStyleSingle;
      brd.BottomLineStyle = CrLineStyleEnum.crLineStyleSingle;
      brd.BackgroundColor = 0xFFFFFFFF;
      boxFld.Border = brd;
      RCD.ReportDefController.ReportObjectController.Add(boxFld, sec13, -1);
      Lg("caixa da grelha de metadados adicionada (field-as-box)");

      // 2) remover o bloco de texto da empresa (substituído pelo logótipo gráfico Subreport4/5)
      string[] toRemove = { "hdrCompNome", "hdrCompMorada", "hdrCompCidadeF", "hdrCompNifF" };
      foreach (var name in toRemove) {
        var ro = Find(rd, name);
        if (ro != null) { RCD.ReportDefController.ReportObjectController.Remove(ro); Lg("removido " + name); }
        else Lg("nao encontrado " + name);
      }

      string dir = System.IO.Path.GetDirectoryName(outPath); string nm = System.IO.Path.GetFileName(outPath); object od = dir;
      RCD.SaveAs(nm, ref od, 0);
      Lg("saved -> " + outPath);
      eng.Close();
    } catch (Exception ex) {
      Lg("FATAL: " + ex.Message);
      Lg(ex.StackTrace);
    }
    return log.ToString();
  }
}
