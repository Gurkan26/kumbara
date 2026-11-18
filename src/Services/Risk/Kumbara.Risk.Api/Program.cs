using Serilog;
using MassTransit;
using Kumbara.Risk.Application;
using Kumbara.Risk.Infrastructure;
using Kumbara.Risk.Infrastructure.Consumers;

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

// 3. MassTransit with RabbitMQ for Risk Consumers
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CheckTransferRiskConsumer>();

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
        Title = "Kumbara Risk API",
        Version = "v1",
        Description = "Risk Service: Transaction velocity limits, frequency checks, and MassTransit consumers for transfer saga risk evaluation."
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kumbara Risk API v1");
    });
}

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Kumbara.Risk",
    timestamp = DateTime.UtcNow
}));

try
{
    Log.Information("Starting Kumbara.Risk.Api on port 5005...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Kumbara.Risk.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
