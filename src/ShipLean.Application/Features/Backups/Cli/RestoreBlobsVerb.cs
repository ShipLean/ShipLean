using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts.Backups;
using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Features.Backups.Cli;

// restore-blobs --in <folder> [--overwrite]. Each object is decrypted fully into a temp file and its envelope read
// before anything is put, so a bad object puts nothing. Keys already in the store are skipped unless --overwrite.
internal sealed class RestoreBlobsVerb(IBackupCipher cipher, IBlobStore store, BackupTempFiles tempFiles)
{
    public async Task<int> RunAsync(CliOptions options, string passphrase, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var input = options.In!;
        if (!Directory.Exists(input))
        {
            await error.WriteLineAsync($"{input}: no such folder.");
            return 2;
        }

        var objects = CliFiles.ObjectsUnder(input);
        if (objects.Count == 0)
        {
            await error.WriteLineAsync($"{input}: no {CliFiles.Suffix} files found.");
            return 2;
        }

        int restored = 0, skipped = 0, failed = 0;
        foreach (var path in objects)
        {
            try
            {
                if (await RestoreOneAsync(path, passphrase, options.Overwrite, cancellationToken))
                {
                    restored++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception ex) when (ex is BackupDecryptException or InvalidDataException or IOException or UnauthorizedAccessException or BlobStoreRestoreException)
            {
                failed++;
                await error.WriteLineAsync($"{path}: {ex.Message}");
            }
        }

        await output.WriteLineAsync($"Restored {restored}, skipped {skipped} (already in the store), failed {failed}.");
        return failed == 0 ? 0 : 1;
    }

    // True when the blob was put, false when its key already existed and --overwrite was not given.
    private async Task<bool> RestoreOneAsync(string path, string passphrase, bool overwrite, CancellationToken cancellationToken)
    {
        await using var plain = tempFiles.Create();
        await using (var encrypted = File.OpenRead(path))
        {
            await cipher.DecryptAsync(encrypted, plain, passphrase, cancellationToken);
        }

        plain.Seek(0, SeekOrigin.Begin);
        var (key, contentType, content) = await BlobEnvelope.ReadAsync(plain, cancellationToken);
        try
        {
            if (!overwrite && await store.ExistsAsync(key, cancellationToken))
            {
                return false;
            }

            await store.PutAsync(key, content, contentType, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or BlobStoreRestoreException))
        {
            throw new BlobStoreRestoreException($"the blob store refused '{key}': {ex.GetType().Name}: {ex.Message}", ex);
        }
    }
}
