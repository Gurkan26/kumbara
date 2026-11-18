namespace Kumbara.Risk.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kumbara.Risk.Domain.Interfaces;
using Kumbara.Risk.Infrastructure.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<RedisVelocityService>();
        services.AddSingleton<IVelocityService>(sp => sp.GetRequiredService<RedisVelocityService>());
        services.AddSingleton<Kumbara.Risk.Application.Interfaces.IVelocityService>(sp => sp.GetRequiredService<RedisVelocityService>());

        return services;
    }
}
