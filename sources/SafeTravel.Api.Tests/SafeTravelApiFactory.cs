using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SafeTravel.Domain.Interfaces;
using NSubstitute;

namespace SafeTravel.Api.Tests;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Disables Hangfire background processing to avoid disposal issues.
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

            // Disable Hangfire background processing during tests
            // by removing and re-adding with 0 workers
            services.RemoveAll<IBackgroundJobClient>();
            services.AddSingleton<IBackgroundJobClient>(Substitute.For<IBackgroundJobClient>());
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Ensure clean shutdown
            try
            {
                var server = Services.GetService<BackgroundJobServer>();
                server?.SendStop();
                server?.Dispose();
            }
            catch
            {
                // Ignore disposal errors during test cleanup
            }
        }
        base.Dispose(disposing);
    }
}
