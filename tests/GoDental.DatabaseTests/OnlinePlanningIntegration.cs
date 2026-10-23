using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Operations;
using Microsoft.Data.SqlClient;
internal static class OnlinePlanningIntegration
{
    public static async Task<int> RunAsync(IDbConnectionFactory factory,OnlineService online,SqlMvpRepository repo,UserSession session,string root,string database)
    {
        var checks=0;void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;Console.WriteLine("OK "+name);}
        long Saved(Result<MvpReceipt> r)=>r.IsSuccess?r.Value!.Id:throw new InvalidOperationException(r.ErrorCode+": "+r.Message);
        await using var c=await factory.OpenConnectionAsync();var today=(await c.ExecuteScalarAsync<DateTimeOffset>("SELECT SYSDATETIMEOFFSET();")).ToOffset(TimeSpan.FromHours(-6)).Date;
        var zr=new SaveOnlineZone(Guid.NewGuid(),0,[],"San Salvador","San Salvador Centro",10,true,"Organizar territorio");var zid=Saved(await online.SaveAsync(zr));
        Check(Saved(await online.SaveAsync(zr))==zid&&(await online.ZonesAsync("San Salvador",new())).Total==1,"Zonas guardan y recuperan mismo GUID sin duplicar");
        var z=(await online.ZonesAsync("",new())).Items.Single();Saved(await online.SaveAsync(zr with{OperationId=Guid.NewGuid(),Id=zid,ExpectedVersion=z.RowVersion,Priority=20}));
        Check((await online.SaveAsync(zr with{OperationId=Guid.NewGuid(),Id=zid,ExpectedVersion=z.RowVersion,Priority=30})).ErrorCode=="CONCURRENCY_CONFLICT","Zona rechaza versión anterior entre estaciones");
        Check((await online.SaveAsync(zr with{OperationId=Guid.NewGuid()})).ErrorCode=="DUPLICATE","Zona duplicada requiere mantener la existente");
        var filter=new OnlineSegmentFilter(Type:"NEGOCIO-PLAN",Source:"WEB",Channel:"EMAIL");
        var sr=new SaveOnlineSegment(Guid.NewGuid(),0,[],"Segmento prueba interna",filter,true,"Segmentación pública");var sid=Saved(await online.SaveAsync(sr));
        var seg=(await online.SegmentsAsync("Segmento prueba interna",new())).Items.Single();
        var tr=new SaveOnlineTemplate(Guid.NewGuid(),0,[],"Presentación interna","EMAIL","Hola {nombre}, consulta nuestro catálogo.",true,"Texto revisable");var tid=Saved(await online.SaveAsync(tr));
        var template=(await online.TemplatesAsync("Presentación interna",new())).Items.Single();
        Check(Saved(await online.SaveAsync(tr))==tid&&template.Preview.Contains("Negocio de ejemplo"),"Plantilla reutilizable conserva texto y vista previa sin enviar");
        async Task<long> Prospect(string suffix)=>Saved(await online.SaveAsync(new SaveOnlineProspect(Guid.NewGuid(),0,[],new(){Name="Negocio planificación "+suffix,Type="NEGOCIO-PLAN",Email="plan"+suffix+"@example.com",Department="San Salvador",Municipality="San Salvador Centro"},new("WEB","https://plan.example.com/"+suffix,today,"Contacto sintético"),"Alta para planificación")));
        var p1=await Prospect("uno");var p2=await Prospect("dos");
        var p2data=(await online.ReadAsync(p2))!;Saved(await online.SaveAsync(new BlockOnlineProspect(Guid.NewGuid(),p2,p2data.RowVersion,"Petición de exclusión")));
        Check((await online.PreviewAsync(filter,new())).Items.Single().Id==p1,"Segmentación combina origen y tipo y excluye no contactar");
        Check((await online.ZonesAsync("San Salvador",new())).Items.Single().Prospects>=2,"Zona cuenta prospectos por territorio real");
        Saved(await online.SaveAsync(new AddOnlineSource(Guid.NewGuid(),p1,(await online.ReadAsync(p1))!.RowVersion,new("WEB","https://plan.example.com/otra",today,"Otra referencia"),"Origen adicional")));
        Check((await online.PreviewAsync(filter,new())).Total==1,"Múltiples fuentes coincidentes no duplican destinatario");
        var create=new CreateOnlineCampaign(Guid.NewGuid(),"Campaña interna de prueba",sid,seg.RowVersion,tid,template.RowVersion,"Preparación para revisar");var cid=Saved(await online.SaveAsync(create));
        Check(Saved(await online.SaveAsync(create))==cid,"Campaña BORRADOR reutiliza GUID sin duplicarse");
        var mismatched=Saved(await online.SaveAsync(tr with{OperationId=Guid.NewGuid(),Name="Otro canal",Channel="WHATSAPP"}));var wt=(await online.TemplatesAsync("Otro canal",new())).Items.Single();
        Check((await online.SaveAsync(create with{OperationId=Guid.NewGuid(),TemplateId=mismatched,TemplateVersion=wt.RowVersion})).ErrorCode=="INVALID_STATE","Campaña rechaza mezclar canales de plantilla y segmento");
        Saved(await online.SaveAsync(tr with{OperationId=Guid.NewGuid(),Id=tid,ExpectedVersion=template.RowVersion,Content="Nuevo mensaje {nombre}"}));
        Saved(await online.SaveAsync(sr with{OperationId=Guid.NewGuid(),Id=sid,ExpectedVersion=seg.RowVersion,Filter=filter with{Type="OTRO-NEGOCIO"}}));
        async Task<OnlineCampaign> Campaign()=> (await online.CampaignsAsync("Campaña interna de prueba",new())).Items.Single();
        var campaign=await Campaign();Check(campaign.Content==tr.Content&&campaign.CriteriaJson!.Contains("NEGOCIO-PLAN"),"Campaña congela texto y criterios aunque se editen plantilla o segmento");
        var prepare=new TransitionOnlineCampaign(Guid.NewGuid(),cid,campaign.RowVersion,"PREPARAR","Revisar destinatarios");Saved(await online.SaveAsync(prepare));Saved(await online.SaveAsync(prepare));campaign=await Campaign();
        Check(campaign.State=="PENDIENTE_APROBACION"&&campaign.Recipients==1&&(await online.RecipientsAsync(cid,new())).Items.Single().Contact=="planuno@example.com","Preparar conserva contacto y deduplica destinatarios sin envío");
        Check((await online.SaveAsync(prepare with{OperationId=Guid.NewGuid()})).ErrorCode=="CONCURRENCY_CONFLICT","Dos preparaciones con versión antigua no se pisan");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM com.CampaniaDestinatarios WHERE IdCampania=@cid AND FechaEnvio IS NOT NULL;",new{cid})==0,"Preparación no registra entregas ficticias");
        var p=(await online.ReadAsync(p1))!;var follow=new RecordOnlineFollowUp(Guid.NewGuid(),p1,p.RowVersion,"INTERESADO","EMAIL",today,today.AddDays(2),"Respondió solicitando catálogo fuera de GoDental.","Respuesta manual");
        Saved(await online.SaveAsync(follow));Saved(await online.SaveAsync(follow));
        Check((await online.FollowUpsAsync("",p1,new())).Total==1&&(await online.ReadAsync(p1))!.State=="INTERESADO","Seguimiento inmutable conserva respuesta y estado sin duplicarse al reintentar");
        Check((await online.SaveAsync(follow with{OperationId=Guid.NewGuid()})).ErrorCode=="CONCURRENCY_CONFLICT","Respuesta rechaza edición concurrente de ficha");
        campaign=await Campaign();Check(campaign.Suppressed==1&&(await online.RecipientsAsync(cid,new())).Items.Single().Suppressed,"Ficha modificada requiere preparar destinatarios nuevamente");
        Check((await online.SaveAsync(new TransitionOnlineCampaign(Guid.NewGuid(),cid,campaign.RowVersion,"APROBAR","Versión modificada"))).ErrorCode=="INVALID_STATE","No aprueba cuando ningún destinatario sigue vigente");
        Saved(await online.SaveAsync(new TransitionOnlineCampaign(Guid.NewGuid(),cid,campaign.RowVersion,"DEVOLVER","Revisar otra vez")));campaign=await Campaign();
        Check(campaign.State=="BORRADOR"&&campaign.Recipients==0&&campaign.ApprovedBy is null,"Volver a borrador elimina preparación y aprobación sin borrar auditoría");
        Saved(await online.SaveAsync(new TransitionOnlineCampaign(Guid.NewGuid(),cid,campaign.RowVersion,"PREPARAR","Preparar vigentes")));campaign=await Campaign();
        var approve=new TransitionOnlineCampaign(Guid.NewGuid(),cid,campaign.RowVersion,"APROBAR","Mensaje y contactos revisados");Saved(await online.SaveAsync(approve));Saved(await online.SaveAsync(approve));campaign=await Campaign();
        Check(campaign.State=="APROBADA"&&campaign.ApprovedAt is not null&&campaign.ApprovedBy is not null,"Aprobación humana identifica usuario y fecha con mismo GUID recuperable");
        Saved(await online.SaveAsync(new ExcludeOnlineContact(Guid.NewGuid(),"EMAIL","planuno@example.com","Exclusión posterior")));campaign=await Campaign();
        Check(campaign.Suppressed==1&&(await online.PreviewAsync(filter,new())).Total==0,"Exclusión posterior a aprobación se respeta dinámicamente");
        p=(await online.ReadAsync(p1))!;
        Check((await online.SaveAsync(follow with{OperationId=Guid.NewGuid(),ExpectedVersion=p.RowVersion,Result="CONTACTADO"})).ErrorCode=="INVALID_STATE","No registra contacto saliente a excluido");
        Saved(await online.SaveAsync(follow with{OperationId=Guid.NewGuid(),ExpectedVersion=p.RowVersion,Result="RESPONDIO"}));
        Check((await online.ReadAsync(p1))!.Suppressed&&(await online.FollowUpsAsync("",p1,new(1,1))).Total==2,"Respuesta entrante preserva exclusión y historial pagina");
        Saved(await online.SaveAsync(new TransitionOnlineCampaign(Guid.NewGuid(),cid,campaign.RowVersion,"CANCELAR","Cerrar preparación")));campaign=await Campaign();
        Check(campaign.State=="CANCELADA"&&!(await online.SaveAsync(new TransitionOnlineCampaign(Guid.NewGuid(),cid,campaign.RowVersion,"PREPARAR","Reabrir cancelada"))).IsSuccess,"Cancelación de campaña es terminal");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'COMERCIAL.CAMPANIAS_APROBAR');");
        Check((await online.SaveAsync(new TransitionOnlineCampaign(Guid.NewGuid(),cid,campaign.RowVersion,"APROBAR","Permiso revocado"))).ErrorCode=="FORBIDDEN","SQL exige aprobación vigente independiente de permisos de prospección");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=1 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'COMERCIAL.CAMPANIAS_APROBAR');");
        var version=campaign.RowVersion.ToArray();await DevelopmentDatabase.InstallAsync(database,Path.Combine(root,"database"));
        Check((await Campaign()).RowVersion.SequenceEqual(version)&&(await online.FollowUpsAsync("",p1,new())).Total==2,"Reaplicar 026 conserva versiones campañas y seguimientos");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'COMERCIAL.CAMPANIAS_CREAR');");
        Check((await online.SaveAsync(tr with{OperationId=Guid.NewGuid(),Name="Sin permiso"})).ErrorCode=="FORBIDDEN","SQL revalida permiso de plantilla pese a caché de sesión");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=1 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'COMERCIAL.CAMPANIAS_CREAR');");
        var broken=tr with{OperationId=Guid.NewGuid(),Name="Plantilla rollback"};
        await c.ExecuteAsync("CREATE TRIGGER sis.TR_TestPlanningAudit ON sis.Bitacora AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Accion=N'ONLINE_PLANTILLA') THROW 50991,N'Fallo de auditoría aislado',1; END;");
        var failed=false;try{await online.SaveAsync(broken);}catch(SqlException ex)when(ex.Number==50991){failed=true;}finally{await c.ExecuteAsync("DROP TRIGGER sis.TR_TestPlanningAudit;");}
        Check(failed&&(await online.TemplatesAsync("Plantilla rollback",new())).Total==0&&await repo.FindOperationAsync(broken.OperationId,(int)session.UserId!,default)is null,"Auditoría fallida revierte plantilla y ledger de planificación");
        var ticket=OperationIdentity.Describe(broken);Check((await repo.ResolveOperationAsync(ticket,true,(int)session.UserId!,(int?)session.WorkstationId,default)).State==OperationResolutionState.RejectedDefinitively&&(await online.SaveAsync(broken)).ErrorCode=="OPERATION_REJECTED","Cierre terminal impide confirmar después un borrador ausente");
        return checks;
    }
}
