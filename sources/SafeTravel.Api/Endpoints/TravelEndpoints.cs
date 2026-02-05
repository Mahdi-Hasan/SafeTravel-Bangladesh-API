using LiteBus.Queries.Abstractions;
using SafeTravel.Application.DTOs;
using SafeTravel.Application.Queries;

namespace SafeTravel.Api.Endpoints;

/// <summary>
/// Endpoints for travel recommendation operations.
/// </summary>
public static class TravelEndpoints
{
    /// <summary>
    /// Maps travel endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapTravelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/travel")
            .WithTags("Travel");

        group.MapPost("/recommendation", GetTravelRecommendationAsync)
            .WithName("GetTravelRecommendation")
            .WithSummary("Get travel recommendation for a destination")
            .WithDescription("Evaluates whether traveling to a destination district is recommended based on temperature and air quality comparison.")
            .Produces<TravelRecommendationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> GetTravelRecommendationAsync(
        TravelRecommendationRequest request,
        IQueryHandler<GetTravelRecommendationQuery, TravelRecommendationResponse> queryHandler,
        CancellationToken cancellationToken)
    {
        var query = new GetTravelRecommendationQuery(request);
        var response = await queryHandler.HandleAsync(query, cancellationToken);

        return Results.Ok(response);
    }
}
