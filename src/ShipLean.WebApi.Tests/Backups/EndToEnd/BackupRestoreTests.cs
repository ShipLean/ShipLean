using System.Net;
using ShipLean.Contracts.Backups.Dtos;
using ShipLean.Contracts.Backups;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Backups.EndToEnd;

// SHIP-801 J1: a real backup restores, following docs/backups.md. The app, pg_dump, the cipher, RustFS, the mirror, the job
// and the worker are all real; the verbs run through BackupCommands.RunAsync and pg_restore as a process.
[TestFixture]
public class BackupRestoreTests : BackupRoundTripFixture
{
    private const string Passphrase = "correct horse battery staple";

    [Test]
    public async Task BackupRestore_WhenRunNowWithAttachments_ShouldRestoreSameData()
    {
        await CreateItemAsync("Created before the backup");
        var attachments = await UploadAttachmentsAsync();
        var settings = await SaveAsync(Passphrase);
        settings.AttachmentStore.InDatabaseBackup.ShouldBeFalse();

        var run = await RunNowAsync();

        run.State.ShouldBe(BackupRunState.Succeeded, run.Error);
        run.PassphraseGeneration.ShouldBe(1);
        var onDisk = BlobFiles(BlobRoot);
        onDisk.Count.ShouldBeGreaterThanOrEqualTo(attachments.Count);
        run.AttachmentsCopied.ShouldBe(onDisk.Count);
        run.DatabaseObjectKey.ShouldNotBeNull().ShouldStartWith($"{BackupPrefix}database/");
        (await ListAsync($"{BackupPrefix}attachments/g1/")).Count.ShouldBe(onDisk.Count);

        await using var connection = await OpenDatabaseAsync();
        await using var restored = await RestoreAsync(run.DatabaseObjectKey, 1, Passphrase);

        await using var target = await restored.Database.OpenAsync();
        foreach (var table in new[] { "work_item", "attachment", "app_user", "project", "backup_settings" })
        {
            (await CountAsync(target, table)).ShouldBe(await CountAsync(connection, table), table);
        }
        (await CountAsync(target, "work_item")).ShouldBeGreaterThan(0);
        ShouldHoldSameBlobs(BlobFiles(restored.BlobRoot), onDisk);
        foreach (var (dto, bytes) in attachments)
        {
            using var download = await restored.Api.GetAsync(dto.DownloadUrl);
            download.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await download.Content.ReadAsByteArrayAsync()).ShouldBe(bytes, dto.Name);
            download.Content.Headers.ContentType!.MediaType.ShouldBe(dto.ContentType, dto.Name);
        }
    }
}
