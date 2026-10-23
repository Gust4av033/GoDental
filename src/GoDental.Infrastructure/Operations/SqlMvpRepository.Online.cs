using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Infrastructure.Operations;
public sealed partial class SqlMvpRepository
{
    private const string OnlineKeys="(VALUES(N'WHATSAPP',p.TelefonoPublico),(N'WHATSAPP',p.WhatsAppPublico),(N'EMAIL',p.EmailPublico),(N'INSTAGRAM',p.Instagram),(N'FACEBOOK',p.Facebook))";
    private const string OnlineSuppressed="p.NoContactar=1 OR EXISTS(SELECT 1 FROM com.ListaNoContactar n CROSS APPLY "+OnlineKeys+" k(Canal,Valor) WHERE n.Activo=1 AND n.Canal=k.Canal AND com.fn_ClaveOnline(n.Identificador,CASE WHEN n.Canal=N'WHATSAPP' THEN N'TELEFONO' ELSE n.Canal END)=com.fn_ClaveOnline(k.Valor,CASE WHEN k.Canal=N'WHATSAPP' THEN N'TELEFONO' ELSE k.Canal END))";
    private const string OnlineColumns="p.IdProspecto Id,p.Nombre Name,p.TipoProspecto Type,p.TelefonoPublico Phone,p.WhatsAppPublico WhatsApp,p.EmailPublico Email,p.SitioWebPublico Website,p.Instagram,p.Facebook,p.Departamento Department,p.Municipio Municipality,p.Distrito District,p.Estado State,p.IdClienteConvertido CustomerId,c.NombreRazonSocial Customer,p.IdVentaConversion ConversionSaleId,v.NumeroVenta ConversionSale,p.FechaRegistro Created,p.NoContactar DoNotContact,p.RowVersion,CONVERT(bit,CASE WHEN "+OnlineSuppressed+" THEN 1 ELSE 0 END) Suppressed";
    private const string OnlineFrom="com.Prospectos p LEFT JOIN cli.Clientes c ON c.IdCliente=p.IdClienteConvertido LEFT JOIN ven.Ventas v ON v.IdVenta=p.IdVentaConversion";
    private static string[] OnlinePermissions=>["COMERCIAL.VER","CLIENTES.VER"];
    public Task<MvpPage<OnlineProspectData>> OnlineProspectsAsync(string search,string? state,MvpFilter f,int user,CancellationToken ct)=>Read(user,OnlinePermissions,w=>Page<OnlineProspectData>(w,OnlineFrom,OnlineColumns,"(@state IS NULL OR p.Estado=@state) AND (@search=N'' OR CHARINDEX(@search,p.Nombre)>0 OR CHARINDEX(@search,ISNULL(p.EmailPublico,N''))>0 OR CONVERT(nvarchar(30),p.IdProspecto)=@search OR CHARINDEX(@search,ISNULL(p.TelefonoPublico,N''))>0 OR CHARINDEX(@search,ISNULL(p.WhatsAppPublico,N''))>0 OR CHARINDEX(@search,ISNULL(p.SitioWebPublico,N''))>0 OR com.fn_ClaveOnline(p.TelefonoPublico,N'TELEFONO')=com.fn_ClaveOnline(@search,N'TELEFONO') OR com.fn_ClaveOnline(p.WhatsAppPublico,N'TELEFONO')=com.fn_ClaveOnline(@search,N'TELEFONO'))","p.IdProspecto DESC",new{search,state,Offset=checked((f.Page-1)*f.PageSize),f.PageSize}),ct);
    public Task<OnlineProspectData?> OnlineProspectAsync(long id,int user,CancellationToken ct)=>Read(user,OnlinePermissions,w=>OnlineCore(w,id,false),ct);
    private static Task<OnlineProspectData?> OnlineCore(Work w,long id,bool update)=>w.Maybe<OnlineProspectData>($"SELECT {OnlineColumns} FROM com.Prospectos p {(update?"WITH(UPDLOCK,HOLDLOCK)":"WITH(HOLDLOCK)")} LEFT JOIN cli.Clientes c ON c.IdCliente=p.IdClienteConvertido LEFT JOIN ven.Ventas v ON v.IdVenta=p.IdVentaConversion WHERE p.IdProspecto=@id;",new{id});
    public Task<MvpPage<OnlineSourceData>> OnlineSourcesAsync(long id,MvpFilter f,int user,CancellationToken ct)=>Read(user,OnlinePermissions,w=>Page<OnlineSourceData>(w,"com.FuentesProspecto","IdFuente Id,TipoFuente Type,UrlFuente Url,FechaObtencion Obtained,DatoObtenido Description","IdProspecto=@id","IdFuente DESC",new{id,Offset=checked((f.Page-1)*f.PageSize),f.PageSize}),ct);
    public Task<MvpPage<OnlineExclusion>> OnlineExclusionsAsync(string search,MvpFilter f,int user,CancellationToken ct)=>Read(user,OnlinePermissions,w=>Page<OnlineExclusion>(w,"com.ListaNoContactar n LEFT JOIN seg.Usuarios u ON u.IdUsuario=n.RegistradoPor","n.IdNoContactar Id,n.Canal Channel,n.Identificador Identifier,n.Motivo Reason,n.Activo Active,n.Fecha Date,u.NombreCompleto [User]","(@search=N'' OR CHARINDEX(@search,n.Identificador)>0)","n.IdNoContactar DESC",new{search,Offset=checked((f.Page-1)*f.PageSize),f.PageSize}),ct);
    public Task<MvpPage<OnlineEvent>> OnlineHistoryAsync(long id,MvpFilter f,int user,CancellationToken ct)=>Read(user,OnlinePermissions,w=>Page<OnlineEvent>(w,"sis.Bitacora b JOIN seg.Usuarios u ON u.IdUsuario=b.IdUsuario","b.Fecha Date,u.NombreCompleto [User],b.Accion Action,JSON_VALUE(b.DatosDespuesJson,'$.Reason') Reason","b.EntidadId=@id AND b.Accion IN(N'ONLINE_PROSPECTO',N'ONLINE_FUENTE',N'ONLINE_ESTADO',N'ONLINE_BLOQUEAR',N'ONLINE_CONVERTIR')","b.IdBitacora DESC",new{id,Offset=checked((f.Page-1)*f.PageSize),f.PageSize}),ct);
    public Task<MvpPage<SaleData>> OnlineSalesAsync(string search,MvpFilter f,int user,CancellationToken ct)=>Read(user,["COMERCIAL.VER","CLIENTES.VER","VENTAS.VER"],w=>Page<SaleData>(w,"ven.Ventas",SaleColumns,"Estado=N'CONFIRMADA' AND IdCliente IS NOT NULL AND EXISTS(SELECT 1 FROM cli.Clientes c WHERE c.IdCliente=ven.Ventas.IdCliente AND c.Activo=1) AND (@search=N'' OR CHARINDEX(@search,NumeroVenta)>0 OR CONVERT(nvarchar(30),IdVenta)=@search)","IdVenta DESC",new{search,Offset=checked((f.Page-1)*f.PageSize),f.PageSize}),ct);
    private const string MatchFrom="""
        (SELECT N'PROSPECTO' Entity,p.IdProspecto Id,p.Nombre Name,p.Estado State,
         CASE WHEN m.PhoneMatch=1 THEN N'Teléfono/WhatsApp' WHEN m.EmailMatch=1 THEN N'Correo' WHEN m.WebMatch=1 THEN N'Dominio empresarial' WHEN m.SocialMatch=1 THEN N'Perfil social' ELSE N'Nombre y municipio' END [Match]
         FROM com.Prospectos p WITH(HOLDLOCK)
         CROSS APPLY(SELECT
          CASE WHEN EXISTS(SELECT 1 FROM (VALUES(p.TelefonoPublico),(p.WhatsAppPublico)) a(v) CROSS JOIN (VALUES(@Phone),(@WhatsApp)) b(v) WHERE com.fn_ClaveOnline(a.v,N'TELEFONO')=com.fn_ClaveOnline(b.v,N'TELEFONO')) THEN 1 ELSE 0 END PhoneMatch,
          CASE WHEN com.fn_ClaveOnline(p.EmailPublico,N'EMAIL')=com.fn_ClaveOnline(@Email,N'EMAIL') THEN 1 ELSE 0 END EmailMatch,
          CASE WHEN p.DominioWeb=@Domain AND @Domain IS NOT NULL THEN 1 ELSE 0 END WebMatch,
          CASE WHEN com.fn_ClaveOnline(p.Instagram,N'INSTAGRAM')=com.fn_ClaveOnline(@Instagram,N'INSTAGRAM') OR com.fn_ClaveOnline(p.Facebook,N'FACEBOOK')=com.fn_ClaveOnline(@Facebook,N'FACEBOOK') THEN 1 ELSE 0 END SocialMatch,
          CASE WHEN com.fn_ClaveOnline(p.Nombre,N'TEXTO') COLLATE Latin1_General_100_CI_AI=com.fn_ClaveOnline(@Name,N'TEXTO') COLLATE Latin1_General_100_CI_AI
           AND @Department IS NOT NULL AND @Municipality IS NOT NULL AND com.fn_ClaveOnline(p.Departamento,N'TEXTO') COLLATE Latin1_General_100_CI_AI=com.fn_ClaveOnline(@Department,N'TEXTO') COLLATE Latin1_General_100_CI_AI AND com.fn_ClaveOnline(p.Municipio,N'TEXTO') COLLATE Latin1_General_100_CI_AI=com.fn_ClaveOnline(@Municipality,N'TEXTO') COLLATE Latin1_General_100_CI_AI THEN 1 ELSE 0 END NameMatch) m
         WHERE p.IdProspecto<>@ExcludeId AND (m.PhoneMatch=1 OR m.EmailMatch=1 OR m.WebMatch=1 OR m.SocialMatch=1 OR m.NameMatch=1)
         UNION ALL
         SELECT N'CLIENTE',c.IdCliente,c.NombreRazonSocial,CASE WHEN c.Activo=1 THEN N'ACTIVO' ELSE N'INACTIVO' END,
          CASE WHEN m.PhoneMatch=1 THEN N'Teléfono/WhatsApp' WHEN m.EmailMatch=1 THEN N'Correo' ELSE N'Nombre y municipio' END
         FROM cli.Clientes c WITH(HOLDLOCK)
         CROSS APPLY(SELECT
          CASE WHEN EXISTS(SELECT 1 FROM (SELECT c.Telefono v UNION ALL SELECT c.WhatsApp UNION ALL SELECT Telefono FROM cli.ClienteContactos WITH(HOLDLOCK) WHERE IdCliente=c.IdCliente UNION ALL SELECT WhatsApp FROM cli.ClienteContactos WITH(HOLDLOCK) WHERE IdCliente=c.IdCliente) a CROSS JOIN(VALUES(@Phone),(@WhatsApp)) b(v) WHERE com.fn_ClaveOnline(a.v,N'TELEFONO')=com.fn_ClaveOnline(b.v,N'TELEFONO')) THEN 1 ELSE 0 END PhoneMatch,
          CASE WHEN com.fn_ClaveOnline(c.Email,N'EMAIL')=com.fn_ClaveOnline(@Email,N'EMAIL') OR EXISTS(SELECT 1 FROM cli.ClienteContactos WITH(HOLDLOCK) WHERE IdCliente=c.IdCliente AND com.fn_ClaveOnline(Email,N'EMAIL')=com.fn_ClaveOnline(@Email,N'EMAIL')) THEN 1 ELSE 0 END EmailMatch,
          CASE WHEN (com.fn_ClaveOnline(c.NombreRazonSocial,N'TEXTO') COLLATE Latin1_General_100_CI_AI=com.fn_ClaveOnline(@Name,N'TEXTO') COLLATE Latin1_General_100_CI_AI OR com.fn_ClaveOnline(c.NombreComercial,N'TEXTO') COLLATE Latin1_General_100_CI_AI=com.fn_ClaveOnline(@Name,N'TEXTO') COLLATE Latin1_General_100_CI_AI)
           AND @Department IS NOT NULL AND @Municipality IS NOT NULL AND EXISTS(SELECT 1 FROM cli.ClienteDirecciones WITH(HOLDLOCK) WHERE IdCliente=c.IdCliente AND com.fn_ClaveOnline(Departamento,N'TEXTO') COLLATE Latin1_General_100_CI_AI=com.fn_ClaveOnline(@Department,N'TEXTO') COLLATE Latin1_General_100_CI_AI AND com.fn_ClaveOnline(Municipio,N'TEXTO') COLLATE Latin1_General_100_CI_AI=com.fn_ClaveOnline(@Municipality,N'TEXTO') COLLATE Latin1_General_100_CI_AI) THEN 1 ELSE 0 END NameMatch) m
         WHERE m.PhoneMatch=1 OR m.EmailMatch=1 OR m.NameMatch=1) matches
        """;
    private static object MatchParameters(OnlineProspectInput d,long id,MvpFilter f)=>new{ExcludeId=id,d.Name,d.Phone,d.WhatsApp,d.Email,Instagram=OnlineValidator.Identifier("INSTAGRAM",d.Instagram??"")??d.Instagram,Facebook=OnlineValidator.Identifier("FACEBOOK",d.Facebook??"")??d.Facebook,d.Department,d.Municipality,Domain=OnlineValidator.Domain(d.Website),Offset=checked((f.Page-1)*f.PageSize),f.PageSize};
    private static Task<MvpPage<OnlineMatch>> MatchesCore(Work w,OnlineProspectInput d,long id,MvpFilter f)=>Page<OnlineMatch>(w,MatchFrom,"Entity,Id,Name,State,[Match]","1=1","Entity,Id",MatchParameters(d,id,f));
    public Task<MvpPage<OnlineMatch>> OnlineCheckMatchesAsync(OnlineProspectInput data,long excludeId,MvpFilter f,int user,CancellationToken ct)=>Read(user,OnlinePermissions,w=>MatchesCore(w,data,excludeId,f),ct);
    public Task<MvpPage<OnlineMatch>> OnlineMatchesAsync(long id,MvpFilter f,int user,CancellationToken ct)=>Read(user,OnlinePermissions,async w=>await OnlineCore(w,id,false) is {} p?await MatchesCore(w,p,id,f):new([],0),ct);
    public Task<Result<MvpReceipt>> SaveOnlineAsync(object request,int user,int? station,CancellationToken ct)
    {
        if(OnlineValidator.Validate(request) is {} error)return Task.FromResult(Result<MvpReceipt>.Failure("VALIDATION",error));
        var r=OnlineValidator.Freeze(request);var key=OnlineValidator.Identity(r);
        string[] extra=r switch{ExcludeOnlineContact or BlockOnlineProspect=>["COMERCIAL.EXCLUSIONES"],ConvertOnlineProspect=>["COMERCIAL.PROSPECTAR","VENTAS.VER"],_=>["COMERCIAL.PROSPECTAR"]};
        return Write(key.Id,key.Type,r,user,station,[..OnlinePermissions,..extra],async w=>
        {
            await Station(w,station);var locked=await w.Scalar<int>("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource=N'GoDental:Online',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=5000;SELECT @r;");
            Require(locked>=0,"CONCURRENCY_CONFLICT","Otra estación está actualizando Online. Consulta y vuelve a revisar.");
            if(r is ExcludeOnlineContact e)return await ExcludeCore(w,e.Channel,e.Identifier,e.Reason,user);
            if(r is SaveOnlineProspect save)return await SaveProspectCore(w,save,user);
            if(r is ConvertOnlineProspect convert)
            {
                var sale=await w.Maybe<SaleData>($"SELECT {SaleColumns} FROM ven.Ventas WITH(UPDLOCK,HOLDLOCK) WHERE IdVenta=@SaleId;",convert);
                Version(sale?.RowVersion,convert.SaleVersion);Require(sale!.State=="CONFIRMADA"&&sale.CustomerId is not null,"INVALID_STATE","Selecciona una compra confirmada asociada a cliente.");
                Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM cli.Clientes WITH(HOLDLOCK) WHERE IdCliente=@CustomerId AND Activo=1) THEN 1 ELSE 0 END);",sale),"INVALID_STATE","El cliente debe estar activo.");
                var p=await OnlineCore(w,convert.ProspectId,true);Version(p?.RowVersion,convert.ExpectedVersion);Require(p!.State is not("CONVERTIDO" or "DESCARTADO")&&p.CustomerId is null&&p.ConversionSaleId is null,"INVALID_STATE","La ficha convertida/descartada no se vuelve a vincular.");
                Require(await w.Scalar<int>("SELECT COUNT(*) FROM com.FuentesProspecto WITH(HOLDLOCK) WHERE IdProspecto=@ProspectId;",convert)>0,"INVALID_STATE","Registra primero una fuente pública.");
                if(p.DoNotContact)await ExcludeProspectKeys(w,p,convert.Reason,user);
                await w.Execute("UPDATE com.Prospectos SET Estado=N'CONVERTIDO',IdClienteConvertido=@CustomerId,IdVentaConversion=@SaleId WHERE IdProspecto=@ProspectId;",new{sale.CustomerId,convert.SaleId,convert.ProspectId});return p.Id;
            }
            var (id,version)=r switch{AddOnlineSource s=>(s.ProspectId,s.ExpectedVersion),ChangeOnlineState s=>(s.ProspectId,s.ExpectedVersion),BlockOnlineProspect s=>(s.ProspectId,s.ExpectedVersion),_=>throw new ArgumentException("Operación desconocida.")};
            var current=await OnlineCore(w,id,true);Version(current?.RowVersion,version);
            switch(r)
            {
                case AddOnlineSource s:await SourceCore(w,id,s.Source);await w.Execute("UPDATE com.Prospectos SET Nombre=Nombre WHERE IdProspecto=@id;",new{id});break;
                case ChangeOnlineState s:Require(current!.State!="CONVERTIDO","INVALID_STATE","Una conversión no se revierte cambiando estado.");await w.Execute("UPDATE com.Prospectos SET Estado=@State WHERE IdProspecto=@ProspectId;",s);break;
                case BlockOnlineProspect s:await ExcludeProspectKeys(w,current!,s.Reason,user);await w.Execute("UPDATE com.Prospectos SET NoContactar=1 WHERE IdProspecto=@id;",new{id});break;
            }
            return id;
        },ct);
    }
    private static async Task<long> SaveProspectCore(Work w,SaveOnlineProspect r,int user)
    {
        var old=r.Id==0?null:await OnlineCore(w,r.Id,true);if(r.Id>0){Version(old?.RowVersion,r.ExpectedVersion);Require(old!.State!="CONVERTIDO"&&old.CustomerId is null,"INVALID_STATE","Los datos del cliente convertido se mantienen desde Clientes.");}
        Require(OnlineValidator.Changed(r.Data,old) is null,"VALIDATION",OnlineValidator.Changed(r.Data,old)??"");
        var matches=await MatchesCore(w,r.Data,r.Id,new(1,1));Require(matches.Total==0,"DUPLICATE","Hay coincidencias con prospectos/clientes. No se guardó otra ficha; revisa el registro existente o usa Clientes.");
        var d=OnlineValidator.Normalize(r.Data,old);var domain=OnlineValidator.Domain(d.Website);
        var id=r.Id==0?await w.Scalar<long>("INSERT INTO com.Prospectos(Nombre,FechaRegistro) OUTPUT INSERTED.IdProspecto VALUES(@Name,SYSDATETIMEOFFSET());",d):r.Id;
        await w.Execute("""
            UPDATE com.Prospectos SET Nombre=@Name,TipoProspecto=@Type,TelefonoPublico=@Phone,WhatsAppPublico=@WhatsApp,EmailPublico=@Email,
             SitioWebPublico=@Website,DominioWeb=@domain,Instagram=@Instagram,Facebook=@Facebook,Departamento=@Department,Municipio=@Municipality,Distrito=@District WHERE IdProspecto=@id;
            """,new{id,d.Name,d.Type,d.Phone,d.WhatsApp,d.Email,d.Website,domain,d.Instagram,d.Facebook,d.Department,d.Municipality,d.District});
        if(r.Source is not null)await SourceCore(w,id,r.Source);
        if(old?.DoNotContact==true)await ExcludeProspectKeys(w,d,r.Reason,user);
        return id;
    }
    private static async Task SourceCore(Work w,long id,OnlineSourceInput source)
    {
        Require(OnlineValidator.Source(source) is null,"VALIDATION",OnlineValidator.Source(source)??"");
        var today=await w.Scalar<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');");
        Require(source.Obtained.Date<=today,"VALIDATION","La fuente no puede tener fecha futura.");
        var obtained=new DateTimeOffset(DateTime.SpecifyKind(source.Obtained.Date,DateTimeKind.Unspecified),TimeSpan.FromHours(-6));
        Require(!await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM com.FuentesProspecto WITH(UPDLOCK,HOLDLOCK) WHERE IdProspecto=@id AND TipoFuente=@Type AND UrlFuente=@Url AND FechaObtencion=@obtained) THEN 1 ELSE 0 END);",new{id,source.Type,source.Url,obtained}),"DUPLICATE","Esta fuente/fecha ya está registrada.");
        await w.Execute("INSERT INTO com.FuentesProspecto(IdProspecto,TipoFuente,UrlFuente,FechaObtencion,DatoObtenido) VALUES(@id,@Type,@Url,@obtained,@Description);",new{id,source.Type,source.Url,obtained,source.Description});
    }
    private static async Task ExcludeProspectKeys(Work w,OnlineProspectInput p,string reason,int user)
    {foreach(var (channel,value) in new[]{("WHATSAPP",p.Phone),("WHATSAPP",p.WhatsApp),("EMAIL",p.Email),("INSTAGRAM",p.Instagram),("FACEBOOK",p.Facebook)})if(!string.IsNullOrWhiteSpace(value))await ExcludeCore(w,channel,value,reason,user);}
    private static async Task<long> ExcludeCore(Work w,string channel,string value,string reason,int user)
    {
        var kind=channel=="WHATSAPP"?"TELEFONO":channel;
        var normalized=await w.Scalar<string?>("SELECT com.fn_ClaveOnline(@value,@kind);",new{value,kind});if(normalized is null)return 0;
        var id=await w.Scalar<long?>("SELECT TOP(1) IdNoContactar FROM com.ListaNoContactar WITH(UPDLOCK,HOLDLOCK) WHERE Canal=@channel AND com.fn_ClaveOnline(Identificador,@kind)=@normalized ORDER BY Activo DESC,IdNoContactar;",new{channel,kind,normalized});
        if(id is null)return await w.Scalar<long>("INSERT INTO com.ListaNoContactar(Canal,Identificador,Motivo,RegistradoPor) OUTPUT INSERTED.IdNoContactar VALUES(@channel,@normalized,@reason,@user);",new{channel,normalized,reason,user});
        await w.Execute("UPDATE com.ListaNoContactar SET Activo=1,Motivo=@reason,Fecha=SYSDATETIMEOFFSET(),RegistradoPor=@user WHERE IdNoContactar=@id AND Activo=0;",new{id,reason,user});return id.Value;
    }
}
