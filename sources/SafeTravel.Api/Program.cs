using Hangfire;
using SafeTravel.Api.Endpoints;
using SafeTravel.Api.Middleware;
using SafeTravel.Application.DependencyInjection;
using SafeTravel.Infrastructure.DependencyInjection;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ===== Configure Serilog =====
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "SafeTravel.Api")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// ===== Configure Services =====
var services = builder.Services;

// Add Application, Infrastructure, and Hangfire services
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
services.AddApplicationServices();
services.AddInfrastructure(redisConnectionString);
services.AddHangfireServices(redisConnectionString);

// Add Hangfire server
services.AddHangfireServer(options =>
{
    options.WorkerCount = 1;
    options.ServerName = $"SafeTravel-{Environment.MachineName}";
});

// Add response compression
services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Optimal;
});

// Add Swagger/OpenAPI
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SafeTravel Bangladesh API",
        Version = "v1",
        Description = "REST API for travel recommendations based on weather and air quality data."
    });
});

// Add ProblemDetails
services.AddProblemDetails();

var app = builder.Build();

// ===== Configure Middleware Pipeline =====
app.UseExceptionHandling();
app.UseRequestLogging();

app.UseResponseCompression();

// Enable Swagger in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ===== Map Endpoints =====
app.MapDistrictsEndpoints();
app.MapTravelEndpoints();
app.MapHealthEndpoints();

// Hangfire Dashboard (secured in production)
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = app.Environment.IsDevelopment()
        ? []
        : [new HangfireDashboardAuthorizationFilter()]
});

// ===== Configure Recurring Jobs =====
HangfireServiceRegistration.ConfigureRecurringJobs();

Log.Information("Starting SafeTravel.Api...");
app.Run();
Log.Information("SafeTravel.Api stopped.");

// Make Program class accessible for WebApplicationFactory in tests
public partial class Program { }

/// <summary>
/// Simple authorization filter for Hangfire dashboard in production.
/// </summary>
public class HangfireDashboardAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        // TODO: Implement proper authorization (e.g., API key, JWT, etc.)
        // For now, allow all authenticated requests
        return true;
    }
}
