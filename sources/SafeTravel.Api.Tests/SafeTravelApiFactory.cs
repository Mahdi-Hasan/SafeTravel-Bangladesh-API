using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SafeTravel.Domain.Interfaces;
using NSubstitute;

namespace SafeTravel.Api.Tests;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// </summary>
public class SafeTravelApiFactory : WebApplicationFactory<Program>
{
    public IWeatherDataCache MockCache { get; } = Substitute.For<IWeatherDataCache>();
    public IOpenMeteoClient MockOpenMeteoClient { get; } = Substitute.For<IOpenMeteoClient>();
    public IDistrictRepository MockDistrictRepository { get; } = Substitute.For<IDistrictRepository>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real services with mocks for testing
            services.AddScoped(_ => MockCache);
            services.AddScoped(_ => MockOpenMeteoClient);
            services.AddScoped(_ => MockDistrictRepository);
        });

        builder.UseEnvironment("Testing");
    }
}
