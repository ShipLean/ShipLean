using Microsoft.Extensions.Hosting;

namespace ShipLean.WebApi.Tests.Harness;

// Remembers whether the host started and then disposed it, so a test can see what a failed start leaves behind.
public sealed class RecordingHostedService : IHostedService, IDisposable
{
    public bool Started { get; private set; }

    public bool Disposed { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => Disposed = true;
}
