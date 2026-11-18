using Serilog;
using MassTransit;
using Kumbara.Notification.Application;
using Kumbara.Notification.Infrastructure;
using Kumbara.Notification.Infrastructure.Persistence;
using Kumbara.Notification.Infrastructure.Consumers;
using Kumbara.Notification.Api.Middleware;

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

// 3. MassTransit with RabbitMQ for Saga event consumers
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TransferCompletedNotificationConsumer>();
    x.AddConsumer<TransferFailedNotificationConsumer>();

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
        Title = "Kumbara Notification API",
        Version = "v1",
        Description = "Notification Service: Idempotent multi-channel notifications and MassTransit saga event consumers."
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

// Global Exception Handler Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();
app.UseCors("AllowAll");

// Enable Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kumbara Notification API v1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Kumbara.Notification",
    port = 5006,
    timestamp = DateTime.UtcNow
}));

// Automatic schema initialization if database is reachable
try
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    context.Database.EnsureCreated();
}
catch (Exception ex)
{
    Log.Warning(ex, "Could not ensure Notification database created at startup.");
}

try
{
    Log.Information("Starting Kumbara.Notification.Api on port 5006...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Kumbara.Notification.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
