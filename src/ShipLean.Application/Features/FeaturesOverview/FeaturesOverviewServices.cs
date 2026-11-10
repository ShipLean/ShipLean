using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.FeaturesOverview;

public sealed class FeaturesOverviewServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IFeaturesOverview, FeaturesOverview>();
        services.AddScoped<IFeatureDigest, FeatureDigest>();
    }
}
