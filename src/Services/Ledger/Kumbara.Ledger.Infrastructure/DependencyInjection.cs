using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kumbara.Ledger.Application.Interfaces;
using Kumbara.Ledger.Infrastructure.Persistence;

namespace Kumbara.Ledger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost,1433;Database=KumbaraLedgerDb;User Id=sa;Password=Your_strong_password123!;TrustServerCertificate=True;MultipleActiveResultSets=true;";

        services.AddDbContext<LedgerDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            });
        });

        services.AddScoped<ILedgerDbContext>(sp => sp.GetRequiredService<LedgerDbContext>());
        services.AddScoped<ILedgerRepository, LedgerRepository>();

        return services;
    }
}
