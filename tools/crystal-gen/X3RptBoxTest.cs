using System;
using System.Text;
using System.Collections.Generic;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptBoxTest {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  static Section FindSection(ReportDefinition rd, string name) {
    var areas = new List<ISCRArea>();
    areas.Add(rd.ReportHeaderArea); areas.Add(rd.PageHeaderArea); areas.Add(rd.DetailArea);
    areas.Add(rd.ReportFooterArea); areas.Add(rd.PageFooterArea);
    for (int i = 0; i < 5; i++) {
      try { var a = rd.GroupHeaderArea[i]; if (a != null) areas.Add(a); } catch {}
      try { var a = rd.GroupFooterArea[i]; if (a != null) areas.Add(a); } catch {}
    }
    foreach (var area in areas) {
      if (area == null) continue;
      foreach (Section s in area.Sections) if (s.Name == name) return s;
    }
    return null;
  }

  public static string TestBlank(string blankPath, string outPath) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(blankPath);
      var rcd = eng.ReportClientDocument;
      var rd = rcd.ReportDefinition;
      Section detail = null;
      foreach (Section s in rd.DetailArea.Sections) { detail = s; break; }
      Lg("detail found=" + (detail != null) + " name=" + (detail != null ? detail.Name : "?"));
      var box = new BoxObjectClass();
      box.Kind = CrReportObjectKindEnum.crReportObjectKindBox;
      box.Left = 40; box.Top = 10; box.Width = 500; box.Height = 100; box.Name = "hdrBoxBlank";
      box.SectionName = detail.Name;
      var brd = new BorderClass();
      brd.LeftLineStyle = CrLineStyleEnum.crLineStyleSingle;
      brd.RightLineStyle = CrLineStyleEnum.crLineStyleSingle;
      brd.TopLineStyle = CrLineStyleEnum.crLineStyleSingle;
      brd.BottomLineStyle = CrLineStyleEnum.crLineStyleSingle;
      brd.BackgroundColor = 0xFFFFFFFF;
      box.Border = brd;
      rcd.ReportDefController.ReportObjectController.Add(box, detail, -1);
      Lg("Add(box) on blank seed detail OK");
      string dir = System.IO.Path.GetDirectoryName(outPath); string nm = System.IO.Path.GetFileName(outPath); object od = dir;
      rcd.SaveAs(nm, ref od, 0);
      Lg("saved");
      eng.Close();
    } catch (Exception ex) {
      Lg("FATAL: " + ex.Message);
      Lg(ex.StackTrace);
    }
    return log.ToString();
  }

  public static string Test(string rptPath, string outPath) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded");
      var rd = rcd.ReportDefinition;

      var sec13 = FindSection(rd, "Section13");

      // try clone-existing-box + Add (same trick that worked for FieldObject in general)
      try {
        ISCRReportObject box16 = null;
        foreach (var area in new List<ISCRArea> { rd.ReportHeaderArea, rd.PageHeaderArea, rd.DetailArea, rd.ReportFooterArea, rd.PageFooterArea }) {
          if (area == null) continue;
          foreach (Section s in area.Sections) foreach (ISCRReportObject ro in s.ReportObjects) if (ro.Name == "Box16") box16 = ro;
        }
        for (int i = 0; i < 5 && box16 == null; i++) {
          try { foreach (Section s in rd.GroupHeaderArea[i].Sections) foreach (ISCRReportObject ro in s.ReportObjects) if (ro.Name == "Box16") box16 = ro; } catch {}
          try { foreach (Section s in rd.GroupFooterArea[i].Sections) foreach (ISCRReportObject ro in s.ReportObjects) if (ro.Name == "Box16") box16 = ro; } catch {}
        }
        Lg("Box16 found=" + (box16 != null));
        // alternative: build the "bordered box" out of a FieldObject (empty formula) with a Border,
        // since Field/Text objects reliably support Add()+Width/Height persistence in this doc.
        try {
          rcd.DataDefController.FormulaFieldController.AddByName("fHdrBoxBorder", "''", CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
          var fo = new FieldObjectClass();
          fo.DataSource = "{@fHdrBoxBorder}"; fo.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeStringField;
          fo.Kind = CrReportObjectKindEnum.crReportObjectKindField;
          fo.Left = 40; fo.Top = 1860; fo.Width = 11800; fo.Height = 500; fo.Name = "hdrBoxAsField";
          var brd = new BorderClass();
          brd.LeftLineStyle = CrLineStyleEnum.crLineStyleSingle;
          brd.RightLineStyle = CrLineStyleEnum.crLineStyleSingle;
          brd.TopLineStyle = CrLineStyleEnum.crLineStyleSingle;
          brd.BottomLineStyle = CrLineStyleEnum.crLineStyleSingle;
          fo.Border = brd;
          rcd.ReportDefController.ReportObjectController.Add(fo, sec13, -1);
          Lg("Add(field-as-box with Border) OK");
          ISCRReportObject added = null;
          foreach (ISCRReportObject ro in sec13.ReportObjects) if (ro.Name == "hdrBoxAsField") added = ro;
          Lg("in-memory: L=" + added.Left + " T=" + added.Top + " W=" + added.Width + " H=" + added.Height);
        } catch (Exception ex) { Lg("field-as-box FAIL: " + ex.Message); }
      } catch (Exception ex) { Lg("Add(clone of Box16) FAIL: " + ex.Message); }

      // try FieldObject add (formula-field pattern, known-good elsewhere in the pipeline)
      try {
        rcd.DataDefController.FormulaFieldController.AddByName("fBoxTestLbl", "'X'", CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
        var fo = new FieldObjectClass();
        fo.DataSource = "{@fBoxTestLbl}"; fo.FieldValueType = CrFieldValueTypeEnum.crFieldValueTypeStringField;
        fo.Kind = CrReportObjectKindEnum.crReportObjectKindField;
        fo.Left = 40; fo.Top = 10; fo.Width = 500; fo.Height = 100; fo.Name = "fldBoxTest";
        rcd.ReportDefController.ReportObjectController.Add(fo, sec13, -1);
        Lg("Add(field) on Section13 OK");
      } catch (Exception ex) { Lg("Add(field) on Section13 FAIL: " + ex.Message); }

      // try box add via ReportDefController.ReportDefinition instead of plain rcd.ReportDefinition
      try {
        var rd2 = rcd.ReportDefController.ReportDefinition;
        Section sec13b = null;
        foreach (Section s in rd2.GroupHeaderArea[0].Sections) if (s.Name == "Section13") sec13b = s;
        Lg("sec13b via ReportDefController.ReportDefinition found=" + (sec13b != null));
        var box2 = new BoxObjectClass();
        box2.Kind = CrReportObjectKindEnum.crReportObjectKindBox;
        box2.Left = 40; box2.Top = 10; box2.Width = 500; box2.Height = 100; box2.Name = "hdrBoxTest2";
        rcd.ReportDefController.ReportObjectController.Add(box2, sec13b, -1);
        Lg("Add(box) via ReportDefController.ReportDefinition OK");
      } catch (Exception ex) { Lg("Add(box) via ReportDefController.ReportDefinition FAIL: " + ex.Message); }

      string dir = System.IO.Path.GetDirectoryName(outPath); string nm = System.IO.Path.GetFileName(outPath); object od = dir;
      rcd.SaveAs(nm, ref od, 0);
      Lg("saved");
      eng.Close();
    } catch (Exception ex) {
      Lg("FATAL: " + ex.Message);
      Lg(ex.StackTrace);
    }
    return log.ToString();
  }
}
