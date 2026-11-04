using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.YouTube;

public sealed class YouTubeServices : IServiceModule
{
    public const string HttpClientName = "youtube";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<YouTubeEndpoints>();
        services.AddHttpClient(HttpClientName);
        services.AddScoped<IYouTubeClient, GoogleYouTubeClient>();
        services.AddScoped<IYouTubeSettings, DbYouTubeSettings>();
        services.AddScoped<IYouTubeConnect, YouTubeConnect>();
        services.AddScoped<IYouTubeCheck, YouTubeCheck>();
        services.AddScoped<IYouTubeConnections, YouTubeConnections>();
        services.AddScoped<IDemoYouTubeRetry, DemoYouTubeRetry>();
        services.AddHostedService<DemoYouTubePublisher>();
    }
}
