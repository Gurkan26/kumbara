namespace Kumbara.Risk.Application;

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Kumbara.Risk.Domain.Rules;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // Register default domain risk rules in sequence
        services.AddScoped<IRiskRule, SingleTransactionLimitRule>();
        services.AddScoped<IRiskRule, DailyVelocityRule>();
        services.AddScoped<IRiskRule, SuspiciousVelocityFrequencyRule>();

        return services;
    }
}
