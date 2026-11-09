using ShipLean.Application.Fakes.Backups.Engine;
using ShipLean.Contracts.Backups;
using ShipLean.TestSupport.Backups;

namespace ShipLean.WebApi.Tests.Backups.Engine;

[TestFixture]
public class PassThroughCipherContractTests : CipherContract
{
    protected override IBackupCipher Create() => new PassThroughBackupCipher();
}
