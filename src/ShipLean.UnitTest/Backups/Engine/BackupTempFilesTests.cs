using Microsoft.Extensions.Configuration;
using ShipLean.Application.Features.Backups.Engine;

namespace ShipLean.UnitTest.Backups.Engine;

[TestFixture]
public class BackupTempFilesTests
{
    private string _folder = null!;

    [SetUp]
    public void CreateFolder() => _folder = Directory.CreateTempSubdirectory("shiplean-backup-tests-").FullName;

    [TearDown]
    public void RemoveFolder() => Directory.Delete(_folder, recursive: true);

    [Test]
    public async Task TempFiles_WhenClosed_ShouldBeDeleted()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [BackupTempFiles.TempKey] = _folder })
            .Build();
        var file = new BackupTempFiles(configuration).Create();
        var path = file.Name;
        await file.WriteAsync(new byte[] { 1, 2, 3 });

        File.Exists(path).ShouldBeTrue();
        Path.GetDirectoryName(path).ShouldBe(_folder);
        Path.GetFileName(path).ShouldStartWith("shiplean-backup-");
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        await file.DisposeAsync();

        File.Exists(path).ShouldBeFalse();
    }
}
