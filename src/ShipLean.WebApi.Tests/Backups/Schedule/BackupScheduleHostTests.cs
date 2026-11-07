using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Features.Backups.Schedule;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backups.Schedule;

// Test hosts never run a backup on a schedule, but keep the worker so Back up now runs there.
[TestFixture]
public class BackupScheduleHostTests : ApiFixture
{
    [Test]
    public void Host_WhenTestHost_ShouldNotRunSchedule()
    {
        var hosted = Host.Services.GetServices<IHostedService>().ToList();

        hosted.OfType<BackupScheduleService>().ShouldBeEmpty();
        hosted.OfType<BackupWorker>().ShouldHaveSingleItem();
    }
}
