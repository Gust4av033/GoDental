using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Configuration;
using GoDental.Infrastructure.Data;
using GoDental.Application.Abstractions.Security;
using GoDental.Infrastructure.Security;
using GoDental.Infrastructure.Repositories;
using GoDental.Infrastructure.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoDental.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName));

        services.AddTransient<SqlConnectionFactory>();
        services.AddTransient<IWorkstationConnectionOpener>(provider => provider.GetRequiredService<SqlConnectionFactory>());
        services.AddTransient<IDbConnectionFactory, WorkstationConnectionFactory>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddTransient<IIdentityGateway, SqlIdentityGateway>();
        services.AddTransient<IProductRepository, SqlProductRepository>();
        services.AddTransient<IInventoryRepository, SqlInventoryRepository>();
        services.AddTransient<IInventoryCountRepository, SqlInventoryCountRepository>();
        services.AddTransient<ISupplierRepository, SqlSupplierRepository>();
        services.AddTransient<ICustomerRepository, SqlCustomerRepository>();
        services.AddTransient<ISalesRepository, SqlMvpRepository>();
        services.AddTransient<ICashRepository, SqlMvpRepository>();
        services.AddTransient<ICollectionRepository, SqlMvpRepository>();
        services.AddTransient<ICreditDocumentRenderer, CreditDocumentRenderer>();
        services.AddTransient<IOrderRepository, SqlMvpRepository>();
        services.AddTransient<IInventorySetupRepository, SqlMvpRepository>();
        services.AddTransient<IFiscalPendingRepository, SqlMvpRepository>();
        services.AddTransient<IMvpOperationRepository, SqlMvpRepository>();
        services.AddTransient<ICommercialParametersRepository, SqlMvpRepository>();
        services.AddTransient<IPurchaseRepository, SqlMvpRepository>();
        services.AddTransient<IImportRepository, SqlMvpRepository>();
        services.AddTransient<ISaleReturnRepository, SqlMvpRepository>();
        services.AddTransient<IReturnEvidenceStore>(sp => new FileReturnEvidenceStore(sp.GetRequiredService<IDbConnectionFactory>()));
        services.AddOptions<ReceiptPdfOptions>().Bind(configuration.GetSection("Pdf"));
        services.AddTransient<IReceiptRenderer, ReceiptPdfRenderer>();
        services.AddTransient<IBarcodeLabelRenderer, BarcodeLabelPdfRenderer>();
        services.AddTransient<ICategoryRepository, SqlCategoryRepository>();
        services.AddTransient<IUserRepository, SqlUserRepository>();
        services.AddTransient<IConnectionHealthCheck, SqlConnectionHealthCheck>();

        return services;
    }
}

