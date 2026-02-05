using FluentValidation;
using LiteBus.Queries.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using SafeTravel.Application.DTOs;
using SafeTravel.Application.Handlers;
using SafeTravel.Application.Queries;
using SafeTravel.Application.Services;
using SafeTravel.Application.Validators;
using SafeTravel.Domain.Services;

namespace SafeTravel.Application.DependencyInjection;

/// <summary>
/// Extension methods for registering Application layer services.
/// </summary>
public static class ApplicationServiceRegistration
{
    /// <summary>
    /// Adds Application layer services to the DI container.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register LiteBus query handlers
        services.AddScoped<IQueryHandler<GetTop10DistrictsQuery, Top10DistrictsResponse>, GetTop10DistrictsHandler>();
        services.AddScoped<IQueryHandler<GetTravelRecommendationQuery, TravelRecommendationResponse>, GetTravelRecommendationHandler>();

        // Register FluentValidation validators
        services.AddScoped<IValidator<TravelRecommendationRequest>, TravelRecommendationRequestValidator>();

        // Register application services
        services.AddScoped<IWeatherDataService, WeatherDataService>();

        // Register domain services
        services.AddSingleton<DistrictRankingService>();
        services.AddSingleton<TravelRecommendationPolicy>();
        services.AddSingleton<WeatherAggregator>();

        return services;
    }
}
