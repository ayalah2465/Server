using Microsoft.Extensions.Primitives;

namespace ConcertBooking.API.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemName = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ReadCorrelationId(context.Request.Headers[HeaderName]) ?? Guid.NewGuid().ToString("N");
        context.Items[ItemName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object> { [ItemName] = correlationId });
        logger.LogInformation(
            "HTTP request started {Method} {Path} with CorrelationId {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        await next(context);

        if (context.Response.StatusCode == StatusCodes.Status409Conflict)
        {
            logger.LogWarning(
                "HTTP request completed with resource conflict {StatusCode} and CorrelationId {CorrelationId}",
                context.Response.StatusCode,
                correlationId);
            return;
        }

        logger.LogInformation(
            "HTTP request completed {StatusCode} with CorrelationId {CorrelationId}",
            context.Response.StatusCode,
            correlationId);
    }

    private static string? ReadCorrelationId(StringValues values)
    {
        var value = values.Count == 1 ? values[0] : null;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return null;
        return value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
            ? value
            : null;
    }
}
