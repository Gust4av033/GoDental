using System.IO;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Wpf.Dialogs;

// Datos controlados exclusivos de pruebas, nunca de la aplicación.
internal sealed class CustomerWorkflowRepository : ICustomerRepository
{
    public CustomerData Customer { get; private set; } = new() { Id = 1, Code = "CLI-PRUEBA", Name = "Cliente de prueba", TypeId = 1, Type = "Clínica", RowVersion = new byte[8] };
    public CustomerCreditData Credit { get; private set; } = new() { Enabled = true, Limit = 100, Days = 15, NoticeDays = 5 };
    public List<CustomerContactData> Contacts { get; } = [new() { Id = 1, Name = "Contacto de prueba", WhatsApp = "00000000" }];
    public List<CustomerAddressData> Addresses { get; } = [new() { Id = 1, Type = "ENTREGA", Address = "Dirección de prueba" }];
    public bool Conflict { get; set; }
    public bool LoseResponse { get; set; }
    public bool DenyRead { get; set; }
    public int Writes { get; private set; }
    public CustomerChange? LastCustomer { get; private set; }
    public CustomerContactChange? LastContact { get; private set; }
    public CustomerAddressChange? LastAddress { get; private set; }
    public CustomerCreditChange? LastCredit { get; private set; }
    public CustomerHistoryFilter? LastHistory { get; private set; }
    public TaskCompletionSource<CustomerSnapshot?>? PendingRead { get; set; }
    public Task<CustomerCatalogs> CatalogsAsync(int u, CancellationToken ct = default) => Task.FromResult(new CustomerCatalogs([new() { Id = 1, Name = "Clínica" }], []));
    public Task<CustomerPage> SearchAsync(CustomerSearch s, int u, CancellationToken ct = default) => Task.FromResult(new CustomerPage(s.IncludeInactive || Customer.Active ? [Customer] : [], s.IncludeInactive || Customer.Active ? 1 : 0));
    public Task<CustomerSnapshot?> ReadAsync(long id, int u, CancellationToken ct = default)
    {
        if (DenyRead) throw new UnauthorizedAccessException();
        return PendingRead?.Task ?? Task.FromResult<CustomerSnapshot?>(new(Customer, Credit, Contacts, Addresses));
    }
    private Result<long> Change()
    {
        Writes++;
        if (Conflict) return Result<long>.Failure("CONCURRENCY_CONFLICT", "Otro equipo modificó el cliente.");
        Customer = Customer with { RowVersion = BitConverter.GetBytes(BitConverter.ToInt64(Customer.RowVersion) + 1) };
        if (LoseResponse) throw new IOException("Respuesta perdida de prueba");
        return Result<long>.Success(Customer.Id);
    }
    public Task<Result<long>> SaveAsync(CustomerChange r, int u, int? w, CancellationToken ct = default)
    {
        LastCustomer = r; var result = Change(); if (result.IsSuccess) Customer = r.Customer with { Id = Customer.Id, RowVersion = Customer.RowVersion }; return Task.FromResult(result);
    }
    public Task<Result<long>> SaveContactAsync(CustomerContactChange r, int u, int? w, CancellationToken ct = default) { LastContact = r; return Task.FromResult(Change()); }
    public Task<Result<long>> SaveAddressAsync(CustomerAddressChange r, int u, int? w, CancellationToken ct = default) { LastAddress = r; return Task.FromResult(Change()); }
    public Task<Result<long>> SaveCreditAsync(CustomerCreditChange r, int u, int? w, CancellationToken ct = default) { LastCredit = r; var result = Change(); if (result.IsSuccess) Credit = r.Credit; return Task.FromResult(result); }
    public Task<CustomerHistoryPage<CustomerSaleData>> SalesAsync(CustomerHistoryFilter f, int u, CancellationToken ct = default)
    {
        LastHistory = f; return Task.FromResult(new CustomerHistoryPage<CustomerSaleData>([new() { Id = 1, Number = "PRUEBA", Date = DateTimeOffset.UtcNow, State = "CONFIRMADA", Total = 20, Pending = 10 }], 31));
    }
    public Task<CustomerHistoryPage<CustomerAccountData>> AccountsAsync(CustomerHistoryFilter f, int u, CancellationToken ct = default) => Task.FromResult(new CustomerHistoryPage<CustomerAccountData>([new() { Id = 1, Sale = "PRUEBA", Issued = DateTime.Today, Due = DateTime.Today.AddDays(15), Original = 20, Balance = 10, State = "PARCIAL" }], 1));
    public Task<CustomerHistoryPage<CustomerPaymentData>> PaymentsAsync(CustomerHistoryFilter f, int u, CancellationToken ct = default) => Task.FromResult(new CustomerHistoryPage<CustomerPaymentData>([new() { Id = 1, AccountId = 1, Receipt = "RECIBO-PRUEBA", Date = DateTimeOffset.UtcNow, Amount = 10, Method = "EFECTIVO" }], 1));
    public Task<CustomerBalanceData?> BalanceAsync(long id, int u, CancellationToken ct = default) => Task.FromResult<CustomerBalanceData?>(new() { Outstanding = 10, Available = 90 });
}
internal sealed class CustomerTestDialogs : ICustomerDialogService
{
    public int Opened { get; private set; }
    public Func<CustomerEditorDialogModel, Task<bool>> Edit { get; set; } = _ => Task.FromResult(false);
    public Task<bool> ShowCustomerEditorAsync(CustomerEditorDialogModel model) { Opened++; return Edit(model); }
}
