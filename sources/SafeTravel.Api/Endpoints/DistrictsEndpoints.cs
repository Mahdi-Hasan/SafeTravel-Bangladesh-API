using LiteBus.Queries.Abstractions;
using SafeTravel.Application.DTOs;
using SafeTravel.Application.Queries;

namespace SafeTravel.Api.Endpoints;

/// <summary>
/// Endpoints for district-related operations.
/// </summary>
public static class DistrictsEndpoints
{
    /// <summary>
    /// Maps district endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapDistrictsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/districts")
            .WithTags("Districts");

        group.MapGet("/top-10", GetTop10DistrictsAsync)
            .WithName("GetTop10Districts")
            .WithSummary("Get the top 10 coolest and cleanest districts")
            .WithDescription("Returns the top 10 districts based on 7-day forecast of temperature and air quality (PM2.5).")
            .Produces<Top10DistrictsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> GetTop10DistrictsAsync(
        IQueryHandler<GetTop10DistrictsQuery, Top10DistrictsResponse> queryHandler,
        CancellationToken cancellationToken)
    {
        var query = new GetTop10DistrictsQuery();
        var response = await queryHandler.HandleAsync(query, cancellationToken);

        return Results.Ok(response);
    }
}
