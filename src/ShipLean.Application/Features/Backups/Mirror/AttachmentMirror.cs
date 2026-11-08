using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts.Backups;
using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Features.Backups.Mirror;

// Copies the blobs the inventory lists that this generation's prefix doesn't hold yet, one at a time: each is wrapped
// in a BlobEnvelope, encrypted into a temporary file and uploaded, and the file is deleted when it closes. A listed
// key that can't be read, or a destination that fails, throws and fails the run.
public sealed class AttachmentMirror(IBlobInventory inventory, IBlobStore blobs, IBackupCipher cipher, BackupTempFiles tempFiles) : IAttachmentMirror
{
    public async Task<MirrorResult> MirrorAsync(IBackupDestination destination, string prefix, int generation, string passphrase, CancellationToken cancellationToken)
    {
        var copied = 0;
        long bytes = 0;
        await foreach (var key in inventory.ListKeysAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var objectKey = BackupObjectKeys.Attachment(prefix, generation, key);
            if (await destination.ExistsAsync(objectKey, cancellationToken))
            {
                continue;
            }

            bytes += await CopyAsync(destination, objectKey, key, passphrase, cancellationToken);
            copied++;
        }

        return new MirrorResult(copied, bytes);
    }

    private async Task<long> CopyAsync(IBackupDestination destination, string objectKey, string key, string passphrase, CancellationToken cancellationToken)
    {
        var blob = await blobs.GetAsync(key, cancellationToken)
            ?? throw new InvalidOperationException($"Blob '{key}' is listed but can't be read.");
        await using var content = blob.Content;
        await using var temp = tempFiles.Create();
        await using (var encryptor = cipher.OpenEncryptor(temp, passphrase))
        {
            await BlobEnvelope.WriteAsync(encryptor, key, blob.ContentType, content, cancellationToken);
        }

        var size = temp.Length;
        temp.Position = 0;
        await destination.PutAsync(objectKey, temp, cancellationToken);
        return size;
    }
}
