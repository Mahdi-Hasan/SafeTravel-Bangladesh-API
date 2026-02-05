using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SafeTravel.Domain.Exceptions;
using System.Net;

namespace SafeTravel.Api.Middleware;

/// <summary>
/// Global exception handling middleware that returns RFC 7807 Problem Details.
/// </summary>
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
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail) = exception switch
        {
            DistrictNotFoundException dnfe => (
                StatusCodes.Status404NotFound,
                "District Not Found",
                dnfe.Message
            ),
            InvalidDateRangeException idre => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                idre.Message
            ),
            WeatherDataUnavailableException wdue => (
                StatusCodes.Status503ServiceUnavailable,
                "Service Unavailable",
                wdue.Message
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred. Please try again later."
            )
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Type = GetProblemType(statusCode),
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path
        };

        return context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static string GetProblemType(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
        StatusCodes.Status503ServiceUnavailable => "https://tools.ietf.org/html/rfc7231#section-6.6.4",
        _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
    };
}

/// <summary>
/// Extension methods for registering exception handling middleware.
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
