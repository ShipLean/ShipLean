using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Crypto;
using ShipLean.Contracts.Backups;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backups.Crypto;

[TestFixture]
public class BackupCipherRegistrationTests : ApiFixture
{
    [Test]
    public void Cipher_WhenHostBuilt_ShouldBeAesGcm()
    {
        Host.Services.GetRequiredService<IBackupCipher>().ShouldBeOfType<AesGcmBackupCipher>();
    }
}
