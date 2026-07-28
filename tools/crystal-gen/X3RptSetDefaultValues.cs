// X3RptSetDefaultValues -- segunda passagem: recarrega um .rpt JA GRAVADO (onde os parametros
// do subreport ja existem de facto, confirmados via reload) e define DefaultValues explicitos
// nos 3 parametros "Pm-..." do subreport RecPendHist.
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptSetDefaultValues {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append(" | "); }

  public static string Build(string rptPath, string outPath, string subName) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      Lg("loaded " + rptPath);

      var subDoc = rcd.SubreportController.GetSubreport(subName);
      int paramCount = 0;
      foreach (ISCRParameterField pfc in subDoc.DataDefController.DataDefinition.ParameterFields) paramCount++;
      Lg("parametros encontrados: " + paramCount);

      int okCount = 0;
      foreach (ISCRParameterField pf in subDoc.DataDefController.DataDefinition.ParameterFields) {
        try {
          var newPf = new ParameterFieldClass();
          newPf.Name = pf.Name;
          newPf.Type = pf.Type;
          newPf.ParameterType = pf.ParameterType;
          newPf.AllowNullValue = pf.AllowNullValue;
          newPf.ReportName = pf.ReportName;
          var dv = new ParameterFieldDiscreteValueClass();
          if (pf.Type == CrFieldValueTypeEnum.crFieldValueTypeNumberField) dv.Value = 0.0;
          else dv.Value = "";
          newPf.DefaultValues.Add(dv);
          subDoc.DataDefController.ParameterFieldController.Modify(pf, newPf);
          okCount++;
          Lg("DefaultValue definido para " + pf.Name);
        } catch (Exception e) { Lg("DefaultValue FALHOU para " + pf.Name + ": " + e.Message); }
      }
      Lg("total DefaultValues definidos: " + okCount);

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
