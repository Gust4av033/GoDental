using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
namespace GoDental.Infrastructure.Operations;
public sealed partial class SqlMvpRepository
{
    private const string FiscalColumns="f.IdDocumentoFiscal Id,f.IdVenta SaleId,v.NumeroVenta SaleNumber,f.TipoDocumento Type,f.Estado State,f.Fecha Created,f.RevisionInterna ReviewState,f.ObservacionRevision Observation,f.FechaRevision Reviewed,u.NombreCompleto Reviewer,f.HuellaRevision ReviewedFingerprint,f.RowVersion";
    private const string FiscalFrom="fis.DocumentosFiscales f JOIN ven.Ventas v ON v.IdVenta=f.IdVenta LEFT JOIN seg.Usuarios u ON u.IdUsuario=f.RevisadoPor";
    public Task<MvpPage<FiscalDocumentData>> FiscalDocumentsAsync(string search,string? state,MvpFilter f,int userId,CancellationToken ct)=>Read(userId,["FISCAL.VER","VENTAS.VER"],w=>Page<FiscalDocumentData>(w,FiscalFrom,FiscalColumns,"(@state IS NULL OR f.RevisionInterna=@state) AND (@search=N'' OR CHARINDEX(@search,v.NumeroVenta)>0 OR CONVERT(nvarchar(30),f.IdDocumentoFiscal)=@search)","f.IdDocumentoFiscal DESC",new{search,state,Offset=checked((f.Page-1)*f.PageSize),f.PageSize}),ct);
    public Task<MvpPage<SaleData>> FiscalCandidatesAsync(string search,MvpFilter f,int userId,CancellationToken ct)=>Read(userId,["FISCAL.VER","VENTAS.VER"],w=>Page<SaleData>(w,"ven.Ventas",SaleColumns,"Estado=N'CONFIRMADA' AND (@search=N'' OR CHARINDEX(@search,NumeroVenta)>0 OR CONVERT(nvarchar(30),IdVenta)=@search) AND NOT EXISTS(SELECT 1 FROM fis.DocumentosFiscales WHERE IdVenta=ven.Ventas.IdVenta AND Estado<>N'ANULADO')","IdVenta DESC",new{search,Offset=checked((f.Page-1)*f.PageSize),f.PageSize}),ct);
    public Task<FiscalDetail?> FiscalDetailAsync(long id,int userId,CancellationToken ct)=>Read(userId,["FISCAL.VER","VENTAS.VER"],w=>FiscalDetailCore(w,id),ct);
    private static async Task<FiscalDetail?> FiscalDetailCore(Work w,long id)
    {
        // La referencia venta es inmutable; liberar la lectura de localización antes de bloquear
        // venta y documento en el orden de registro/posventa, también al consultar la ficha.
        var saleId=await w.Scalar<long?>("SELECT IdVenta FROM fis.DocumentosFiscales WITH(READCOMMITTED) WHERE IdDocumentoFiscal=@id;",new{id});
        if(saleId is null)return null;
        var sale=await w.One<SaleData>($"SELECT {SaleColumns} FROM ven.Ventas WITH(UPDLOCK,HOLDLOCK) WHERE IdVenta=@saleId;",new{saleId});
        var document=await w.Maybe<FiscalDocumentData>($"SELECT {FiscalColumns} FROM fis.DocumentosFiscales f WITH(UPDLOCK,HOLDLOCK) JOIN ven.Ventas v ON v.IdVenta=f.IdVenta LEFT JOIN seg.Usuarios u ON u.IdUsuario=f.RevisadoPor WHERE f.IdDocumentoFiscal=@id;",new{id});
        if(document is null)return null;
        var receiver=await w.Maybe<FiscalReceiverData>("""
            SELECT c.IdCliente Id,c.NombreRazonSocial Name,c.NIT,c.NRC,c.DUI,c.Activo Active,
             (SELECT TOP(1) d.Direccion FROM cli.ClienteDirecciones d WITH(HOLDLOCK) WHERE d.IdCliente=c.IdCliente AND d.Activo=1 AND d.Tipo=N'FISCAL' ORDER BY d.EsPrincipal DESC,d.IdDireccion) Address
            FROM cli.Clientes c WITH(HOLDLOCK) WHERE c.IdCliente=@CustomerId;
            """,sale)??new FiscalReceiverData{Name="Consumidor final"};
        var lines=await w.List<SaleLineData>("SELECT TOP(5001) IdDetalle Id,IdProducto ProductId,IdLote LotId,SkuSnapshot Sku,DescripcionSnapshot Name,Cantidad Quantity,PrecioUnitario UnitPrice,DescuentoMonto Discount,ImpuestoMonto Tax,TotalLinea Total FROM ven.VentaDetalle WITH(HOLDLOCK) WHERE IdVenta=@saleId ORDER BY IdDetalle;",new{saleId});
        Require(lines.Count<=5000,"VALIDATION","La venta supera el límite de revisión interna; requiere revisión de mayor volumen.");
        var dtes=await w.Scalar<int>("SELECT COUNT(*) FROM fis.DocumentosDte WITH(HOLDLOCK) WHERE IdDocumentoFiscal=@id;",new{id});
        var fingerprint=HashToken(new{document.SaleId,document.Type,document.State,sale,receiver,lines,dtes});
        return new(document,sale,receiver,lines,dtes,fingerprint,HashToken(new{document.RowVersion,fingerprint}));
    }
    public Task<MvpPage<FiscalReviewEvent>> FiscalHistoryAsync(long id,MvpFilter f,int userId,CancellationToken ct)=>Read(userId,["FISCAL.VER","VENTAS.VER"],w=>Page<FiscalReviewEvent>(w,"sis.Bitacora b JOIN seg.Usuarios u ON u.IdUsuario=b.IdUsuario","b.Fecha Date,u.NombreCompleto [User],JSON_VALUE(b.DatosDespuesJson,'$.Outcome') Outcome,JSON_VALUE(b.DatosDespuesJson,'$.Reason') Reason","b.Accion=N'REVISAR_FISCAL_INTERNO' AND b.EntidadId=@id","b.IdBitacora DESC",new{id,Offset=checked((f.Page-1)*f.PageSize),f.PageSize}),ct);
    public Task<Result<MvpReceipt>> ReviewFiscalAsync(ReviewFiscalInternal request,int userId,int? station,CancellationToken ct)
    {
        if(FiscalWorkspaceService.Validate(request) is {} error)return Task.FromResult(Result<MvpReceipt>.Failure("VALIDATION",error));
        var r=FiscalWorkspaceService.Freeze(request);
        return Write(r.OperationId,"REVISAR_FISCAL_INTERNO",r,userId,station,["FISCAL.VER","VENTAS.VER","FISCAL.REVISAR_INTERNO"],async w=>
        {
            await Station(w,station);var detail=await FiscalDetailCore(w,r.DocumentId);
            Require(detail is not null,"INVALID_STATE","El documento no existe.");Version(detail!.Document.RowVersion,r.ExpectedVersion);
            Require(detail.ReviewToken==r.ReviewToken,"CONCURRENCY_CONFLICT","Cambió la venta o el receptor. Actualiza la ficha y revisa nuevamente.");
            Require(detail.Document.State=="PENDIENTE"&&detail.DteCount==0,"INVALID_STATE","La revisión interna sólo corresponde a solicitudes pendientes sin DTE asociado.");
            Require(r.Outcome!="REVISADO"||detail.Issues.Count==0,"INVALID_STATE",string.Join(" ",detail.Issues));
            await w.Execute("UPDATE fis.DocumentosFiscales SET RevisionInterna=@Outcome,ObservacionRevision=@Reason,FechaRevision=SYSDATETIMEOFFSET(),RevisadoPor=@userId,HuellaRevision=@Fingerprint WHERE IdDocumentoFiscal=@DocumentId;",new{r.Outcome,r.Reason,userId,detail.Fingerprint,r.DocumentId});
            return r.DocumentId;
        },ct);
    }
}
