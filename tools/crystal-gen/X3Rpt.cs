// X3Rpt — gerador programático de relatórios Crystal Reports para Sage X3
// Compilar via Add-Type em PowerShell 32-BIT (ver Generate-X3Report.ps1).
// Padrão validado: Command SQL + ligação OLE DB (build) -> repoint p/ ODBC DSN (X3).
//
// A parte REUTILIZÁVEL é a infraestrutura (OdbcCi, Raw, Label, DbF, summaries,
// landscape, repoint ODBC, parâmetro+record selection). A parte por-relatório
// é o bloco de layout dentro de Build() (colunas, títulos, agrupamento) e o SQL.
using System;
using System.Text;
using CrystalDecisions.ReportAppServer.ClientDoc;
using CrystalDecisions.ReportAppServer.DataDefModel;
using CrystalDecisions.ReportAppServer.ReportDefModel;
using CrystalDecisions.Shared;
using Eng = CrystalDecisions.CrystalReports.Engine;

public class X3Rpt {
  static StringBuilder log; static int lblN;
  static ISCDReportClientDocument RCD;
  static void Lg(string m){ log.Append(m).Append(" | "); }
  static ISCRField Fld(ISCRTable t,string n){ foreach(ISCRField f in t.DataFields) if(f.Name==n) return f; return null; }
  static Section Sec(ISCRArea a){ Section r=null; foreach(Section s in a.Sections){ r=s; break; } return r; }

