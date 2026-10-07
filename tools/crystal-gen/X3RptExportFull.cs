// X3RptExportFull -- exporta um relatorio X3 COMPLETO (principal + subreports) para texto/PDF contra
// uma BD real via um DSN ODBC local, remapeando todas as tabelas para <db>.<schema>.<tabela>.
// Os parametros (seqedt/numedt/etat/usr/...) apontam para um pedido de impressao ja existente em
// AREPORTM (as linhas ficam na BD depois de cada impressao), o que permite reproduzir exatamente o
// documento impresso. Uso: validar fixes com dados reais antes de publicar em producao.
using System;
using System.Text;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3RptExportFull {
  static StringBuilder log;
  static void Lg(string m){ log.Append(m).Append("\r\n"); }
  // Parametros extra "nome=valor;nome=valor" (ex. "X3FCT=GESSIH;X3PRF=ADMIN") que se sobrepoem aos defaults.
  public static string Extra = "";
  // Subreports a suprimir no teste ("nome;nome" ou "*" = todos) -- diagnostico por bissecao.
  public static string Suppress = "";

  static void Remap(Eng.Tables tables, string dsn, string db, string schema, string user, string pass, string who) {
    foreach (Eng.Table t in tables) {
      var li = t.LogOnInfo;
      li.ConnectionInfo.ServerName = dsn;
      li.ConnectionInfo.DatabaseName = db;
      li.ConnectionInfo.UserID = user;
      li.ConnectionInfo.Password = pass;
      t.ApplyLogOnInfo(li);
      string loc = t.Location; string baseName = loc.Substring(loc.LastIndexOf('.') + 1);
      t.Location = schema + "." + baseName;
      Lg(who + " " + t.Name + " : " + loc + " -> " + t.Location);
    }
  }

  public static string Export(string rptPath, string outPath, string format,
                              string dsn, string db, string schema, string user, string pass,
                              string usr, string etat, double numedt, double seqedt, string lan, string dos) {
    log = new StringBuilder();
    try {
      var eng = new Eng.ReportDocument();
      eng.Load(rptPath);
      Remap(eng.Database.Tables, dsn, db, schema, user, pass, "main");
      foreach (Eng.ReportDocument sub in eng.Subreports)
        Remap(sub.Database.Tables, dsn, db, schema, user, pass, "sub[" + sub.Name + "]");
      // subreports sem tabelas (so texto) rebentam o motor local com "Erro no arquivo" -> suprimidos no teste
      foreach (Eng.ReportObject ro in eng.ReportDefinition.ReportObjects) {
        var so = ro as Eng.SubreportObject;
        if (so == null) continue;
        if (eng.Subreports[so.SubreportName].Database.Tables.Count == 0 || (";" + Suppress + ";").Contains(";" + so.SubreportName + ";") || Suppress == "*") {
          so.ObjectFormat.EnableSuppress = true; Lg("suprimido " + so.Name + " (" + so.SubreportName + ")");
        }
      }

      foreach (Eng.ParameterFieldDefinition p in eng.DataDefinition.ParameterFields) {
        if (p.ReportName != "" ) continue;
        object v = null;
        string ex = null;
        foreach (string kv in (Extra ?? "").Split(';')) {
          int i = kv.IndexOf('=');
          if (i > 0 && kv.Substring(0, i).Trim() == p.Name) ex = kv.Substring(i + 1);
        }
        if (ex != null) {
          v = (p.ValueType == FieldValueType.NumberField) ? (object)double.Parse(ex, System.Globalization.CultureInfo.InvariantCulture) :
              (p.ValueType == FieldValueType.BooleanField) ? (object)bool.Parse(ex) :
              (p.ValueType == FieldValueType.DateField) ? (object)DateTime.Parse(ex, System.Globalization.CultureInfo.InvariantCulture) : (object)ex;
          eng.SetParameterValue(p.Name, v);
          continue;
        }
        switch (p.Name) {
          case "usr": v = usr; break;
          case "etat": v = etat; break;
          case "numedt": v = numedt; break;
          case "seqedt": v = seqedt; break;
          case "X3LAN": v = lan; break;
          case "X3DOS": v = dos; break;
          case "impselection": v = 0.0; break;
          case "imput": v = false; break;
          case "impdetiva": v = false; break;
          default:
            v = (p.ValueType == FieldValueType.NumberField) ? (object)0.0 :
                (p.ValueType == FieldValueType.BooleanField) ? (object)false :
                (p.ValueType == FieldValueType.DateField || p.ValueType == FieldValueType.DateTimeField) ?
                  (object)(p.Name.EndsWith("fin") ? new DateTime(2099, 12, 31) : new DateTime(1900, 1, 1)) : (object)"";
            break;
        }
        eng.SetParameterValue(p.Name, v);
      }
      Lg("parametros definidos");

      eng.ExportToDisk(format == "pdf" ? ExportFormatType.PortableDocFormat : ExportFormatType.Text, outPath);
      Lg("EXPORT OK -> " + outPath);
      eng.Close();
    } catch (Exception ex) {
      Lg("FATAL: " + ex.Message + " [" + ex.GetType().Name + "]");
      var ie = ex; while (ie.InnerException != null) { ie = ie.InnerException; Lg("  inner: " + ie.GetType().Name + " " + ie.Message); }
      if (ex.InnerException != null) Lg("inner: " + ex.InnerException.Message);
    }
    return log.ToString();
  }
}
