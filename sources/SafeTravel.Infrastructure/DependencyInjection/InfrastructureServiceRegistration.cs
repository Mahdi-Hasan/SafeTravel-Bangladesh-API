using Microsoft.Extensions.DependencyInjection;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Infrastructure.DataProviders;

namespace SafeTravel.Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDistrictRepository, DistrictDataProvider>();

        return services;
    }
}
