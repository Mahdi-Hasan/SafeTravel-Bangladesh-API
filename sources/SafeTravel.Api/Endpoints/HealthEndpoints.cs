using Microsoft.AspNetCore.Http;
using SafeTravel.Domain.Interfaces;

namespace SafeTravel.Api.Endpoints;

/// <summary>
/// Endpoints for health check operations.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Maps health endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/health")
            .WithTags("Health");

        group.MapGet("/live", LivenessCheck)
            .WithName("LivenessCheck")
            .WithSummary("Liveness probe")
            .WithDescription("Returns 200 OK if the application is running.")
            .Produces(StatusCodes.Status200OK)
            .ExcludeFromDescription();

        group.MapGet("/ready", ReadinessCheck)
            .WithName("ReadinessCheck")
            .WithSummary("Readiness probe")
            .WithDescription("Returns 200 OK if the application is ready to serve requests (Redis available and cache populated).")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status503ServiceUnavailable)
            .ExcludeFromDescription();

        return app;
    }

    private static IResult LivenessCheck()
    {
        return Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
    }

    private static async Task<IResult> ReadinessCheck(IWeatherDataCache cache, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await cache.GetMetadataAsync(cancellationToken);

            if (metadata == null)
            {
                return Results.Problem(
                    title: "Service Unavailable",
                    detail: "No weather data available in cache.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var staleness = DateTime.UtcNow - metadata.LastUpdated;
            var stalenessThreshold = TimeSpan.FromMinutes(30);

            if (staleness > stalenessThreshold)
            {
                return Results.Problem(
                    title: "Service Degraded",
                    detail: $"Weather data is stale (last updated: {metadata.LastUpdated:O}, age: {staleness.TotalMinutes:F1} minutes).",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new
            {
                status = "Healthy",
                lastUpdated = metadata.LastUpdated,
                dataAge = staleness,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Service Unavailable",
                detail: $"Cache connectivity check failed: {ex.Message}",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
