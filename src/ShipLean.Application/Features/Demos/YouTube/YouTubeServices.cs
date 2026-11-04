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
        // No redirects (the bearer token goes only where the endpoint says), and no overall timeout: an upload can take long, so
        // GoogleYouTubeClient sets a limit per call.
        services.AddHttpClient(HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddScoped<IYouTubeClient, GoogleYouTubeClient>();
        services.AddScoped<IYouTubeSettings, DbYouTubeSettings>();
        services.AddScoped<IYouTubeConnect, YouTubeConnect>();
        services.AddScoped<IYouTubeCheck, YouTubeCheck>();
        services.AddScoped<IYouTubeConnections, YouTubeConnections>();
        services.AddScoped<IDemoYouTubeRetry, DemoYouTubeRetry>();
        services.AddHostedService<YouTubeClientEnvStartup>();
        services.AddHostedService<DemoYouTubePublisher>();
    }
}
