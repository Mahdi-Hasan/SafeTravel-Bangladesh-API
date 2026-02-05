using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.ValueObjects;
using SafeTravel.Infrastructure.ExternalApis.Models;
using static SafeTravel.Infrastructure.InfrastructureConstants;

namespace SafeTravel.Infrastructure.ExternalApis;

public sealed class OpenMeteoClient : IOpenMeteoClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenMeteoClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OpenMeteoClient(HttpClient httpClient, ILogger<OpenMeteoClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BulkWeatherResponse> GetBulkForecastAsync(
        IEnumerable<Coordinates> locations,
        int days,
        CancellationToken cancellationToken = default)
    {
        var locationList = locations.ToList();
        if (locationList.Count == 0)
        {
            return new BulkWeatherResponse(new Dictionary<Coordinates, IReadOnlyList<HourlyWeatherData>>());
        }

        var results = new Dictionary<Coordinates, IReadOnlyList<HourlyWeatherData>>();

        // Open-Meteo supports bulk requests via comma-separated lat/lon
        var latitudes = string.Join(",", locationList.Select(l => l.Latitude.ToString(CultureInfo.InvariantCulture)));
        var longitudes = string.Join(",", locationList.Select(l => l.Longitude.ToString(CultureInfo.InvariantCulture)));

        var url = $"{OpenMeteoApi.WeatherBaseUrl}?latitude={latitudes}&longitude={longitudes}&hourly=temperature_2m&forecast_days={days}";

        _logger.LogDebug("Fetching weather data for {Count} locations", locationList.Count);

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        // Open-Meteo returns array for multiple locations, single object for one location
        if (locationList.Count == 1)
        {
            var singleResponse = JsonSerializer.Deserialize<OpenMeteoWeatherApiResponse>(json, JsonOptions);
            if (singleResponse?.Hourly != null)
            {
                var data = ParseWeatherHourlyData(singleResponse.Hourly);
                results[locationList[0]] = data;
            }
        }
        else
        {
            var multiResponse = JsonSerializer.Deserialize<List<OpenMeteoWeatherApiResponse>>(json, JsonOptions);
            if (multiResponse != null)
            {
                for (var i = 0; i < Math.Min(locationList.Count, multiResponse.Count); i++)
                {
                    var apiResponse = multiResponse[i];
                    if (apiResponse.Hourly != null)
                    {
                        var data = ParseWeatherHourlyData(apiResponse.Hourly);
                        results[locationList[i]] = data;
                    }
                }
            }
        }

        _logger.LogInformation("Successfully fetched weather data for {Count} locations", results.Count);

        return new BulkWeatherResponse(results);
    }

    public async Task<BulkAirQualityResponse> GetBulkAirQualityAsync(
        IEnumerable<Coordinates> locations,
        int days,
        CancellationToken cancellationToken = default)
    {
        var locationList = locations.ToList();
        if (locationList.Count == 0)
        {
            return new BulkAirQualityResponse(new Dictionary<Coordinates, IReadOnlyList<HourlyAirQualityData>>());
        }

        var results = new Dictionary<Coordinates, IReadOnlyList<HourlyAirQualityData>>();

        var latitudes = string.Join(",", locationList.Select(l => l.Latitude.ToString(CultureInfo.InvariantCulture)));
        var longitudes = string.Join(",", locationList.Select(l => l.Longitude.ToString(CultureInfo.InvariantCulture)));

        var url = $"{OpenMeteoApi.AirQualityBaseUrl}?latitude={latitudes}&longitude={longitudes}&hourly=pm2_5&forecast_days={days}";

        _logger.LogDebug("Fetching air quality data for {Count} locations", locationList.Count);

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (locationList.Count == 1)
        {
            var singleResponse = JsonSerializer.Deserialize<OpenMeteoAirQualityApiResponse>(json, JsonOptions);
            if (singleResponse?.Hourly != null)
            {
                var data = ParseAirQualityHourlyData(singleResponse.Hourly);
                results[locationList[0]] = data;
            }
        }
        else
        {
            var multiResponse = JsonSerializer.Deserialize<List<OpenMeteoAirQualityApiResponse>>(json, JsonOptions);
            if (multiResponse != null)
            {
                for (var i = 0; i < Math.Min(locationList.Count, multiResponse.Count); i++)
                {
                    var apiResponse = multiResponse[i];
                    if (apiResponse.Hourly != null)
                    {
                        var data = ParseAirQualityHourlyData(apiResponse.Hourly);
                        results[locationList[i]] = data;
                    }
                }
            }
        }

        _logger.LogInformation("Successfully fetched air quality data for {Count} locations", results.Count);

        return new BulkAirQualityResponse(results);
    }

    private static List<HourlyWeatherData> ParseWeatherHourlyData(OpenMeteoWeatherApiResponse.HourlyData hourly)
    {
        var result = new List<HourlyWeatherData>();

        if (hourly.Time == null || hourly.Temperature_2m == null)
            return result;

        for (var i = 0; i < Math.Min(hourly.Time.Count, hourly.Temperature_2m.Count); i++)
        {
            if (DateTime.TryParse(hourly.Time[i], out var time) && hourly.Temperature_2m[i].HasValue)
            {
                result.Add(new HourlyWeatherData(time, hourly.Temperature_2m[i]!.Value));
            }
        }

        return result;
    }

    private static List<HourlyAirQualityData> ParseAirQualityHourlyData(OpenMeteoAirQualityApiResponse.HourlyData hourly)
    {
        var result = new List<HourlyAirQualityData>();

        if (hourly.Time == null || hourly.Pm2_5 == null)
            return result;

        for (var i = 0; i < Math.Min(hourly.Time.Count, hourly.Pm2_5.Count); i++)
        {
            if (DateTime.TryParse(hourly.Time[i], out var time) && hourly.Pm2_5[i].HasValue)
            {
                result.Add(new HourlyAirQualityData(time, hourly.Pm2_5[i]!.Value));
            }
        }

        return result;
    }
}
