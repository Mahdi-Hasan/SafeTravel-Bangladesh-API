using FluentValidation;
using LiteBus.Queries.Abstractions;
using SafeTravel.Application.DTOs;
using SafeTravel.Application.Queries;
using SafeTravel.Application.Services;
using SafeTravel.Domain.Exceptions;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.Services;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Application.Handlers;

/// <summary>
/// Handles travel recommendation queries by orchestrating weather data retrieval
/// and applying the recommendation policy.
/// </summary>
public sealed class GetTravelRecommendationHandler 
    : IQueryHandler<GetTravelRecommendationQuery, TravelRecommendationResponse>
{
    private readonly IValidator<TravelRecommendationRequest> _validator;
    private readonly IDistrictRepository _districtRepository;
    private readonly IWeatherDataService _weatherDataService;
    private readonly TravelRecommendationPolicy _recommendationPolicy;

    public GetTravelRecommendationHandler(
        IValidator<TravelRecommendationRequest> validator,
        IDistrictRepository districtRepository,
        IWeatherDataService weatherDataService,
        TravelRecommendationPolicy recommendationPolicy)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _districtRepository = districtRepository ?? throw new ArgumentNullException(nameof(districtRepository));
        _weatherDataService = weatherDataService ?? throw new ArgumentNullException(nameof(weatherDataService));
        _recommendationPolicy = recommendationPolicy ?? throw new ArgumentNullException(nameof(recommendationPolicy));
    }

    public async Task<TravelRecommendationResponse> HandleAsync(
        GetTravelRecommendationQuery query,
        CancellationToken cancellationToken = default)
    {
        var request = query.Request;

        // Validate request
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new InvalidDateRangeException(
                string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage)));
        }

        // Find destination district
        var destination = _districtRepository.GetByName(request.DestinationDistrict)
            ?? throw new DistrictNotFoundException(request.DestinationDistrict);

        // Get weather data for origin and destination
        var originCoordinates = Coordinates.Create(request.Latitude, request.Longitude);

        var originWeatherTask = _weatherDataService.GetWeatherForCoordinatesAsync(
            originCoordinates, request.TravelDate, cancellationToken);

        var destinationWeatherTask = _weatherDataService.GetWeatherForDistrictAsync(
            destination, request.TravelDate, cancellationToken);

        await Task.WhenAll(originWeatherTask, destinationWeatherTask);

        var originWeather = await originWeatherTask;
        var destinationWeather = await destinationWeatherTask;

        // Apply recommendation policy
        var recommendation = _recommendationPolicy.Evaluate(
            originWeather, destinationWeather, destination.Name);

        // Build comparison DTO
        var comparison = new WeatherComparisonDto(
            OriginTemperature: Math.Round(originWeather.Temperature.Celsius, 1),
            OriginPM25: Math.Round(originWeather.PM25.Value, 1),
            OriginAirQuality: originWeather.PM25.GetAirQualityCategory().ToString(),
            DestinationTemperature: Math.Round(destinationWeather.Temperature.Celsius, 1),
            DestinationPM25: Math.Round(destinationWeather.PM25.Value, 1),
            DestinationAirQuality: destinationWeather.PM25.GetAirQualityCategory().ToString());

        return new TravelRecommendationResponse(
            IsRecommended: recommendation.IsRecommended,
            Reason: recommendation.Reason,
            DestinationDistrict: destination.Name,
            Comparison: comparison,
            TravelDate: request.TravelDate);
    }
}
