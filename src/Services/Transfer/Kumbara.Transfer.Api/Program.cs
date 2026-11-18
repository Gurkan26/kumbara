using Serilog;
using MassTransit;
using Kumbara.Transfer.Application.Sagas;
using Kumbara.Transfer.Domain.Entities;
using Kumbara.Transfer.Infrastructure;
using Kumbara.Transfer.Infrastructure.Persistence;
using Kumbara.Transfer.Api.Middleware;

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

// 2. Infrastructure Layer
builder.Services.AddInfrastructure(builder.Configuration);

// 3. MassTransit with RabbitMQ and EF Core Saga Repository
builder.Services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<TransferStateMachine, TransferState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.ExistingDbContext<TransferDbContext>();
        });

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitPortStr = builder.Configuration["RabbitMQ:Port"] ?? "5672";
        ushort.TryParse(rabbitPortStr, out var rabbitPort);
        var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(rabbitHost, rabbitPort == 0 ? (ushort)5672 : rabbitPort, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ConfigureEndpoints(context);
    });
});

// 4. Controllers
builder.Services.AddControllers();

// 5. Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Kumbara Transfer API",
        Version = "v1",
        Description = "Transfer Orchestration Service: MassTransit Saga State Machine for distributed money transfers."
    });
});

// 6. Health Checks
builder.Services.AddHealthChecks();

// 7. CORS
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

app.UseSerilogRequestLogging();
app.UseCors("AllowAll");

// Enable Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kumbara Transfer API v1");
    c.RoutePrefix = string.Empty;
});

// Idempotency Middleware
app.UseMiddleware<IdempotencyFilter>();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Kumbara.Transfer",
    timestamp = DateTime.UtcNow
}));

// Automatic schema initialization if database is reachable
try
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<TransferDbContext>();
    context.Database.EnsureCreated();
}
catch (Exception ex)
{
    Log.Warning(ex, "Could not ensure database created at startup. SQL Server might not be ready yet.");
}

try
{
    Log.Information("Starting Kumbara.Transfer.Api on port 5004...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Kumbara.Transfer.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
