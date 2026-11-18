namespace Kumbara.Identity.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kumbara.Identity.Application.Interfaces;
using Kumbara.Identity.Infrastructure.Persistence;
using Kumbara.Identity.Infrastructure.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost,1433;Database=KumbaraIdentityDb;User Id=sa;Password=Your_strong_password123!;TrustServerCertificate=True;";

        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
            options.UseOpenIddict();
        });

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                       .UseDbContext<IdentityDbContext>();
            })
            .AddServer(options =>
            {
                options.SetTokenEndpointUris("/connect/token");
                options.AllowPasswordFlow()
                       .AllowRefreshTokenFlow();
                options.AcceptAnonymousClients();
                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();
                options.UseAspNetCore()
                       .EnableTokenEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
}
