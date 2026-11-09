using ShipLean.Contracts.Backups.Dtos;

namespace ShipLean.WebApi.Tests.Backups.EndToEnd;

// SHIP-801 J1: a dump made before a passphrase change restores with its own generation's passphrase and attachments.
[TestFixture]
public class BackupPassphraseChangeTests : BackupRoundTripFixture
{
    private const string OldPassphrase = "the first passphrase of 2026";
    private const string NewPassphrase = "a second and different passphrase";

    [Test]
    public async Task BackupRestore_WhenPassphraseChangedAfterBackup_ShouldRestoreOlderDumpWithItsGeneration()
    {
        var older = await UploadAttachmentsAsync();
        await SaveAsync(OldPassphrase);
        var first = await RunNowAsync();
        await using var connection = await OpenDatabaseAsync();
        var itemsAtFirstBackup = await CountAsync(connection, "work_item");
        var blobsAtFirstBackup = BlobFiles(BlobRoot);

        await CreateItemAsync("Created after the first backup");
        await UploadAttachmentsAsync("-later");
        var changed = await SaveAsync(NewPassphrase);
        var second = await RunNowAsync();

        changed.PassphraseGeneration.ShouldBe(2);
        first.State.ShouldBe(BackupRunState.Succeeded, first.Error);
        first.PassphraseGeneration.ShouldBe(1);
        second.State.ShouldBe(BackupRunState.Succeeded, second.Error);
        second.PassphraseGeneration.ShouldBe(2);
        first.DatabaseObjectKey.ShouldNotBeNull().ShouldContain("-g1.");
        second.DatabaseObjectKey.ShouldNotBeNull().ShouldContain("-g2.");
        (await ListAsync($"{BackupPrefix}attachments/g1/")).Count.ShouldBe(blobsAtFirstBackup.Count);
        (await ListAsync($"{BackupPrefix}attachments/g2/")).Count.ShouldBe(BlobFiles(BlobRoot).Count);

        // The newer passphrase does not open the older dump.
        var downloaded = await DownloadAsync(first.DatabaseObjectKey, Path.Combine(Scratch, "wrong"));
        var wrong = await RestoreRunbook.DecryptAsync(downloaded, Path.Combine(Scratch, "wrong", "out.dump"), NewPassphrase);
        wrong.ExitCode.ShouldBe(1);
        File.Exists(Path.Combine(Scratch, "wrong", "out.dump")).ShouldBeFalse();

        await using var restored = await RestoreAsync(first.DatabaseObjectKey, 1, OldPassphrase);

        await using var target = await restored.Database.OpenAsync();
        (await CountAsync(target, "work_item")).ShouldBe(itemsAtFirstBackup);
        (await CountAsync(connection, "work_item")).ShouldBe(itemsAtFirstBackup + 1);
        ShouldHoldSameBlobs(BlobFiles(restored.BlobRoot), blobsAtFirstBackup);
        foreach (var (dto, bytes) in older)
        {
            using var download = await restored.Api.GetAsync(dto.DownloadUrl);
            (await download.Content.ReadAsByteArrayAsync()).ShouldBe(bytes, dto.Name);
            download.Content.Headers.ContentType!.MediaType.ShouldBe(dto.ContentType, dto.Name);
        }
    }
}
