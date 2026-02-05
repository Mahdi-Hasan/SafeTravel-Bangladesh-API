using Serilog.Context;

namespace SafeTravel.Api.Middleware;

/// <summary>
/// Middleware that adds correlation ID and logs HTTP requests/responses.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        // Add correlation ID to response headers
        context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);

        // Push correlation ID to Serilog LogContext for all logs in this request
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path))
        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        {
            var startTime = DateTime.UtcNow;

            _logger.LogInformation(
                "Request started: {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            try
            {
                await _next(context);
            }
            finally
            {
                var duration = DateTime.UtcNow - startTime;

                _logger.LogInformation(
                    "Request completed: {Method} {Path} - {StatusCode} in {DurationMs}ms",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    duration.TotalMilliseconds);
            }
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.ToString();
        }

        return Guid.NewGuid().ToString();
    }
}

/// <summary>
/// Extension methods for registering request logging middleware.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestLoggingMiddleware>();
    }
}
