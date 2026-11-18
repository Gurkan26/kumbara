using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Kumbara.BuildingBlocks.Idempotency;
using Kumbara.Ledger.Application.Interfaces;

namespace Kumbara.Ledger.Api.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequireIdempotencyKeyAttribute : TypeFilterAttribute
{
    public RequireIdempotencyKeyAttribute() : base(typeof(IdempotencyFilter))
    {
    }
}

public class IdempotencyFilter : IAsyncActionFilter
{
    private readonly ILedgerDbContext _dbContext;
    private readonly ILogger<IdempotencyFilter> _logger;

    public IdempotencyFilter(ILedgerDbContext dbContext, ILogger<IdempotencyFilter> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var keyValue) ||
            string.IsNullOrWhiteSpace(keyValue))
        {
            context.Result = new BadRequestObjectResult(new 
            { 
                error = "Idempotency-Key header is required for this operation." 
            });
            return;
        }

        var idempotencyKey = keyValue.ToString();

        // Check if this idempotent request has already been processed
        var existing = await _dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey);

        if (existing != null)
        {
            _logger.LogInformation("Returning cached idempotent response for key: {Key}", idempotencyKey);
            context.Result = new ContentResult
            {
                Content = existing.ResponseBody,
                ContentType = "application/json",
                StatusCode = existing.StatusCode
            };
            return;
        }

        // Execute action
        var executedContext = await next();

        // Store successful response
        if (executedContext.Result is ObjectResult objectResult &&
            (objectResult.StatusCode == null || (objectResult.StatusCode >= 200 && objectResult.StatusCode < 300)))
        {
            try
            {
                var statusCode = objectResult.StatusCode ?? 200;
                var responseBody = JsonSerializer.Serialize(objectResult.Value);

                var record = new IdempotencyRecord
                {
                    IdempotencyKey = idempotencyKey,
                    RequestHash = context.HttpContext.Request.Path,
                    ResponseBody = responseBody,
                    StatusCode = statusCode,
                    CreatedAt = DateTime.UtcNow
                };

                await _dbContext.IdempotencyRecords.AddAsync(record);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist idempotency record for key {Key}", idempotencyKey);
            }
        }
    }
}
