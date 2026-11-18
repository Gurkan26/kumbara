using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Kumbara.Identity.Application;
using Kumbara.Identity.Infrastructure;
using Kumbara.Identity.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Serilog Configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341")
    .CreateLogger();

builder.Host.UseSerilog();

// Add Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Add JWT Bearer Authentication
var secret = builder.Configuration["Jwt:Secret"] ?? "KumbaraSuperSecretKeyForDevelopmentPurposesOnly12345!";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "Kumbara.Identity";
var audience = builder.Configuration["Jwt:Audience"] ?? "Kumbara";
var key = Encoding.UTF8.GetBytes(secret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Controllers & HealthChecks
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Kumbara.Identity" }));

// Optional: Ensure DB created if connection is ready
try
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    // If SQL Server is available, create DB schema if not exists
    context.Database.EnsureCreated();
}
catch (Exception ex)
{
    Log.Warning(ex, "Could not ensure database created at startup. SQL Server might not be running yet.");
}

try
{
    Log.Information("Starting Kumbara.Identity.Api on port 5001...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
