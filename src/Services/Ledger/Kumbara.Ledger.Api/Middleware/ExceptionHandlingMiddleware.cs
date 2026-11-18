using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Kumbara.Ledger.Domain.Exceptions;

namespace Kumbara.Ledger.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = HttpStatusCode.InternalServerError;
        var message = "An unexpected error occurred.";

        switch (exception)
        {
            case InsufficientFundsException ifEx:
                statusCode = HttpStatusCode.BadRequest;
                message = ifEx.Message;
                _logger.LogWarning(ifEx, "Insufficient funds: {Message}", ifEx.Message);
                break;

            case CurrencyMismatchException cmEx:
                statusCode = HttpStatusCode.BadRequest;
                message = cmEx.Message;
                _logger.LogWarning(cmEx, "Currency mismatch: {Message}", cmEx.Message);
                break;

            case AccountFrozenException afEx:
                statusCode = HttpStatusCode.Forbidden;
                message = afEx.Message;
                _logger.LogWarning(afEx, "Account frozen: {Message}", afEx.Message);
                break;

            case DbUpdateConcurrencyException concEx:
                statusCode = HttpStatusCode.Conflict;
                message = "Optimistic concurrency conflict occurred on account balance snapshot. The record was modified by another operation. Please retry.";
                _logger.LogWarning(concEx, "Optimistic concurrency token conflict detected on account balance.");
                break;

            case ArgumentException or InvalidOperationException:
                statusCode = HttpStatusCode.BadRequest;
                message = exception.Message;
                _logger.LogWarning(exception, "Client error: {Message}", exception.Message);
                break;

            default:
                _logger.LogError(exception, "Unhandled exception occurred.");
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            statusCode = (int)statusCode,
            error = message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
