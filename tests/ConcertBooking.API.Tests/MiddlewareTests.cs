using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ConcertBooking.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ConcertBooking.API.Tests;

public sealed class MiddlewareTests
{
    [Fact]
    public async Task Unexpected_error_returns_uniform_json_and_matching_correlation_id_at_error_level()
    {
        const string secret = "NeverLogThisPassword123!";
        var entries = new ConcurrentBag<LogEntry>();
        using var loggerFactory = CreateLoggerFactory(entries);
        RequestDelegate failedEndpoint = _ => throw new InvalidOperationException(secret);
        var correlation = new CorrelationIdMiddleware(
            failedEndpoint,
            loggerFactory.CreateLogger<CorrelationIdMiddleware>());
        var errors = new GlobalExceptionMiddleware(
            correlation.InvokeAsync,
            loggerFactory.CreateLogger<GlobalExceptionMiddleware>());
        var context = NewContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "test-correlation-42";

        await errors.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.Equal("test-correlation-42", context.Response.Headers[CorrelationIdMiddleware.HeaderName]);
        Assert.Equal("test-correlation-42", body.RootElement.GetProperty("correlationId").GetString());
        Assert.Contains(entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("test-correlation-42"));
        Assert.DoesNotContain(secret, string.Join('\n', entries.Select(entry => entry.Message)));
    }

    [Fact]
    public async Task Conflict_is_logged_as_warning_without_headers_or_request_body()
    {
        const string token = "secret-jwt-value";
        const string password = "SecretRegistrationPassword!";
        var entries = new ConcurrentBag<LogEntry>();
        using var loggerFactory = CreateLoggerFactory(entries);
        RequestDelegate endpoint = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsync("conflict");
        };
        var middleware = new CorrelationIdMiddleware(
            endpoint,
            loggerFactory.CreateLogger<CorrelationIdMiddleware>());
        var context = NewContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/auth/register";
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes($"{{\"password\":\"{password}\"}}"));

        await middleware.InvokeAsync(context);

        var logText = string.Join('\n', entries.Select(entry => entry.Message));
        Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("409"));
        Assert.DoesNotContain(token, logText);
        Assert.DoesNotContain(password, logText);
        Assert.False(string.IsNullOrWhiteSpace(context.Response.Headers[CorrelationIdMiddleware.HeaderName]));
    }

    [Fact]
    public async Task Invalid_incoming_correlation_id_is_replaced()
    {
        var entries = new ConcurrentBag<LogEntry>();
        using var loggerFactory = CreateLoggerFactory(entries);
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            loggerFactory.CreateLogger<CorrelationIdMiddleware>());
        var context = NewContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "invalid value with spaces";

        await middleware.InvokeAsync(context);

        var actual = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.Equal(32, actual.Length);
        Assert.DoesNotContain(' ', actual);
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ILoggerFactory CreateLoggerFactory(ConcurrentBag<LogEntry> entries) =>
        LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(new CaptureLoggerProvider(entries)));

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class CaptureLoggerProvider(ConcurrentBag<LogEntry> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(entries);
        public void Dispose() { }
    }

    private sealed class CaptureLogger(ConcurrentBag<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}
