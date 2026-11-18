using Serilog;
using Kumbara.Ledger.Application;
using Kumbara.Ledger.Infrastructure;
using Kumbara.Ledger.Infrastructure.Persistence;
using Kumbara.Ledger.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog Configuration
var seqUrl = builder.Configuration["Serilog:SeqServerUrl"] 
    ?? builder.Configuration["Seq:ServerUrl"] 
    ?? "http://localhost:5341";

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(seqUrl)
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Application & Infrastructure Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 3. Controllers & Exception Handling
builder.Services.AddControllers();

// 4. Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Kumbara Ledger API",
        Version = "v1",
        Description = "Core Ledger Service: Double-entry bookkeeping, append-only ledger entries, and optimistic balance concurrency."
    });
});

// 5. Health Checks
builder.Services.AddHealthChecks();

// 6. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Global Exception Handler Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();
app.UseCors("AllowAll");

// Enable Swagger in Development and Container environments
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kumbara Ledger API v1");
    c.RoutePrefix = string.Empty; // Swagger UI at root
});

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Kumbara.Ledger",
    timestamp = DateTime.UtcNow
}));

// Automatic schema initialization if database is reachable
try
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
    context.Database.EnsureCreated();
}
catch (Exception ex)
{
    Log.Warning(ex, "Could not ensure database created at startup. SQL Server might not be ready yet.");
}

try
{
    Log.Information("Starting Kumbara.Ledger.Api on port 5003...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Kumbara.Ledger.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
