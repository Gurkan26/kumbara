using System.Net;
using System.Text.Json;
using Kumbara.Notification.Domain.Exceptions;

namespace Kumbara.Notification.Api.Middleware;

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
            case DuplicateNotificationException dupEx:
                statusCode = HttpStatusCode.Conflict;
                message = dupEx.Message;
                _logger.LogWarning(dupEx, "Duplicate notification: {Message}", dupEx.Message);
                break;

            case ArgumentException or InvalidOperationException:
                statusCode = HttpStatusCode.BadRequest;
                message = exception.Message;
                _logger.LogWarning(exception, "Client error: {Message}", exception.Message);
                break;

            default:
                _logger.LogError(exception, "Unhandled exception in Notification API.");
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
