using Microsoft.Extensions.DependencyInjection;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Services;

namespace GoDental.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ISessionController, UserSession>();
        services.AddSingleton<IUserSession>(provider => provider.GetRequiredService<ISessionController>());
        services.AddTransient<ProductService>();
        services.AddTransient<InventoryService>();
        services.AddTransient<InventoryCountService>();
        services.AddTransient<SupplierService>();
        services.AddTransient<CustomerService>();
        services.AddTransient<SalesService>();
        services.AddTransient<CashService>();
        services.AddTransient<OrderService>();
        services.AddTransient<InventorySetupService>();
        services.AddTransient<FiscalPendingService>();
        services.AddTransient<MvpOperationService>();
        services.AddTransient<SaleReceiptService>();
        services.AddTransient<CategoryService>();
        services.AddTransient<UserManagementService>();
        return services;
    }
}

