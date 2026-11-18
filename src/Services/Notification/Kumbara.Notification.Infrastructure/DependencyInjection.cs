using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kumbara.Notification.Application.Interfaces;
using Kumbara.Notification.Infrastructure.Persistence;
using Kumbara.Notification.Infrastructure.Repositories;
using Kumbara.Notification.Infrastructure.Services;

namespace Kumbara.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var useInMemory = configuration.GetValue<bool>("Database:UseInMemory");
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (useInMemory || string.IsNullOrWhiteSpace(connectionString) || "InMemory".Equals(connectionString, StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<NotificationDbContext>(options =>
            {
                options.UseInMemoryDatabase("KumbaraNotificationDb");
            });
        }
        else
        {
            services.AddDbContext<NotificationDbContext>(options =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });
            });
        }

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationSenderService, SimulatedNotificationSenderService>();

        return services;
    }
}
