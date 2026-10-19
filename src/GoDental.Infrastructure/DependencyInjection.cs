using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Configuration;
using GoDental.Infrastructure.Data;
using GoDental.Application.Abstractions.Security;
using GoDental.Infrastructure.Security;
using GoDental.Infrastructure.Repositories;
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
        services.AddTransient<IDbConnectionFactory, WorkstationConnectionFactory>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddTransient<IIdentityGateway, SqlIdentityGateway>();
        services.AddTransient<IProductRepository, SqlProductRepository>();
        services.AddTransient<IInventoryRepository, SqlInventoryRepository>();
        services.AddTransient<IInventoryCountRepository, SqlInventoryCountRepository>();
        services.AddTransient<ICategoryRepository, SqlCategoryRepository>();
        services.AddTransient<IUserRepository, SqlUserRepository>();
        services.AddTransient<IConnectionHealthCheck, SqlConnectionHealthCheck>();

        return services;
    }
}

