using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Runs;

namespace ShipLean.WebApi.Tests.Backups.Schedule;

// The worker's scope factory, by hand: each scope holds one ProbeJob, which it disposes with the scope.
public sealed class ProbeScopes(JobProbe probe, IBackupRunLog runLog) : IServiceScopeFactory
{
    public IServiceScope CreateScope() => new ProbeScope(new ProbeJob(probe, runLog));
}
