using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Configuration;
using GoDental.Infrastructure.Data;
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
        services.AddTransient<IDbConnectionFactory>(provider => provider.GetRequiredService<SqlConnectionFactory>());
        services.AddTransient<IConnectionHealthCheck, SqlConnectionHealthCheck>();

        return services;
    }
}
