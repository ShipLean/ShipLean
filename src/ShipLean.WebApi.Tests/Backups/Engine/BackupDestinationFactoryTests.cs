using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Backups;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backups.Engine;

// The factory the app registers, whichever it is.
[TestFixture]
public class BackupDestinationFactoryTests : ApiFixture
{
    [Test]
    public void Factory_WhenProviderUnknown_ShouldThrow()
    {
        var factory = Host.Services.GetRequiredService<IBackupDestinationFactory>();
        var config = new BackupDestinationConfig("ftp", new S3DestinationConfig("eu-west-1", null, "backups", "", false, null, null));

        Should.Throw<BackupDestinationException>(() => factory.Create(config)).Message.ShouldContain("ftp");
    }

    // Destinations come from the factory only; IBackupDestination is [MultipleImplementations], so ServiceModuleTests
    // would not notice a registration.
    [Test]
    public void Destination_WhenHostBuilt_ShouldNotBeRegistered()
    {
        Host.Services.GetService<IBackupDestination>().ShouldBeNull();
    }
}
