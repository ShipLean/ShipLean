using ShipLean.Application.Features.Backups.Crypto;
using ShipLean.Contracts.Backups;
using ShipLean.TestSupport.Backups;

namespace ShipLean.WebApi.Tests.Backups.Crypto;

[TestFixture]
public class AesGcmCipherContractTests : CipherContract
{
    protected override IBackupCipher Create() => new AesGcmBackupCipher();
}
