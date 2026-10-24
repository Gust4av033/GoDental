using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Infrastructure.Operations;
using Microsoft.Extensions.Options;
namespace GoDental.Tests.Common;
public sealed class CollectionTests
{
    private static UserSession Session(params string[] permissions)
    { var s=new UserSession();s.Begin(new(1,"test","Prueba",1,[],permissions,false));return s; }
    [Fact] public void NoticeIdentityIsStableAfterTrimmingAndIncludesVersion()
    {
        var r=new RegisterCollectionNotice(Guid.NewGuid(),1,new byte[8],"INTERNO","GESTION_INTERNA"," Motivo ");
        var ticket=OperationIdentity.Describe(r);Assert.True(OperationIdentity.IsValid(ticket));Assert.Equal("AVISO_COBRANZA",ticket.Type);
        Assert.Equal(ticket,OperationIdentity.Describe(r with{Reason="Motivo"}));
        var frozen=CollectionValidator.Freeze(r);r.ExpectedVersion[0]=1;Assert.Equal(0,frozen.ExpectedVersion[0]);
        Assert.NotEqual(ticket,OperationIdentity.Describe(r));
    }
    [Theory][InlineData("SMS","CONTACTADO")][InlineData("EMAIL","ENVIADO_AUTOMATICAMENTE")]
    public void RejectsUnknownMediumAndOutcome(string medium,string outcome) => Assert.NotNull(CollectionValidator.Validate(new(Guid.NewGuid(),1,new byte[8],medium,outcome,"Observación")));
    [Fact] public async Task ReadDoesNotGrantManageOrPrint()
    {
        var repo=new CollectionUiRepository();var s=Session("CLIENTES.VER","VENTAS.VER");var svc=new CollectionService(repo,new Renderer(),s);
        Assert.NotNull(await svc.StatementAsync(1));Assert.False(svc.CanManage);Assert.False(svc.CanPrint);
        Assert.Equal("FORBIDDEN",(await svc.RegisterAsync(new(Guid.NewGuid(),1,new byte[8],"INTERNO","GESTION_INTERNA","Observación"))).ErrorCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>svc.PreviewStatementAsync(1,PrintLayout.Letter));Assert.Empty(repo.Writes);
    }
    [Fact] public async Task SessionChangeDuringRenderDiscardsTheDocument()
    {
        var s=Session("CLIENTES.VER","VENTAS.VER","VENTAS.REIMPRIMIR");var svc=new CollectionService(new CollectionUiRepository(),new Renderer{DuringRender=s.Clear},s);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>svc.PreviewPaymentAsync(1,PrintLayout.Letter));
    }
    [Fact] public void StatementSeparatesCashRefundAndFlagsIncompleteHistory()
    {
        var statement=new CollectionUiRepository().Statement;Assert.Equal(25,statement.CalculatedBalance);Assert.Equal(0,statement.Difference);
        Assert.Equal(5,statement.Movements.Sum(m=>m.CashRefund));statement.Accounts[0].Balance=24;Assert.Equal(-1,statement.Difference);
        statement.Accounts[0].State="CASTIGADA";Assert.Equal(0,statement.CollectibleBalance);Assert.Equal(24,statement.RecordedBalance);
    }
    [Theory][InlineData(215.9,279.4,11.3)][InlineData(210,297,11.3)][InlineData(80,250,3)][InlineData(58,250,3)]
    public void CreditDocumentsAreRealPdfAndPreviewInConfigurableSizes(double width,double height,double margin)
    {
        var r=new CreditDocumentRenderer(Options.Create(new ReceiptPdfOptions()));var source=new CollectionUiRepository();var layout=new PrintLayout((decimal)width,(decimal)height,(decimal)margin);
        foreach(var doc in new[]{r.RenderStatement(source.Statement,layout),r.RenderPayment(source.Payment,layout)})
        {Assert.Equal("%PDF-",System.Text.Encoding.ASCII.GetString(doc.Pdf,0,5));Assert.NotEmpty(doc.Pages);Assert.Equal(layout.WidthMm,doc.WidthMm);Assert.All(doc.Pages,p=>Assert.True(p.Length>1000));}
    }
    [Fact] public void LegacyReceiptAndUnreconciledStatementRenderWithoutFabricatedBalances()
    {
        var source=new CollectionUiRepository();var payment=source.Payment;payment.BalanceBefore=payment.BalanceAfter=null;payment.HistoricalDetailsMissing=true;payment.Voided=true;
        var r=new CreditDocumentRenderer(Options.Create(new ReceiptPdfOptions()));Assert.NotEmpty(r.RenderPayment(payment,PrintLayout.Letter).Pdf);
        var statement=source.Statement;statement.Accounts[0].Balance=30;Assert.Equal(5,statement.Difference);Assert.NotEmpty(r.RenderStatement(statement,PrintLayout.Letter).Pages);
        Assert.Null(payment.BalanceBefore);Assert.Null(payment.BalanceAfter);
    }
    [Fact] public void LongStatementPaginatesWithoutTruncatingSource()
    {
        var source=new CollectionUiRepository();var rows=Enumerable.Range(1,80).Select(i=>new CreditMovementData{Id=i,Date=DateTimeOffset.UtcNow,Type="ABONO",Document="REC-"+i,Sale="VENTA-TEST",Delta=-1,RunningBalance=100-i}).ToArray();
        var statement=source.Statement with{Movements=rows};var doc=new CreditDocumentRenderer(Options.Create(new ReceiptPdfOptions())).RenderStatement(statement,PrintLayout.Letter);
        Assert.True(doc.Pages.Count>1);Assert.Equal(80,statement.Movements.Count);
    }
    private sealed class Renderer : ICreditDocumentRenderer
    {
        public Action? DuringRender;
        public ReceiptDocument RenderPayment(PaymentReceiptData r,PrintLayout l){DuringRender?.Invoke();return new([1],[[1]],l.WidthMm,l.HeightMm,l.MarginMm);}
        public ReceiptDocument RenderStatement(CustomerStatement r,PrintLayout l){DuringRender?.Invoke();return new([1],[[1]],l.WidthMm,l.HeightMm,l.MarginMm);}
    }
}
