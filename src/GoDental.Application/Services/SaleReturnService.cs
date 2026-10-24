using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Application.Services;
public sealed class SaleReturnService(ISaleReturnRepository repository,IUserSession session,IIdentityGateway identity,IReturnEvidenceStore? evidenceStore=null):MvpServiceAccess(session)
{
    public bool CanConfigure=>Has("VENTAS.VER","AJUSTES.EDITAR")&&CurrentSession.Roles.Contains("Administrador");
    public bool CanRequest=>Has("VENTAS.VER","VENTAS.DEVOLUCION_SOLICITAR");
    public bool CanResolve=>Has("VENTAS.VER","VENTAS.DEVOLUCION_SOLICITAR");
    public bool CanCancel=>CanRequest&&Has("VENTAS.ANULAR");
    public Task<ReturnEvidenceLocation> EvidenceLocationAsync(CancellationToken ct=default)
    {Read("VENTAS.VER","AJUSTES.EDITAR");return repository.ReadEvidenceLocationAsync(UserId,ct);}
    public async Task<Result<MvpReceipt>> ConfigureAsync(ConfigureReturnEvidence request,CancellationToken ct=default)
    {
        if(!CanConfigure)return Result<MvpReceipt>.Failure("FORBIDDEN","Sólo el administrador puede configurar la carpeta central.");
        if(SaleReturnValidator.Validate(request) is {} error)return Result<MvpReceipt>.Failure("VALIDATION",error);
        if(evidenceStore is null)return Result<MvpReceipt>.Failure("INVALID_STATE","El almacenamiento de evidencias no está disponible.");
        var revision=CurrentSession is ISessionController s?s.Revision:0;var userId=UserId;var station=WorkstationId;
        try{await evidenceStore.CheckRootAsync(request.Root,ct);}
        catch(ArgumentException ex){return Result<MvpReceipt>.Failure("VALIDATION",ex.Message);}
        catch(Exception ex)when(ex is System.IO.IOException or UnauthorizedAccessException){return Result<MvpReceipt>.Failure("INVALID_STATE","No se pudo escribir en la carpeta central. Revisa la ruta compartida y los permisos de Windows.");}
        if(!CanConfigure || CurrentSession is ISessionController current&&current.Revision!=revision)return Result<MvpReceipt>.Failure("FORBIDDEN","La sesión cambió al configurar evidencias.");
        return await repository.ConfigureEvidenceAsync(request with{Root=request.Root.Trim(),ExpectedVersion=request.ExpectedVersion.ToArray(),Reason=request.Reason.Trim()},userId,station,ct);
    }
    public async Task<ReturnEvidence> UploadPhotoAsync(byte[] image,string name,CancellationToken ct=default)
    {
        Read("VENTAS.VER","VENTAS.DEVOLUCION_SOLICITAR");
        if(evidenceStore is null)throw new ArgumentException("Configura el almacenamiento central de evidencias antes de adjuntar fotos.");
        var revision=CurrentSession is ISessionController s?s.Revision:0;
        var result=await evidenceStore.SaveAsync(image,name,UserId,ct);
        if(!CanRequest || CurrentSession is ISessionController current&&current.Revision!=revision)throw new UnauthorizedAccessException("La sesión cambió al adjuntar evidencia.");
        return result;
    }
    public async Task<byte[]> PhotoAsync(long saleId,long returnId,long lineId,int photo,CancellationToken ct=default)
    {
        var revision=CurrentSession is ISessionController s?s.Revision:0;
        var detail=await DetailAsync(saleId,returnId,ct);
        var line=detail?.Lines.SingleOrDefault(l=>l.Id==lineId);
        if(evidenceStore is null||line is null||photo<0||photo>=line.Evidence.Count)throw new ArgumentException("La foto no está disponible.");
        byte[] result;
        try{result=await evidenceStore.ReadAsync(line.Evidence[photo],ct);}
        catch(Exception ex)when(ex is System.IO.IOException or UnauthorizedAccessException){throw new ArgumentException("La foto no está disponible o no coincide con el archivo registrado. Revisa la carpeta central o su respaldo.");}
        Read("VENTAS.VER");
        if(CurrentSession is ISessionController current&&current.Revision!=revision)throw new UnauthorizedAccessException("La sesión cambió al consultar evidencia.");
        return result;
    }
    public Task<SaleReturnsSnapshot> ReadAsync(long saleId,int page=1,CancellationToken ct=default)
    {Read("VENTAS.VER");Id(saleId);var filter=new MvpFilter(page);Page(filter);return repository.ReadReturnsAsync(saleId,filter,UserId,ct);}
    public Task<SaleReturnSnapshot?> DetailAsync(long saleId,long returnId,CancellationToken ct=default)
    {Read("VENTAS.VER");Id(saleId);Id(returnId);return repository.ReadReturnAsync(saleId,returnId,UserId,ct);}
    public Task<Result<MvpReceipt>> RequestAsync(RequestSaleReturn r,CancellationToken ct=default)
    {
        if(!CanRequest || r.CancelSale&&!CanCancel)return Task.FromResult(Result<MvpReceipt>.Failure("FORBIDDEN","No tienes permiso para solicitar esta operación."));
        return SaleReturnValidator.Validate(r) is {} error?Task.FromResult(Result<MvpReceipt>.Failure("VALIDATION",error)):repository.RequestReturnAsync(SaleReturnValidator.Freeze(r),UserId,WorkstationId,ct);
    }
    public async Task<Result<MvpReceipt>> ResolveAsync(ResolveSaleReturn r,string administrator,string password,CancellationToken ct=default)
    {
        if(!CanResolve)return Result<MvpReceipt>.Failure("FORBIDDEN","No tienes permiso para gestionar esta solicitud.");
        if(SaleReturnValidator.Validate(r) is {} error)return Result<MvpReceipt>.Failure("VALIDATION",error);
        var revision=CurrentSession is ISessionController s?s.Revision:0;
        var verified=await identity.SignInAsync(administrator,password,Environment.MachineName,ct);
        if(!verified.IsSuccess || verified.Value is not {} admin || admin.MustChangePassword || !admin.Roles.Contains("Administrador") || !admin.Permissions.Contains("VENTAS.DEVOLUCION_AUTORIZAR"))
            return Result<MvpReceipt>.Failure("FORBIDDEN","No se pudo autorizar con esas credenciales administrativas.");
        if(CurrentSession is ISessionController current && current.Revision!=revision)return Result<MvpReceipt>.Failure("FORBIDDEN","La sesión cambió durante la autorización.");
        return await repository.ResolveReturnAsync(SaleReturnValidator.Freeze(r),admin.Id,UserId,WorkstationId,ct);
    }
}
