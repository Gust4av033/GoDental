using System.IO;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
// Datos sintéticos, exclusivamente para pruebas.
internal sealed class CollectionUiRepository : ICollectionRepository
{
    public List<RegisterCollectionNotice> Writes { get; } = [];
    public Task<CustomerStatement?>? StatementRead { get; set; }
    public Task<PaymentReceiptData?>? PaymentRead { get; set; }
    public bool LoseResponse { get; set; }
    public CreditAccountData Account { get; } = new() { Id=1, CustomerId=1, Customer="Cliente de prueba", Sale="PRUEBA-001", Due=DateTime.Today, Original=50, Balance=25, State="PARCIAL", NoticeDue=true, RowVersion=new byte[8] };
    public CustomerStatement Statement => new(1,"CLI-PRUEBA","Cliente de prueba",DateTimeOffset.UtcNow,[Account],[
        new(){ Id=1, AccountId=1, Date=DateTimeOffset.UtcNow.AddDays(-2),Type="CARGO", Document="PRUEBA-001",Sale="PRUEBA-001",Delta=50,RunningBalance=50 },
        new(){ Id=1, AccountId=1, Date=DateTimeOffset.UtcNow.AddDays(-1),Type="ABONO",Document="REC-PRUEBA",Sale="PRUEBA-001",Delta=-10,RunningBalance=40 },
        new(){ Id=1, AccountId=1, Date=DateTimeOffset.UtcNow,Type="DEVOLUCION",Document="DEV-PRUEBA",Sale="PRUEBA-001",Delta=-15,CashRefund=5,RunningBalance=25 }]);
    public PaymentReceiptData Payment => new(){Id=1,CustomerId=1,AccountId=1,Customer="Cliente de prueba",Sale="PRUEBA-001",Number="REC-PRUEBA",Date=DateTimeOffset.UtcNow,AsOf=DateTimeOffset.UtcNow,Amount=10,Method="Transferencia",User="Usuario de prueba",BalanceBefore=50,BalanceAfter=40,CurrentBalance=25};
    public Task<MvpPage<CreditAccountData>> DueAccountsAsync(bool overdueOnly,MvpFilter f,int user,CancellationToken ct) => Task.FromResult(new MvpPage<CreditAccountData>([Account],1));
    public Task<Result<MvpReceipt>> RegisterNoticeAsync(RegisterCollectionNotice r,int user,int? station,CancellationToken ct) { Writes.Add(r); if(LoseResponse)throw new IOException("Prueba: respuesta perdida");return Task.FromResult(Result<MvpReceipt>.Success(new(1,r.OperationId))); }
    public Task<MvpPage<CollectionNoticeData>> NoticesAsync(long account,MvpFilter f,int user,CancellationToken ct) => Task.FromResult(new MvpPage<CollectionNoticeData>([new(){Id=1,AccountId=account,Date=DateTimeOffset.UtcNow,User="Usuario de prueba",Balance=25,Due=DateTime.Today,NextNotice=DateTime.Today.AddDays(5),Medium="INTERNO",Outcome="GESTION_INTERNA",Reason="Seguimiento de prueba"}],1));
    public Task<CustomerStatement?> StatementAsync(long customer,bool forPrint,int user,CancellationToken ct) => StatementRead ?? Task.FromResult<CustomerStatement?>(Statement);
    public Task<PaymentReceiptData?> PaymentReceiptAsync(long payment,int user,CancellationToken ct) => PaymentRead ?? Task.FromResult<PaymentReceiptData?>(Payment);
}
