using System.Text.Json;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Infrastructure.Operations;
public sealed partial class SqlMvpRepository
{
    private const string SegmentColumns="IdSegmento Id,Nombre Name,Departamento Department,Municipio Municipality,TipoProspecto Type,EstadoProspecto State,TipoFuente Source,Canal Channel,Activo Active,RowVersion";
    private const string TemplateColumns="IdPlantilla Id,Nombre Name,Canal Channel,Contenido Content,Activo Active,RowVersion";
    private const string CampaignSuppressed=OnlineSuppressed+" OR p.Estado IN(N'CONVERTIDO',N'DESCARTADO') OR d.VersionProspecto IS NULL OR d.VersionProspecto<>p.RowVersion";
    private const string CampaignColumns="a.IdCampania Id,a.Nombre Name,a.Canal Channel,a.Estado State,a.IdSegmento SegmentId,s.Nombre Segment,a.CriteriosJson CriteriaJson,a.ContenidoSnapshot Content,a.RowVersion,u.NombreCompleto ApprovedBy,a.FechaAprobacion ApprovedAt,(SELECT COUNT(*) FROM com.CampaniaDestinatarios d WHERE d.IdCampania=a.IdCampania) Recipients,(SELECT COUNT(*) FROM com.CampaniaDestinatarios d JOIN com.Prospectos p ON p.IdProspecto=d.IdProspecto WHERE d.IdCampania=a.IdCampania AND ("+CampaignSuppressed+")) Suppressed";
    private const string CampaignFrom="com.Campanias a LEFT JOIN com.SegmentosProspecto s ON s.IdSegmento=a.IdSegmento LEFT JOIN seg.Usuarios u ON u.IdUsuario=a.AprobadaPor";
    private const string SegmentWhere="p.Estado NOT IN(N'CONVERTIDO',N'DESCARTADO') AND EXISTS(SELECT 1 FROM com.FuentesProspecto f WHERE f.IdProspecto=p.IdProspecto) AND NOT("+OnlineSuppressed+") AND (@Department IS NULL OR p.Departamento=@Department) AND (@Municipality IS NULL OR p.Municipio=@Municipality) AND (@Type IS NULL OR p.TipoProspecto=@Type) AND (@State IS NULL OR p.Estado=@State) AND (@Source IS NULL OR EXISTS(SELECT 1 FROM com.FuentesProspecto f WHERE f.IdProspecto=p.IdProspecto AND f.TipoFuente=@Source)) AND NULLIF(LTRIM(RTRIM(CASE @Channel WHEN N'WHATSAPP' THEN p.WhatsAppPublico WHEN N'EMAIL' THEN p.EmailPublico WHEN N'INSTAGRAM' THEN p.Instagram WHEN N'FACEBOOK' THEN p.Facebook END)),N'') IS NOT NULL";
    private static object PlanningPage(string search,MvpFilter f)=>new{search,Offset=checked((f.Page-1)*f.PageSize),f.PageSize};
    private static object SegmentParams(OnlineSegmentFilter s,MvpFilter f)=>new{s.Department,s.Municipality,s.Type,s.State,s.Source,s.Channel,Offset=checked((f.Page-1)*f.PageSize),f.PageSize};
    public Task<MvpPage<OnlineZone>> OnlineZonesAsync(string search,MvpFilter f,int user,CancellationToken ct)=>Read(user,OnlinePermissions,w=>Page<OnlineZone>(w,"com.ZonasProspeccion z","z.IdZona Id,z.Departamento Department,z.Municipio Municipality,z.Prioridad Priority,z.Activo Active,z.RowVersion,(SELECT COUNT(*) FROM com.Prospectos p WHERE p.Departamento=z.Departamento AND (z.Municipio IS NULL OR p.Municipio=z.Municipio)) Prospects","(@search=N'' OR CHARINDEX(@search,z.Departamento)>0 OR CHARINDEX(@search,ISNULL(z.Municipio,N''))>0)","z.Activo DESC,z.Prioridad,z.IdZona",PlanningPage(search,f)),ct);
    public Task<MvpPage<OnlineSegment>> OnlineSegmentsAsync(string search,MvpFilter f,int user,CancellationToken ct)=>Read(user,OnlinePermissions,w=>Page<OnlineSegment>(w,"com.SegmentosProspecto",SegmentColumns,"(@search=N'' OR CHARINDEX(@search,Nombre)>0)","IdSegmento DESC",PlanningPage(search,f)),ct);
    public Task<MvpPage<OnlineProspectData>> OnlineSegmentPreviewAsync(OnlineSegmentFilter filter,MvpFilter f,int user,CancellationToken ct)=>Read(user,OnlinePermissions,w=>Page<OnlineProspectData>(w,OnlineFrom,OnlineColumns,SegmentWhere,"p.IdProspecto",SegmentParams(filter,f)),ct);
    public Task<MvpPage<OnlineTemplate>> OnlineTemplatesAsync(string search,MvpFilter f,int user,CancellationToken ct)=>Read(user,OnlinePermissions,w=>Page<OnlineTemplate>(w,"com.PlantillasMensaje",TemplateColumns,"(@search=N'' OR CHARINDEX(@search,Nombre)>0)","IdPlantilla DESC",PlanningPage(search,f)),ct);
    public Task<MvpPage<OnlineCampaign>> OnlineCampaignsAsync(string search,MvpFilter f,int user,CancellationToken ct)=>Read(user,OnlinePermissions,w=>Page<OnlineCampaign>(w,CampaignFrom,CampaignColumns,"(@search=N'' OR CHARINDEX(@search,a.Nombre)>0)","a.IdCampania DESC",PlanningPage(search,f)),ct);
    public Task<MvpPage<OnlineRecipient>> OnlineRecipientsAsync(long campaignId,MvpFilter f,int user,CancellationToken ct)=>Read(user,OnlinePermissions,w=>Page<OnlineRecipient>(w,"com.CampaniaDestinatarios d JOIN com.Prospectos p ON p.IdProspecto=d.IdProspecto","p.IdProspecto Id,p.Nombre Name,d.ContactoSnapshot Contact,d.Estado State,CONVERT(bit,CASE WHEN "+CampaignSuppressed+" THEN 1 ELSE 0 END) Suppressed","d.IdCampania=@campaignId","d.IdDestinatario",new{campaignId,Offset=checked((f.Page-1)*f.PageSize),f.PageSize}),ct);
    public Task<MvpPage<OnlineFollowUp>> OnlineFollowUpsAsync(string search,long? prospectId,MvpFilter f,int user,CancellationToken ct)=>Read(user,OnlinePermissions,w=>Page<OnlineFollowUp>(w,"com.SeguimientosProspecto h JOIN com.Prospectos p ON p.IdProspecto=h.IdProspecto JOIN seg.Usuarios u ON u.IdUsuario=h.IdUsuario","h.IdSeguimiento Id,h.IdProspecto ProspectId,p.Nombre Name,h.Resultado Result,h.Canal Channel,h.Fecha Date,h.ProximaGestion NextDate,h.Nota Note,u.NombreCompleto [User]","(@prospectId IS NULL OR h.IdProspecto=@prospectId) AND (@search=N'' OR CHARINDEX(@search,p.Nombre)>0 OR CHARINDEX(@search,h.Nota)>0)","h.Fecha DESC,h.IdSeguimiento DESC",new{search,prospectId,Offset=checked((f.Page-1)*f.PageSize),f.PageSize}),ct);
    private static async Task<long> SaveOnlinePlanning(Work w,object request,int user)
    {
        switch(request)
        {
            case SaveOnlineZone r:
            {
                if(r.Id>0)Version(await w.Scalar<byte[]?>("SELECT RowVersion FROM com.ZonasProspeccion WITH(UPDLOCK,HOLDLOCK) WHERE IdZona=@Id;",r),r.ExpectedVersion);
                Require(!await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM com.ZonasProspeccion WITH(UPDLOCK,HOLDLOCK) WHERE IdZona<>@Id AND Departamento=@Department AND ISNULL(Municipio,N'')=ISNULL(@Municipality,N'')) THEN 1 ELSE 0 END);",r),"DUPLICATE","Ya existe esta zona; actualízala en su ficha.");
                if(r.Id==0)return await w.Scalar<long>("INSERT INTO com.ZonasProspeccion(Departamento,Municipio,Prioridad,Activo) OUTPUT INSERTED.IdZona VALUES(@Department,@Municipality,@Priority,@Active);",r);
                await w.Execute("UPDATE com.ZonasProspeccion SET Departamento=@Department,Municipio=@Municipality,Prioridad=@Priority,Activo=@Active WHERE IdZona=@Id;",r);return r.Id;
            }
            case SaveOnlineSegment r:
            {
                if(r.Id>0)Version(await w.Scalar<byte[]?>("SELECT RowVersion FROM com.SegmentosProspecto WITH(UPDLOCK,HOLDLOCK) WHERE IdSegmento=@Id;",r),r.ExpectedVersion);
                var p=new{r.Id,r.Name,r.Active,r.Filter.Department,r.Filter.Municipality,r.Filter.Type,r.Filter.State,r.Filter.Source,r.Filter.Channel};
                if(r.Id==0)return await w.Scalar<long>("INSERT INTO com.SegmentosProspecto(Nombre,Departamento,Municipio,TipoProspecto,EstadoProspecto,TipoFuente,Canal,Activo) OUTPUT INSERTED.IdSegmento VALUES(@Name,@Department,@Municipality,@Type,@State,@Source,@Channel,@Active);",p);
                await w.Execute("UPDATE com.SegmentosProspecto SET Nombre=@Name,Departamento=@Department,Municipio=@Municipality,TipoProspecto=@Type,EstadoProspecto=@State,TipoFuente=@Source,Canal=@Channel,Activo=@Active WHERE IdSegmento=@Id;",p);return r.Id;
            }
            case SaveOnlineTemplate r:
                if(r.Id>0)Version(await w.Scalar<byte[]?>("SELECT RowVersion FROM com.PlantillasMensaje WITH(UPDLOCK,HOLDLOCK) WHERE IdPlantilla=@Id;",r),r.ExpectedVersion);
                if(r.Id==0)return await w.Scalar<long>("INSERT INTO com.PlantillasMensaje(Nombre,Canal,Contenido,Activo) OUTPUT INSERTED.IdPlantilla VALUES(@Name,@Channel,@Content,@Active);",r);
                await w.Execute("UPDATE com.PlantillasMensaje SET Nombre=@Name,Canal=@Channel,Contenido=@Content,Activo=@Active WHERE IdPlantilla=@Id;",r);return r.Id;
            case CreateOnlineCampaign r:
            {
                var s=await w.Maybe<OnlineSegment>($"SELECT {SegmentColumns} FROM com.SegmentosProspecto WITH(HOLDLOCK) WHERE IdSegmento=@SegmentId;",r);
                var t=await w.Maybe<OnlineTemplate>($"SELECT {TemplateColumns} FROM com.PlantillasMensaje WITH(HOLDLOCK) WHERE IdPlantilla=@TemplateId;",r);
                Version(s?.RowVersion,r.SegmentVersion);Version(t?.RowVersion,r.TemplateVersion);
                Require(s!.Active&&t!.Active&&s.Channel==t.Channel,"INVALID_STATE","Selecciona segmento y plantilla activos del mismo canal.");
                Require(OnlineValidator.FilterError(s!.Filter) is null&&OnlineValidator.TemplateError(t!.Content) is null,"VALIDATION","Revisa segmento y contenido de la plantilla.");
                return await w.Scalar<long>("INSERT INTO com.Campanias(Nombre,Canal,IdPlantilla,IdSegmento,CriteriosJson,ContenidoSnapshot) OUTPUT INSERTED.IdCampania VALUES(@Name,@Channel,@TemplateId,@SegmentId,@criteria,@Content);",new{r.Name,s.Channel,r.TemplateId,r.SegmentId,criteria=Json(s!.Filter),t!.Content});
            }
            case TransitionOnlineCampaign r:return await TransitionCampaign(w,r,user);
            case RecordOnlineFollowUp r:
            {
                var p=await OnlineCore(w,r.ProspectId,true);Version(p?.RowVersion,r.ExpectedVersion);
                Require(p!.State!="CONVERTIDO","INVALID_STATE","Registra la gestión del cliente convertido desde su módulo.");
                Require(r.Result!="CONTACTADO"||!p.Suppressed,"INVALID_STATE","No se registra contacto saliente con un prospecto excluido.");
                var today=await w.Scalar<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');");
                Require(r.Date.Date<=today,"VALIDATION","La interacción no puede ser futura.");
                var date=new DateTimeOffset(r.Date.Date,TimeSpan.FromHours(-6));
                await w.Execute("INSERT INTO com.SeguimientosProspecto(IdProspecto,IdUsuario,Fecha,ProximaGestion,Canal,Resultado,Nota) VALUES(@ProspectId,@user,@date,@NextDate,@Channel,@Result,@Note);",new{r.ProspectId,user,date,r.NextDate,r.Channel,r.Result,r.Note});
                var state=r.Result switch{"CONTACTADO" or "RESPONDIO"=>p.State=="INTERESADO"?"INTERESADO":"CONTACTADO","INTERESADO"=>"INTERESADO","SIN_INTERES"=>"DESCARTADO",_=>p.State};
                await w.Execute("UPDATE com.Prospectos SET Estado=@state WHERE IdProspecto=@ProspectId;",new{state,r.ProspectId});return r.ProspectId;
            }
            default:throw new ArgumentException("Operación de planificación desconocida.");
        }
    }
    private static async Task<long> TransitionCampaign(Work w,TransitionOnlineCampaign r,int user)
    {
        var c=await w.Maybe<OnlineCampaign>("SELECT IdCampania Id,Canal Channel,Estado State,CriteriosJson CriteriaJson,ContenidoSnapshot Content,RowVersion FROM com.Campanias WITH(UPDLOCK,HOLDLOCK) WHERE IdCampania=@Id;",r);
        Version(c?.RowVersion,r.ExpectedVersion);Require(c!.Internal,"INVALID_STATE","Campaña histórica sin preparación interna: conserva su historia y crea una nueva.");
        if(r.Action=="PREPARAR")
        {
            Require(c.State=="BORRADOR","INVALID_STATE","Sólo un borrador se prepara para revisión.");
            var filter=JsonSerializer.Deserialize<OnlineSegmentFilter>(c.CriteriaJson!);Require(filter is not null&&OnlineValidator.FilterError(filter) is null&&filter.Channel==c.Channel,"INVALID_STATE","Criterios de campaña no válidos.");
            var candidates=await w.List<OnlineProspectData>($"SELECT TOP(1001) {OnlineColumns} FROM {OnlineFrom} WHERE {SegmentWhere} ORDER BY p.IdProspecto;",SegmentParams(filter!,new()));
            Require(candidates.Count<=1000,"VALIDATION","Acota el segmento a un máximo de 1000 candidatos antes de preparar.");
            var recipients=candidates.Select(p=>(Prospect:p,Contact:ContactFor(p,c.Channel))).Where(p=>p.Contact is not null).ToArray();
            Require(recipients.Length>0,"INVALID_STATE","No hay prospectos con contacto válido y sin exclusión para esta campaña.");
            await w.Execute("DELETE FROM com.CampaniaDestinatarios WHERE IdCampania=@Id;",r);
            foreach(var (p,contact) in recipients)await w.Execute("INSERT INTO com.CampaniaDestinatarios(IdCampania,IdProspecto,ContactoSnapshot,VersionProspecto) VALUES(@Id,@prospectId,@contact,@RowVersion);",new{r.Id,prospectId=p.Id,contact,p.RowVersion});
            await w.Execute("UPDATE com.Campanias SET Estado=N'PENDIENTE_APROBACION',AprobadaPor=NULL,FechaAprobacion=NULL WHERE IdCampania=@Id;",r);
        }
        else if(r.Action=="APROBAR")
        {
            Require(c.State=="PENDIENTE_APROBACION","INVALID_STATE","Prepara destinatarios antes de aprobar.");
            var eligible=await w.Scalar<int>($"SELECT COUNT(*) FROM com.CampaniaDestinatarios d JOIN com.Prospectos p ON p.IdProspecto=d.IdProspecto WHERE d.IdCampania=@Id AND NOT({CampaignSuppressed});",r);
            Require(eligible>0,"INVALID_STATE","Todos los destinatarios cambiaron o están excluidos. Devuelve y prepara otra vez.");
            await w.Execute("UPDATE com.Campanias SET Estado=N'APROBADA',AprobadaPor=@user,FechaAprobacion=SYSDATETIMEOFFSET() WHERE IdCampania=@Id;",new{r.Id,user});
        }
        else if(r.Action=="DEVOLVER")
        {
            Require(c.State is "PENDIENTE_APROBACION" or "APROBADA","INVALID_STATE","Sólo se devuelve una campaña preparada o aprobada.");
            await w.Execute("DELETE FROM com.CampaniaDestinatarios WHERE IdCampania=@Id;UPDATE com.Campanias SET Estado=N'BORRADOR',AprobadaPor=NULL,FechaAprobacion=NULL WHERE IdCampania=@Id;",r);
        }
        else
        {
            Require(c.State is "BORRADOR" or "PENDIENTE_APROBACION" or "APROBADA","INVALID_STATE","Esta campaña ya es terminal o está en ejecución externa.");
            await w.Execute("UPDATE com.Campanias SET Estado=N'CANCELADA' WHERE IdCampania=@Id;",r);
        }
        return r.Id;
    }
    private static string? ContactFor(OnlineProspectData p,string channel)
    {
        var value=channel switch{"WHATSAPP"=>p.WhatsApp,"EMAIL"=>p.Email,"INSTAGRAM"=>p.Instagram,"FACEBOOK"=>p.Facebook,_=>null};
        return value is null?null:OnlineValidator.Identifier(channel,value);
    }
}