  // campo bound a um data source/fórmula, com tipo e alinhamento
  static void Raw(Section sec,string ds,CrFieldValueTypeEnum vt,int l,int t,int w,int h,string nm,CrAlignmentEnum al){
    var fo=new FieldObjectClass(); fo.DataSource=ds; fo.FieldValueType=vt; fo.Kind=CrReportObjectKindEnum.crReportObjectKindField;
    fo.Left=l; fo.Top=t; fo.Width=w; fo.Height=h; fo.Name=nm;
    try{ if(fo.Format!=null) fo.Format.HorizontalAlignment=al; }catch{}
    RCD.ReportDefController.ReportObjectController.Add(fo,sec,-1);
  }
  // texto estático: SimpleText/Paragraphs fazem CRASH -> usar formula-field com literal
  static void Label(Section sec,string text,int l,int t,int w,int h,CrAlignmentEnum al){
    string fn="fL"+(lblN++); string esc=text.Replace("'","''");
    RCD.DataDefController.FormulaFieldController.AddByName(fn,"'"+esc+"'",CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
    Raw(sec,"{@"+fn+"}",CrFieldValueTypeEnum.crFieldValueTypeStringField,l,t,w,h,fn,al);
  }
  // campo da BD: usa SEMPRE o .Type real do campo (ex.: SQL date chega como DateTime)
  static void DbF(Section sec,ISCRTable T,string col,int l,int t,int w,int h,CrAlignmentEnum al){
    var f=Fld(T,col); Raw(sec,f.FormulaForm,f.Type,l,t,w,h,"d_"+col,al);
  }

  // ligação ODBC (RDO) via DSN — a forma que o print engine do X3 espera (crdb_odbc.dll)
  static CrystalDecisions.ReportAppServer.DataDefModel.ConnectionInfo OdbcCi(string dsn,string db,string user,string pass){
    var logon=new PropertyBagClass(); logon.Add("DSN",dsn); logon.Add("Database",db); logon.Add("UseDSNProperties","False");
    var attr=new PropertyBagClass(); attr.Add("Database DLL","crdb_odbc.dll"); attr.Add("QE_DatabaseName",db); attr.Add("QE_DatabaseType","ODBC (RDO)"); attr.Add("QE_ServerDescription",dsn); attr.Add("QE_SQLDB","True"); attr.Add("SSO Enabled","False"); attr.Add("QE_LogonProperties",logon);
    var ci=new ConnectionInfoClass(); ci.Attributes=attr; ci.UserName=user; ci.Password=pass; ci.Kind=CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
    return ci;
  }

  // workServer = servidor OLE DB ALCANÇÁVEL no build (ex. IP) p/ obter os campos;
  // dsn = DSN ODBC do X3 (ex. ADX_CRCNN_TEBX3) p/ runtime no print server.
  public static string Build(string blankPath,string outRpt,string sql,
                             string workServer,string dsn,string db,string user,string pass,string companyName){
    if(string.IsNullOrEmpty(companyName)) companyName="Empresa";
    log=new StringBuilder(); lblN=0;
    var L=CrAlignmentEnum.crAlignmentLeft; var R=CrAlignmentEnum.crAlignmentRight;
    var SS=CrFieldValueTypeEnum.crFieldValueTypeStringField;
    try {
      var eng=new Eng.ReportDocument(); eng.Load(blankPath);
      RCD=eng.ReportClientDocument; Lg("loaded");
      // BUILD com OLE DB alcançável (obtém os campos do Command)
      var logon=new PropertyBagClass(); logon.Add("Provider","SQLOLEDB"); logon.Add("Data Source",workServer); logon.Add("Initial Catalog",db); logon.Add("Integrated Security","False");
      var attr=new PropertyBagClass(); attr.Add("Database DLL","crdb_ado.dll"); attr.Add("QE_DatabaseName",db); attr.Add("QE_DatabaseType","OLE DB (ADO)"); attr.Add("QE_ServerDescription",workServer); attr.Add("QE_SQLDB","True"); attr.Add("SSO Enabled","False"); attr.Add("QE_LogonProperties",logon);
      var ci=new ConnectionInfoClass(); ci.Attributes=attr; ci.UserName=user; ci.Password=pass; ci.Kind=CrConnectionInfoKindEnum.crConnectionInfoKindCRQE;
      var tbl=new CommandTableClass(); tbl.Name="X3MOV"; tbl.Alias="X3MOV"; tbl.ConnectionInfo=ci; tbl.CommandText=sql;
      RCD.DatabaseController.AddTable(tbl,null); Lg("addtable");
      ISCRTable T=null; foreach(ISCRTable t in RCD.Database.Tables) if(t.Name=="X3MOV") T=t;

      // LANDSCAPE robusto (dissociado da impressora + dimensões A4 explícitas)
      try{
        var po=RCD.PrintOutputController.GetPrintOptions();
        po.PaperSize=CrPaperSizeEnum.crPaperSizePaperA4;
        po.PaperOrientation=CrPaperOrientationEnum.crPaperOrientationLandscape;
        po.DissociatePageSizeAndPrinterPaperSize=true;
        RCD.PrintOutputController.ModifyPrintOptions(po);
        RCD.PrintOutputController.ModifyUserPaperSize(11906, 16838); // (altura, largura) twips A4 landscape
        RCD.PrintOutputController.ModifyPaperOrientation(CrPaperOrientationEnum.crPaperOrientationLandscape);
        Lg("landscape");
      }catch(Exception e){ Lg("landscape ERR:"+e.Message); }

      // ===================== LAYOUT POR-RELATÓRIO (adaptar) =====================
      Group grp=null;
      try{ grp=new GroupClass(); grp.ConditionField=Fld(T,"Site"); RCD.DataDefController.GroupController.Add(0,grp); Lg("group"); }catch(Exception e){ Lg("group ERR:"+e.Message); }

      var RDF=RCD.ReportDefinition; int H=230;
      int[] cL={0,1150,3050,5200,8400,9750,11050,11700,13250};
      int[] cW={1150,1900,2150,3200,1350,1300,600,1500,1500};
      string[] hdr={"Data","Tipo Movimento","Documento","Descritivo","Lote","Qtd","Un","Valor","Saldo"};
      CrAlignmentEnum[] al={L,L,L,L,L,R,L,R,R};

      try{ var s=Sec(RDF.PageHeaderArea); for(int i=0;i<hdr.Length;i++) Label(s,hdr[i],cL[i],60,cW[i],H,al[i]); Lg("pagehdr"); }catch(Exception e){ Lg("pagehdr ERR:"+e.Message); }
      try{ var s=Sec(RDF.ReportHeaderArea);
        Label(s,companyName,0,0,9000,300,L);
        Label(s,"Extrato de Movimentos de Stock",0,330,9000,300,L);
        Label(s,"Artigo:",0,700,800,H,L); DbF(s,T,"Artigo",820,700,1800,H,L); DbF(s,T,"Descricao",2650,700,6000,H,L);
        Lg("rephdr");
      }catch(Exception e){ Lg("rephdr ERR:"+e.Message); }
      try{ var s=Sec(RDF.GroupHeaderArea[0]);
        Label(s,"Site:",0,40,600,H,L); DbF(s,T,"Site",650,40,900,H,L); DbF(s,T,"SiteNome",1600,40,4000,H,L); Lg("grphdr");
      }catch(Exception e){ Lg("grphdr ERR:"+e.Message); }
      try{ var s=Sec(RDF.DetailArea);
        DbF(s,T,"DataMov",cL[0],0,cW[0],H,L); DbF(s,T,"TipoMov",cL[1],0,cW[1],H,L);
        DbF(s,T,"NumDoc",cL[2],0,cW[2],H,L);  DbF(s,T,"Descritivo",cL[3],0,cW[3],H,L);
        DbF(s,T,"Lote",cL[4],0,cW[4],H,L);    DbF(s,T,"Qtd",cL[5],0,cW[5],H,R);
        DbF(s,T,"Un",cL[6],0,cW[6],H,L);      DbF(s,T,"Valor",cL[7],0,cW[7],H,R);
        DbF(s,T,"Saldo",cL[8],0,cW[8],H,R); Lg("details");
      }catch(Exception e){ Lg("details ERR:"+e.Message); }

      // SUMMARIES: SummaryFieldController.Add AUTO-COLOCA o campo na footer (não adicionar outro!)
      Func<ISCRField,Group,int> mkSum=(field,g)=>{
        var sf=new SummaryFieldClass(); sf.Operation=CrSummaryOperationEnum.crSummaryOperationSum; sf.SummarizedField=field; sf.Group=g;
        return RCD.DataDefController.SummaryFieldController.Add(-1,sf);
      };
      var qf=Fld(T,"Qtd"); var vf=Fld(T,"Valor");
      try{ var s=Sec(RDF.GroupFooterArea[0]); Label(s,"Subtotal site:",cL[1],20,2000,H,R); mkSum(qf,grp); mkSum(vf,grp); Lg("grpftr"); }catch(Exception e){ Lg("grpftr ERR:"+e.Message); }
      try{ var s=Sec(RDF.ReportFooterArea); Label(s,"TOTAL GERAL:",cL[1],20,2000,H,R); mkSum(qf,null); mkSum(vf,null); Lg("repftr"); }catch(Exception e){ Lg("repftr ERR:"+e.Message); }

      // RODAPÉ: data + página N de M
      try{
        RCD.DataDefController.FormulaFieldController.AddByName("fImpresso","'Impresso em ' + ToText(CurrentDateTime,'dd/MM/yyyy HH:mm')",CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
        RCD.DataDefController.FormulaFieldController.AddByName("fPagina","'Página ' + ToText(PageNumber,0) + ' de ' + ToText(TotalPageCount,0)",CrFormulaSyntaxEnum.crFormulaSyntaxCrystal);
        var s=Sec(RDF.PageFooterArea);
        Raw(s,"{@fImpresso}",SS,0,40,5000,H,"pfdt",L);
        Raw(s,"{@fPagina}",SS,11250,40,3500,H,"pfpg",R); Lg("pageftr");
      }catch(Exception e){ Lg("pageftr ERR:"+e.Message); }
      // ===================== FIM LAYOUT POR-RELATÓRIO =====================

      // PARÂMETROS + RECORD SELECTION (parâmetros de Command no RAS são problemáticos; usar record selection)
      // requer no SQL: coluna DataReal = data real (p/ filtrar) além de DataMov (string p/ mostrar)
      // default só pré-preenche o ecrã do X3; o motor EXIGE valor em runtime -> declarar os params em AREPORTD
      Action<string,CrFieldValueTypeEnum,object> addParam = (pname,ty,defval)=>{
        try{ var pf=new ParameterFieldClass(); pf.Name=pname; pf.Type=ty;
             pf.ParameterType=CrParameterFieldTypeEnum.crParameterFieldTypeReportParameter; pf.AllowNullValue=true;
             var dv1=new ParameterFieldDiscreteValueClass(); dv1.Value=defval; pf.DefaultValues.Add(dv1);
             var dv2=new ParameterFieldDiscreteValueClass(); dv2.Value=defval; pf.CurrentValues.Add(dv2);
             RCD.DataDefController.ParameterFieldController.Add(pf); Lg("param "+pname);
        }catch(Exception e){ Lg("param "+pname+" ERR:"+e.Message); }
      };
      // nomes EXATOS dos códigos do AREPORTD. "Limite" (datdeb/sitedeb) => o supervisor X3 gera o par fin.
      // padrão X3 de RANGE (ver ZPENDENTES): pares deb/fin + sintaxe "{campo} in {?deb} to {?fin}".
      // No AREPORTD declarar SÓ os "deb" como "Limite" (artigo Único, datedeb Limite, sitedeb Limite);
      // o supervisor X3 gera e passa datefin/sitefin. NÃO declarar os "fin" à parte (senão aloca 2x -> ERR 504).
      addParam("artigo",  CrFieldValueTypeEnum.crFieldValueTypeStringField, "M003147");
      addParam("datedeb", CrFieldValueTypeEnum.crFieldValueTypeDateField,   new DateTime(2000,1,1));
      addParam("datefin", CrFieldValueTypeEnum.crFieldValueTypeDateField,   new DateTime(2099,12,31));
      addParam("sitedeb", CrFieldValueTypeEnum.crFieldValueTypeStringField, "");
      addParam("sitefin", CrFieldValueTypeEnum.crFieldValueTypeStringField, "zzzzzz");
      try{
        eng.RecordSelectionFormula =
          "{X3MOV.Artigo} = {?artigo}"
        + " and {X3MOV.DataReal} in {?datedeb} to {?datefin}"
        + " and {X3MOV.Site} in {?sitedeb} to {?sitefin}";
        Lg("recsel");
      }catch(Exception e){ Lg("recsel ERR:"+e.Message); }

      // REPOINT p/ ODBC DSN sem verificar (só SetTableLocation persiste p/ CommandTable)
      try{
        ISCRTable t2=null; foreach(ISCRTable tt in RCD.Database.Tables){ if(tt.Name=="X3MOV") t2=tt; }
        var nt=new CommandTableClass(); nt.Name="X3MOV"; nt.Alias="X3MOV"; nt.CommandText=sql; nt.ConnectionInfo=OdbcCi(dsn,db,user,pass);
        RCD.DatabaseController.SetTableLocation(t2, nt); Lg("repoint->ODBC "+dsn);
      } catch(Exception e){ Lg("repoint ERR:"+e.Message); }

      string dir=System.IO.Path.GetDirectoryName(outRpt); string nm=System.IO.Path.GetFileName(outRpt); object od=dir;
      RCD.SaveAs(nm,ref od,0); Lg("saved");
      eng.Close();
    } catch(Exception ex){ Lg("FATAL:"+ex.Message); }
    return log.ToString();
  }
}
