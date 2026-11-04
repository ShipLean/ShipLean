using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Storage;
using ShipLean.LocalEnvironment.Database;
using ShipLean.LocalEnvironment.Hosting;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// The attachment limits through a real host: AppHost applies its settings after the modules register, so only a read at resolve time sees them.
[TestFixture]
public class AttachmentLimitsHostTests
{
    private ClonedDatabase _database = null!;

    [OneTimeSetUp]
    public async Task CloneDatabase() => _database = await ShipLeanDatabase.CloneSeedAsync();

    [OneTimeTearDown]
    public async Task DropDatabase() => await _database.DisposeAsync();

    [Test]
    public async Task Start_WhenLimitSettingsGiven_ShouldResolveThem()
    {
        var settings = new Dictionary<string, string?>
        {
            [AttachmentLimits.ConfigKey] = "1234",
            [AttachmentLimits.ItemConfigKey] = "9000",
            [AttachmentLimits.DemoConfigKey] = "5678",
        };

        await using var host = AppHost.Start(new AppHostOptions { ConnectionString = _database.ConnectionString, Settings = settings });

        var limits = host.Services.GetRequiredService<AttachmentLimits>();
        limits.MaxBytes.ShouldBe(1234);
        limits.MaxItemBytes.ShouldBe(9000);
        limits.MaxDemoBytes.ShouldBe(5678);
    }

    [Test]
    public async Task Start_WhenNoLimitSettings_ShouldResolveTheDefaults()
    {
        await using var host = AppHost.Start(new AppHostOptions { ConnectionString = _database.ConnectionString });

        host.Services.GetRequiredService<AttachmentLimits>().ShouldBe(new AttachmentLimits(AttachmentLimits.DefaultMaxBytes));
    }

    [Test]
    public async Task Start_WhenDemoLimitIsAboveTheItemLimit_ShouldFailNamingTheSetting()
    {
        var settings = new Dictionary<string, string?> { [AttachmentLimits.DemoConfigKey] = "2000", [AttachmentLimits.ItemConfigKey] = "1000" };

        var error = await Should.ThrowAsync<Exception>(() => Task.Run(() => AppHost.Start(new AppHostOptions { ConnectionString = _database.ConnectionString, Settings = settings })));

        error.ToString().ShouldContain("SHIPLEAN_DEMO_MAX_BYTES");
    }
}
