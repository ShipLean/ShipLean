using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Job;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backups.Job;

// The app registers the real pipeline, and everything it needs resolves.
[TestFixture]
public class BackupJobRegistrationTests : EmptyApiFixture
{
    [Test]
    public void Resolve_WhenHostBuilt_ShouldGiveRealJob()
    {
        var job = Host.Services.GetRequiredService<IBackupJob>();

        job.ShouldBeOfType<BackupJob>();
    }
}
