using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ShipLean.Application.Features.Demos.YouTube;

// Reads the YouTube environment pair once at host start, so half a pair fails startup naming the setting.
public sealed class YouTubeClientEnvStartup(IConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        YouTubeClientEnv.Read(configuration);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
