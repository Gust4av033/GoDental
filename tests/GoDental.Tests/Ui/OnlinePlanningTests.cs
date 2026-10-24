using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.ViewModels.Operations;
using Microsoft.Extensions.Logging.Abstractions;
namespace GoDental.Tests.Ui;
public sealed class OnlinePlanningTests
{
    private static (OnlineViewModel Vm,UserSession Session,OnlineUiRepository Repo,Dialogs Dialogs) Create(params string[] permissions)
    {
        var s=new UserSession();s.Begin(new(1,"test","Prueba",1,[],["COMERCIAL.VER","CLIENTES.VER",..permissions],false));var repo=new OnlineUiRepository();var dialogs=new Dialogs();
        return(new(new(repo,s),dialogs,s,new(new MvpUiRepository(),s),NullLogger<OnlineViewModel>.Instance),s,repo,dialogs);
    }
    [Theory][InlineData("Hola {nombre}",true)][InlineData("Hola {otra}",false)][InlineData("{{nombre}}",false)][InlineData("",false)]
    public void TemplatesOnlyReplaceDocumentedMarker(string text,bool valid)=>Assert.Equal(valid,OnlineValidator.TemplateError(text)is null);
    [Fact]public void InvalidTerritoryStateSourceAndFutureInteractionAreRejected()
    {
        Assert.NotNull(OnlineValidator.FilterError(new("San Salvador","Municipio inventado")));
        Assert.NotNull(OnlineValidator.FilterError(new(State:"CONVERTIDO")));Assert.NotNull(OnlineValidator.FilterError(new(Source:"SCRAPING")));
        Assert.NotNull(OnlineValidator.Validate(new RecordOnlineFollowUp(Guid.NewGuid(),1,new byte[8],"INTERESADO","EMAIL",DateTime.UtcNow.AddDays(3),null,"Interacción","Registro manual")));
    }
    [Fact]public void PlanningIdentitiesFreezeVersionsAndKeepMessageInHash()
    {
        var r=new SaveOnlineTemplate(Guid.NewGuid(),1,new byte[8],"Presentación","EMAIL","Hola {nombre}",true,"Texto preparado");
        var frozen=(SaveOnlineTemplate)OnlineValidator.Freeze(r);var identity=OperationIdentity.Describe(frozen);r.ExpectedVersion[0]=99;
        Assert.Equal(0,frozen.ExpectedVersion[0]);Assert.True(OperationIdentity.IsValid(identity));Assert.Equal("ONLINE_PLANTILLA",identity.Type);
        Assert.NotEqual(identity,OperationIdentity.Describe(frozen with{Content="Mensaje diferente"}));
        var campaign=new CreateOnlineCampaign(Guid.NewGuid(),"Campaña",1,new byte[8],1,new byte[8],"Preparación interna");
        var copy=(CreateOnlineCampaign)OnlineValidator.Freeze(campaign);campaign.SegmentVersion[0]=77;Assert.Equal(0,copy.SegmentVersion[0]);
    }
    [Fact]public async Task ProspectPermissionDoesNotGrantCampaignOrApproval()
    {
        var(vm,s,repo,_)=Create("COMERCIAL.PROSPECTAR");Assert.True(vm.CanManage);Assert.False(vm.CanPlanCampaign);Assert.False(vm.CanApproveCampaign);
        var service=new OnlineService(repo,s);
        Assert.Equal("FORBIDDEN",(await service.SaveAsync(new SaveOnlineTemplate(Guid.NewGuid(),0,[],"Mensaje","EMAIL","Hola",true,"Preparación interna"))).ErrorCode);
        Assert.Equal("FORBIDDEN",(await service.SaveAsync(new TransitionOnlineCampaign(Guid.NewGuid(),1,new byte[8],"APROBAR","Revisión interna"))).ErrorCode);Assert.Empty(repo.Writes);
    }
    [Fact]public async Task ApprovalDoesNotGrantTemplateOrDraftMaintenance()
    {
        var(vm,_,repo,_)=Create("COMERCIAL.CAMPANIAS_APROBAR");Assert.True(vm.CanApproveCampaign);Assert.False(vm.CanPlanCampaign);
        await vm.NewTemplateCommand.ExecuteAsync(null);Assert.Empty(repo.Writes);
    }
    [Fact]public async Task ChangedSegmentAndSessionDropPendingPreviewAndResetPlanningData()
    {
        var(vm,s,repo,_)=Create();await vm.FindSegmentsCommand.ExecuteAsync(null);vm.SelectedSegment=vm.Segments.Single();
        var pending=new TaskCompletionSource<MvpPage<OnlineProspectData>>(TaskCreationOptions.RunContinuationsAsynchronously);repo.PendingPreview=pending.Task;
        var read=vm.FindPreviewCommand.ExecuteAsync(null);vm.SelectedSegment=new(){Id=2,Channel="EMAIL"};pending.SetResult(new([repo.Prospect],1));await read;Assert.Empty(vm.Preview);
        await vm.FindCampaignsCommand.ExecuteAsync(null);vm.SelectedCampaign=vm.Campaigns.Single();await vm.FindRecipientsCommand.ExecuteAsync(null);Assert.Single(vm.Recipients);
        s.Clear();Assert.Empty(vm.Campaigns);Assert.Empty(vm.Segments);Assert.Empty(vm.Recipients);Assert.Null(vm.SelectedCampaign);Assert.False(vm.CanApproveCampaign);
    }
    [Fact]public async Task LostApprovalResponseRetainsGuidAndReviewedVersion()
    {
        var(vm,_,repo,dialogs)=Create("COMERCIAL.CAMPANIAS_APROBAR");await vm.FindCampaignsCommand.ExecuteAsync(null);vm.SelectedCampaign=vm.Campaigns.Single();
        repo.LoseResponse=true;dialogs.Action=async form=>{form.Fields.Single(x=>x.Key=="reason").Value="Contenido revisado";form.Confirmed=true;await ((IAsyncRelayCommand)form.SaveCommand).ExecuteAsync(null);};
        await vm.ApproveCampaignCommand.ExecuteAsync(null);Assert.True(vm.Operation.HasPending);var identity=OperationIdentity.Describe(Assert.Single(repo.Writes));
        vm.SelectedCampaign.RowVersion[0]=42;repo.LoseResponse=false;await vm.Operation.RetryCommand.ExecuteAsync(null);
        Assert.Equal(2,repo.Writes.Count);Assert.All(repo.Writes,r=>Assert.Equal(identity,OperationIdentity.Describe(r)));Assert.False(vm.Operation.HasPending);
    }
    [Fact]public void PlanningDraftKeepsMunicipalityInsideSelectedDepartment()
    {
        var d=new OnlinePlanningDraftModel{IsSegment=true};d.Location.Department="San Salvador";d.Location.Municipality="San Salvador Centro";Assert.Null(OnlineValidator.FilterError(d.Filter()));
        d.Location.Department="La Libertad";Assert.Null(d.Filter().Municipality);Assert.Null(OnlineValidator.FilterError(d.Filter()));
    }
    private sealed class Dialogs:IMvpDialogService {public Func<MvpFormModel,Task>? Action;public async Task<bool> ShowAsync(MvpFormModel f){if(Action is not null)await Action(f);return false;}}
}
