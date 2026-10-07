// X3RptPieceTest -- TESTE LOCAL do TEB_PIECE com dados reais, sem pedido de impressao em AREPORTM.
// 1) cria uma COPIA DE TESTE: formulas com TextOfChapter em stub, tabela AREPORTM trocada por um
//    Command constante (diario/referencial do documento), record selection simplificada para 1 peca;
// 2) exporta essa copia para PDF via DSN local (TEST_TEB211), remapeando as tabelas nativas.
// NUNCA publicar a copia gerada.
using System;
using System.Text;
using CrystalDecisions.Shared;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ClientDoc;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptPieceTest {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append("\r\n"); }

  static CrystalDecisions.ReportAppServer.DataDefModel.ConnectionInfo OdbcCi(string dsn, string db, string user, string pass) {
    var logon = new PropertyBagClass(); logon.Add("DSN", dsn); logon.Add("Database", db);
    logon.Add("UseDSNProperties", "False"); logon.Add("UID", user); logon.Add("PWD", pass);
    var attr = new PropertyBagClass(); attr.Add("Database DLL", "crdb_odbc.dll");
    attr.Add("QE_DatabaseName", db); attr.Add("QE_DatabaseType", "ODBC (RDO)");
    attr.Add("QE_ServerDescription", dsn); attr.Add("QE_SQLDB", "True"); attr.Add("SSO Enabled", "False");
    attr.Add("QE_LogonProperties", logon);
    var ci = new ConnectionInfoClass(); ci.Attributes = attr; ci.UserName = user; ci.Password = pass;
    ci.Kind = CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
    return ci;
  }

  public static string Build(string rptPath, string outPath, string typ, string num, string jou, string leg,
                             string dsn, string db, string user, string pass) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      var rcd = eng.ReportClientDocument;
      var ffc = rcd.DataDefController.FormulaFieldController;
      var targets = new System.Collections.Generic.List<ISCRFormulaField>();
      foreach (ISCRFormulaField ff in rcd.DataDefController.DataDefinition.FormulaFields)
        if (ff.Text != null && ff.Text.Contains("TextOfChapter")) targets.Add(ff);
      foreach (var ff in targets) {
        var nf = new FormulaFieldClass(); nf.Name = ff.Name;
        nf.Text = ff.Name == "typref" ? "\"Social\"" : "WhilePrintingRecords; \"\"";
        ffc.Modify(ff, nf); Lg("stub " + ff.Name);
      }
      // AREPORTM -> Command constante
      ISCRTable old = null;
      foreach (ISCRTable t in rcd.DatabaseController.Database.Tables) if (t.Alias == "AREPORTM") old = t;
      var cmd = new CommandTableClass();
      cmd.Name = "AREPORTM"; cmd.Alias = "AREPORTM";
      cmd.CommandText = "SELECT CAST(1 AS numeric(10,0)) AS NUMREQ_0, '" + jou + "' AS CLEA1_0, '" + leg + "' AS CLEA2_0, '" + leg + "' AS CLEA3_0";
      cmd.ConnectionInfo = OdbcCi(dsn, db, user, pass);
      if (old != null) { rcd.DatabaseController.SetTableLocation(old, cmd); Lg("AREPORTM -> command"); }
      else Lg("(sem AREPORTM no relatorio)");
      if (typ != "") rcd.DataDefController.RecordFilterController.SetFormulaText(
        "{GACCENTRY.TYP_0} = '" + typ + "' and {GACCENTRY.NUM_0} = '" + num + "' and {AFCTFCY.FNC_0} = 'GESGAS' and {AFCTFCY.PRFCOD_0} = 'ADMIN' and ({GACCENTRYD.LEDTYP_0} = {?referentiel} or {?touref})");
      string dir = System.IO.Path.GetDirectoryName(outPath); string nm = System.IO.Path.GetFileName(outPath); object od = dir;
      rcd.SaveAs(nm, ref od, 0);
      Lg("saved test copy -> " + outPath);
      eng.Close();
    } catch (Exception ex) { Lg("FATAL build: " + ex.Message); }
    return log.ToString();
  }

  static void Remap(Eng.Tables tables, string dsn, string db, string schema, string user, string pass, string who) {
    foreach (Eng.Table t in tables) {
      var li = t.LogOnInfo;
      li.ConnectionInfo.ServerName = dsn; li.ConnectionInfo.DatabaseName = db;
      li.ConnectionInfo.UserID = user; li.ConnectionInfo.Password = pass;
      t.ApplyLogOnInfo(li);
      if (t.Name == "AREPORTM" && who == "main") continue;   // command constante
      string loc = t.Location; string baseName = loc.Substring(loc.LastIndexOf('.') + 1);
      t.Location = schema + "." + baseName;
    }
  }

  // Modo "selecao real": nao mexe no relatorio (so stub dos UFL), exporta com a record selection
  // original e os parametros como o X3 os passa (intervalos de 1 peca, funcao/perfil/utilizador).
  public static string ExportReal(string rptPath, string outPdf, string dsn, string db, string schema, string user, string pass,
                                  string dos, string cpy, string fcy, string typ, string num, DateTime dat, string fct, string prf, string usr) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      Remap(eng.Database.Tables, dsn, db, schema, user, pass, "real");
      foreach (Eng.ReportDocument sub in eng.Subreports) Remap(sub.Database.Tables, dsn, db, schema, user, pass, "sub");
      foreach (Eng.ParameterFieldDefinition p in eng.DataDefinition.ParameterFields) {
        if (p.ReportName != "") continue;
        object v;
        switch (p.Name) {
          case "X3DOS": v = dos; break;
          case "X3LAN": v = "POR"; break;
          case "X3TIT": v = "Impressão de docum."; break;
          case "X3FCT": v = fct; break;
          case "X3PRF": v = prf; break;
          case "X3USR": v = usr; break;
          case "societe": v = cpy; break;
          case "sitedeb": case "sitefin": v = fcy; break;
          case "typpcedeb": case "typpcefin": v = typ; break;
          case "pcedeb": case "pcefin": v = num; break;
          case "datedeb": case "datefin": v = dat; break;
          case "referentiel": v = 1.0; break;
          case "numedt": v = 999999.0; break;   // pedido inexistente: prova que ja nao depende da AREPORTM
          default:
            v = (p.ValueType == FieldValueType.NumberField) ? (object)0.0 :
                (p.ValueType == FieldValueType.BooleanField) ? (object)false :
                (p.ValueType == FieldValueType.DateField) ? (object)DateTime.Today : (object)"";
            break;
        }
        eng.SetParameterValue(p.Name, v);
      }
      eng.ExportToDisk(ExportFormatType.PortableDocFormat, outPdf);
      Lg("EXPORT OK -> " + outPdf);
      eng.Close();
    } catch (Exception ex) {
      Lg("FATAL export: " + ex.Message);
      if (ex.InnerException != null) Lg("inner: " + ex.InnerException.Message);
    }
    return log.ToString();
  }

  public static string Export(string rptPath, string outPdf, string dsn, string db, string schema, string user, string pass, string dos) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      Remap(eng.Database.Tables, dsn, db, schema, user, pass, "main");
      foreach (Eng.ReportDocument sub in eng.Subreports) Remap(sub.Database.Tables, dsn, db, schema, user, pass, "sub");
      foreach (Eng.ParameterFieldDefinition p in eng.DataDefinition.ParameterFields) {
        if (p.ReportName != "") continue;
        object v;
        switch (p.Name) {
          case "X3DOS": v = dos; break;
          case "X3LAN": v = "POR"; break;
          case "X3TIT": v = "Impressão de docum."; break;
          case "referentiel": v = 1.0; break;
          case "touref": v = false; break;
          case "detana": v = false; break;
          case "saut": v = false; break;
          case "impselections": v = 0.0; break;
          case "numedt": v = 1.0; break;
          case "datedeb": v = new DateTime(2000,1,1); break;
          case "datefin": v = new DateTime(2099,12,31); break;
          default:
            v = (p.ValueType == FieldValueType.NumberField) ? (object)0.0 :
                (p.ValueType == FieldValueType.BooleanField) ? (object)false :
                (p.ValueType == FieldValueType.DateField) ? (object)DateTime.Today : (object)"";
            break;
        }
        eng.SetParameterValue(p.Name, v);
      }
      eng.ExportToDisk(ExportFormatType.PortableDocFormat, outPdf);
      Lg("EXPORT OK -> " + outPdf);
      eng.Close();
    } catch (Exception ex) {
      Lg("FATAL export: " + ex.Message);
      if (ex.InnerException != null) Lg("inner: " + ex.InnerException.Message);
    }
    return log.ToString();
  }
}
