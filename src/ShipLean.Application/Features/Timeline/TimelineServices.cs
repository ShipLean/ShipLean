using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.Timeline;

public sealed class TimelineServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IFeatureTimeline, FeatureTimeline>();
        services.AddSingleton<ITimelineFactsReader, TimelineFactsReader>();
    }
}
