using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoDental.Tests.Ui;

public sealed class AccessUiTests
{
    [Theory][InlineData("", "frase sintética")][InlineData("   ", "frase sintética")][InlineData("usuario", "")]
    public async Task EmptyInputsNeverConsumeGatewayAttempts(string user,string password)
    {
        var gateway=new Gateway();var session=new UserSession();var vm=new AccessViewModel(gateway,session,NullLogger<AccessViewModel>.Instance);await vm.PrepareCommand.ExecuteAsync(null);vm.UserName=user;
        await vm.SubmitAsync(password,"","");Assert.Equal(0,gateway.SignIns);Assert.False(session.IsAuthenticated);Assert.Contains("Escribe",vm.StatusMessage);
    }
    [Fact]public async Task BootstrapRequiresNameAndMatchingConfirmationBeforeGateway()
    {
        var gateway=new Gateway{Bootstrap=true};var session=new UserSession();var vm=new AccessViewModel(gateway,session,NullLogger<AccessViewModel>.Instance);await vm.PrepareCommand.ExecuteAsync(null);vm.UserName="administrador";
        await vm.SubmitAsync("frase sintética","frase sintética","");Assert.Equal(0,gateway.Creates);vm.DisplayName="Persona sintética";
        await vm.SubmitAsync("frase sintética","otra","");Assert.Equal(0,gateway.Creates);Assert.Contains("no coinciden",vm.StatusMessage);
    }
    [Fact]public async Task ReturningToSignInCannotBypassMandatoryPasswordChange()
    {
        var gateway=new Gateway{ForceChange=true};var session=new UserSession();var vm=new AccessViewModel(gateway,session,NullLogger<AccessViewModel>.Instance);await vm.PrepareCommand.ExecuteAsync(null);vm.UserName="usuario";
        await vm.SubmitAsync("temporal sintética","","");Assert.True(vm.MustChangePassword);Assert.False(session.IsAuthenticated);
        await vm.SubmitAsync("nueva sintética","nueva sintética","");Assert.Equal(0,gateway.Changes);
        await vm.CancelPasswordChangeCommand.ExecuteAsync(null);Assert.False(vm.MustChangePassword);Assert.False(session.IsAuthenticated);Assert.True(vm.IsReady);
        await vm.SubmitAsync("temporal sintética","","");Assert.True(vm.MustChangePassword);Assert.False(session.IsAuthenticated);Assert.Equal(2,gateway.SignIns);
    }
    [Fact]public async Task PendingAccessRejectsDoubleSubmitAndCannotBeCancelledIntoAnotherIdentity()
    {
        var pending=new TaskCompletionSource<Result<UserIdentity>>(TaskCreationOptions.RunContinuationsAsynchronously);var gateway=new Gateway{Pending=pending.Task};var session=new UserSession();var vm=new AccessViewModel(gateway,session,NullLogger<AccessViewModel>.Instance);await vm.PrepareCommand.ExecuteAsync(null);vm.UserName="usuario";
        var task=vm.SubmitAsync("frase sintética","","");Assert.True(vm.IsSubmitting);Assert.Contains("Procesando",vm.PrimaryActionLabel);
        await vm.SubmitAsync("otra frase","","");await vm.CancelPasswordChangeCommand.ExecuteAsync(null);Assert.Equal(1,gateway.SignIns);Assert.True(vm.IsBusy);
        pending.SetResult(Result<UserIdentity>.Failure("INVALID_CREDENTIALS","Acceso rechazado."));await task;Assert.False(vm.IsSubmitting);Assert.False(vm.IsBusy);Assert.False(session.IsAuthenticated);
    }
    private sealed class Gateway:IIdentityGateway
    {
        public bool Bootstrap,ForceChange;public int SignIns,Creates,Changes;public Task<Result<UserIdentity>>? Pending;
        public Task<bool> NeedsAdministratorAsync(CancellationToken ct=default)=>Task.FromResult(Bootstrap);
        public Task<Result<UserIdentity>> SignInAsync(string u,string p,string machine,CancellationToken ct=default){SignIns++;return Pending??Task.FromResult(Result<UserIdentity>.Success(new(1,u,"Persona sintética",1,[],[],ForceChange)));}
        public Task<Result<UserIdentity>> CreateAdministratorAsync(string u,string n,string p,string machine,CancellationToken ct=default){Creates++;return Task.FromResult(Result<UserIdentity>.Success(new(1,u,n,1,[],[],false)));}
        public Task<Result> ChangePasswordAsync(int id,string current,string next,CancellationToken ct=default){Changes++;return Task.FromResult(Result.Success());}
        public Task SignOutAsync(int id,int? workstation,CancellationToken ct=default)=>Task.CompletedTask;
    }
}
