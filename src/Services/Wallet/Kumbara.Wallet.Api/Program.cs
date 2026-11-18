using Serilog;
using Kumbara.Wallet.Application;
using Kumbara.Wallet.Infrastructure;
using Kumbara.Wallet.Infrastructure.Persistence;

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

// Controllers, Swagger & HealthChecks
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Kumbara.Wallet.Api", Version = "v1" });
});
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kumbara.Wallet.Api v1"));
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Kumbara.Wallet" }));

// Optional: Ensure DB schema created if connection is ready
try
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
    context.Database.EnsureCreated();
}
catch (Exception ex)
{
    Log.Warning(ex, "Could not ensure database created at startup. SQL Server might not be running yet.");
}

try
{
    Log.Information("Starting Kumbara.Wallet.Api on port 5002...");
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
