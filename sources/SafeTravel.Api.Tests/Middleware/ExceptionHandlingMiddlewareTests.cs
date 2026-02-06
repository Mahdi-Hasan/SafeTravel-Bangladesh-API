using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SafeTravel.Api.Middleware;
using SafeTravel.Domain.Exceptions;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace SafeTravel.Api.Tests.Middleware;

/// <summary>
/// Unit tests for ExceptionHandlingMiddleware.
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<ExceptionHandlingMiddleware> _logger = Substitute.For<ILogger<ExceptionHandlingMiddleware>>();

    [Fact]
    public async Task InvokeAsync_ShouldReturn404_WhenDistrictNotFoundExceptionThrown()
    {
        // Arrange
        var (middleware, context) = CreateMiddlewareAndContext(() =>
            throw new DistrictNotFoundException("NonExistent"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        context.Response.ContentType.ShouldStartWith("application/");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn400_WhenInvalidDateRangeExceptionThrown()
    {
        // Arrange
        var (middleware, context) = CreateMiddlewareAndContext(() =>
            throw new InvalidDateRangeException("Invalid date range"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldStartWith("application/");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn503_WhenWeatherDataUnavailableExceptionThrown()
    {
        // Arrange
        var (middleware, context) = CreateMiddlewareAndContext(() =>
            throw new WeatherDataUnavailableException("Weather service unavailable"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.ShouldStartWith("application/");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn500_WhenUnexpectedExceptionThrown()
    {
        // Arrange
        var (middleware, context) = CreateMiddlewareAndContext(() =>
            throw new InvalidOperationException("Something went wrong"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.ShouldStartWith("application/");
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotCatchException_WhenNoExceptionThrown()
    {
        // Arrange
        var (middleware, context) = CreateMiddlewareAndContext(() => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - when no exception, status code should be default 200
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_ShouldWriteProblemDetails_WhenExceptionThrown()
    {
        // Arrange
        var (middleware, context) = CreateMiddlewareAndContext(() =>
            throw new DistrictNotFoundException("Dhaka"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert - read the response body
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, JsonOptions);

        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(404);
        problemDetails.Title.ShouldBe("District Not Found");
        problemDetails.Detail.ShouldNotBeNull();
        problemDetails.Detail.ShouldContain("Dhaka");
    }

    private (ExceptionHandlingMiddleware middleware, HttpContext context) CreateMiddlewareAndContext(
        Func<Task> nextAction)
    {
        RequestDelegate next = _ => nextAction();
        var middleware = new ExceptionHandlingMiddleware(next, _logger);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/v1/test";

        return (middleware, context);
    }
}
