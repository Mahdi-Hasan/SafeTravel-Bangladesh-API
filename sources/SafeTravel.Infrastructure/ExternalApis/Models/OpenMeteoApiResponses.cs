namespace SafeTravel.Infrastructure.ExternalApis.Models;

/// <summary>
/// Raw response from Open-Meteo weather API.
/// </summary>
public sealed record OpenMeteoWeatherApiResponse
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public HourlyData? Hourly { get; init; }

    public sealed record HourlyData
    {
        public List<string>? Time { get; init; }
        public List<double?>? Temperature_2m { get; init; }
    }
}

/// <summary>
/// Raw response from Open-Meteo air quality API.
/// </summary>
public sealed record OpenMeteoAirQualityApiResponse
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public HourlyData? Hourly { get; init; }

    public sealed record HourlyData
    {
        public List<string>? Time { get; init; }
        public List<double?>? Pm2_5 { get; init; }
    }
}
