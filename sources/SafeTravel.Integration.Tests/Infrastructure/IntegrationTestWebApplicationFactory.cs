using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Infrastructure.Caching;
using SafeTravel.Infrastructure.ExternalApis;
using Testcontainers.Redis;
using WireMock.Server;

namespace SafeTravel.Integration.Tests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory that uses a real Redis container and WireMock for external APIs.
/// Implements IAsyncLifetime for proper container lifecycle management.
/// </summary>
public class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly RedisContainer _redisContainer = new RedisBuilder(image: "redis:7-alpine")
        .Build();

    public WireMockServer WireMockServer { get; private set; } = null!;

    public string RedisConnectionString => _redisContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();
        WireMockServer = WireMockServer.Start();
    }

    public new async Task DisposeAsync()
    {
        WireMockServer?.Stop();
        WireMockServer?.Dispose();
        await _redisContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing cache registration and add one with test Redis
            services.RemoveAll<IWeatherDataCache>();
            services.AddSingleton<IWeatherDataCache>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisWeatherDataCache>>();
                return new RedisWeatherDataCache(logger, RedisConnectionString);
            });

            // Remove existing OpenMeteo client and add one that uses WireMock
            services.RemoveAll<IOpenMeteoClient>();
            services.AddSingleton<IOpenMeteoClient>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OpenMeteoClient>>();
                var handler = new RedirectToWireMockHandler(WireMockServer.Urls[0]);
                var httpClient = new HttpClient(handler);
                return new OpenMeteoClient(httpClient, logger);
            });

            // Disable Hangfire background processing during tests
            services.RemoveAll<IBackgroundJobClient>();
            services.AddSingleton<IBackgroundJobClient>(NSubstitute.Substitute.For<IBackgroundJobClient>());
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
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

/// <summary>
/// Handler that redirects requests from the real Open-Meteo API to WireMock server.
/// </summary>
internal sealed class RedirectToWireMockHandler : DelegatingHandler
{
    private readonly string _wireMockBaseUrl;

    public RedirectToWireMockHandler(string wireMockBaseUrl)
    {
        _wireMockBaseUrl = wireMockBaseUrl.TrimEnd('/');
        InnerHandler = new HttpClientHandler();
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var originalUri = request.RequestUri!;
        var newUri = new Uri($"{_wireMockBaseUrl}{originalUri.PathAndQuery}");
        request.RequestUri = newUri;

        return base.SendAsync(request, cancellationToken);
    }
}
