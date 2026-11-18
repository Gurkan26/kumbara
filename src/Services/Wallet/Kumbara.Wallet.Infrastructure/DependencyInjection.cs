namespace Kumbara.Wallet.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kumbara.Wallet.Application.Interfaces;
using Kumbara.Wallet.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WalletDb")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost,1433;Database=KumbaraWalletDb;User Id=sa;Password=Your_strong_password123!;TrustServerCertificate=True;";

        services.AddDbContext<WalletDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IWalletRepository, WalletRepository>();

        return services;
    }
}
