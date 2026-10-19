using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;

public interface ICustomerRepository
{
    Task<CustomerPage> SearchAsync(CustomerSearch search, int userId, CancellationToken ct = default);
    Task<CustomerCatalogs> CatalogsAsync(int userId, CancellationToken ct = default);
    Task<CustomerSnapshot?> ReadAsync(long id, int userId, CancellationToken ct = default);
    Task<Result<long>> SaveAsync(CustomerChange request, int userId, int? workstationId, CancellationToken ct = default);
    Task<Result<long>> SaveContactAsync(CustomerContactChange request, int userId, int? workstationId, CancellationToken ct = default);
    Task<Result<long>> SaveAddressAsync(CustomerAddressChange request, int userId, int? workstationId, CancellationToken ct = default);
    Task<Result<long>> SaveCreditAsync(CustomerCreditChange request, int userId, int? workstationId, CancellationToken ct = default);
    Task<CustomerHistoryPage<CustomerSaleData>> SalesAsync(CustomerHistoryFilter filter, int userId, CancellationToken ct = default);
    Task<CustomerHistoryPage<CustomerAccountData>> AccountsAsync(CustomerHistoryFilter filter, int userId, CancellationToken ct = default);
    Task<CustomerHistoryPage<CustomerPaymentData>> PaymentsAsync(CustomerHistoryFilter filter, int userId, CancellationToken ct = default);
    Task<CustomerBalanceData?> BalanceAsync(long id, int userId, CancellationToken ct = default);
}
