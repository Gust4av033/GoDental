using Microsoft.Extensions.DependencyInjection;

namespace GoDental.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Registrar aquí casos de uso, validators y servicios de Application
        // conforme cada módulo sea implementado.
        return services;
    }
}
