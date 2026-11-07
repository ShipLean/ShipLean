using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Runs;

namespace ShipLean.WebApi.Tests.Backups.Schedule;

// One worker scope: it resolves IBackupJob to its ProbeJob and nothing else, and disposes the job with itself.
public sealed class ProbeScope(ProbeJob job) : IServiceScope, IServiceProvider, IAsyncDisposable
{
    public IServiceProvider ServiceProvider => this;

    public object? GetService(Type serviceType) => serviceType == typeof(IBackupJob) ? job : null;

    public ValueTask DisposeAsync() => job.DisposeAsync();

    public void Dispose() => job.Dispose();
}
