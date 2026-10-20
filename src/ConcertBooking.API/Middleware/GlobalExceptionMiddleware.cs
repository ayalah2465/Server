using Microsoft.AspNetCore.Mvc;

namespace ConcertBooking.API.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var correlationId = context.Items[CorrelationIdMiddleware.ItemName]?.ToString()
                ?? Guid.NewGuid().ToString("N");
            logger.LogError(
                "Unhandled request failure of type {ExceptionType} with CorrelationId {CorrelationId}",
                exception.GetType().Name,
                correlationId);

            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
            var problem = new ProblemDetails
            {
                Type = "https://httpstatuses.com/500",
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "Use the correlation ID when reporting this error.",
                Instance = context.Request.Path,
                Extensions = { ["correlationId"] = correlationId }
            };
            await context.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json");
        }
    }
}
