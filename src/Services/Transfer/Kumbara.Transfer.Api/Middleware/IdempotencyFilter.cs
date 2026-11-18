using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Kumbara.BuildingBlocks.Idempotency;
using Kumbara.Transfer.Infrastructure.Persistence;

namespace Kumbara.Transfer.Api.Middleware;

public class IdempotencyFilter
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyFilter> _logger;

    public IdempotencyFilter(RequestDelegate next, ILogger<IdempotencyFilter> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        // Only enforce idempotency for POST to api/transfers
        if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.Path.StartsWithSegments("/api/transfers"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyValues) ||
            string.IsNullOrWhiteSpace(idempotencyKeyValues.FirstOrDefault()))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "MissingIdempotencyKey",
                message = "The 'Idempotency-Key' header is required for initiating transfers."
            }));
            return;
        }

        var idempotencyKey = idempotencyKeyValues.First()!.Trim();

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<TransferDbContext>();

        if (dbContext != null)
        {
            try
            {
                var existingRecord = await dbContext.IdempotencyRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey);

                if (existingRecord != null)
                {
                    _logger.LogInformation("Idempotent request detected for key {IdempotencyKey}. Returning cached response.", idempotencyKey);
                    context.Response.StatusCode = existingRecord.StatusCode;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(existingRecord.ResponseBody);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not check idempotency record in database. Proceeding with execution.");
            }
        }

        context.Request.EnableBuffering();
        string requestBody;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        var requestHash = ComputeSha256(requestBody);

        var originalResponseBody = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await _next(context);

            responseBodyStream.Position = 0;
            var responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();
            responseBodyStream.Position = 0;

            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300 && dbContext != null)
            {
                try
                {
                    var record = new IdempotencyRecord
                    {
                        IdempotencyKey = idempotencyKey,
                        RequestHash = requestHash,
                        ResponseBody = responseBody,
                        StatusCode = context.Response.StatusCode,
                        CreatedAt = DateTime.UtcNow
                    };

                    dbContext.IdempotencyRecords.Add(record);
                    await dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save idempotency record for key {IdempotencyKey}", idempotencyKey);
                }
            }

            await responseBodyStream.CopyToAsync(originalResponseBody);
        }
        finally
        {
            context.Response.Body = originalResponseBody;
        }
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
