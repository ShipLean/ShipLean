using Microsoft.Extensions.Configuration;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Backups;
using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Features.Backups.Cli;

// restore-blobs: each attachment object is decrypted whole into a BackupTempFiles file before anything reaches the blob
// store, then its envelope gives the key (checked against the blob key rules) and content type, and the content is put
// under that key, unless the store already holds the key and --overwrite isn't given. It stops at the first object
// that fails (wrong passphrase, damaged object or store error); the ones before it stay put, and a second run skips them.
public sealed class RestoreBlobsCommand(
    IBackupCipher cipher,
    BackupTempFiles tempFiles,
    BlobStoreChoice choice,
    Func<IBlobStore> openStore,
    IConfiguration configuration,
    TextWriter output,
    TextWriter error)
{
    // The objects to restore, or null with a usage problem.
    public static IReadOnlyList<CliFile>? Plan(string inPath, out string problem)
    {
        problem = "";
        if (!Directory.Exists(inPath))
        {
            problem = $"restore-blobs: --in {inPath} is not a folder.";
            return null;
        }

        var sources = CliPaths.EncryptedFiles(inPath);
        if (sources.Count == 0)
        {
            problem = $"restore-blobs: there are no {CliPaths.EncryptedSuffix} files under {inPath}.";
            return null;
        }

        return sources.Select(source => new CliFile(source, null)).ToList();
    }

    public async Task<int> RunAsync(IReadOnlyList<CliFile> files, bool overwrite, string passphrase, CancellationToken cancellationToken)
    {
        await output.WriteLineAsync($"Blob store: {choice.Kind}, chosen by {choice.Setting} ({choice.Description})");
        if (choice.Warning is not null)
        {
            await error.WriteLineAsync(choice.Warning);
        }

        IBlobStore store;
        try
        {
            // As at startup: a Postgres blob store gets its table, which a database of its own may not have yet.
            if (choice.Kind == BlobStoreKind.Postgres && configuration.GetValue(DatabaseServices.MigrateKey, true))
            {
                PostgresBlobStore.ApplySchema(DatabaseServices.PinUtc(configuration[BlobStoreServices.DbConnectionKey]!));
            }

            store = openStore();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await error.WriteLineAsync($"restore-blobs: the blob store could not be opened: {ex.GetType().Name}: {ex.Message}");
            return BackupCommands.Failed;
        }

        var put = 0;
        var skipped = 0;
        for (var i = 0; i < files.Count; i++)
        {
            var source = files[i].Source;
            var (wasPut, outcome, problem) = await RestoreAsync(store, source, overwrite, passphrase, cancellationToken);
            if (problem is not null)
            {
                await error.WriteLineAsync($"restore-blobs: {source}: {problem}");
                await error.WriteLineAsync($"Stopped there: {put} put and {skipped} skipped of {files.Count} objects before it.");
                return BackupCommands.Failed;
            }

            await output.WriteLineAsync(outcome);
            if (wasPut)
            {
                put++;
            }
            else
            {
                skipped++;
            }
        }

        await output.WriteLineAsync($"Restored {put} blob(s); skipped {skipped} the store already held.");
        return BackupCommands.Ok;
    }

    // Whether it was put and a line saying what happened, or what went wrong (never the passphrase).
    private async Task<(bool Put, string Outcome, string? Problem)> RestoreAsync(IBlobStore store, string source, bool overwrite, string passphrase, CancellationToken cancellationToken)
    {
        await using var plain = tempFiles.Create();
        try
        {
            await using var encrypted = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
            await cipher.DecryptAsync(encrypted, plain, passphrase, cancellationToken);
        }
        catch (BackupDecryptException ex)
        {
            return (false, "", ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (false, "", ex.Message);
        }

        plain.Position = 0;
        string key;
        string contentType;
        Stream content;
        try
        {
            (key, contentType, content) = await BlobEnvelope.ReadAsync(plain, cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            return (false, "", ex.Message);
        }

        try
        {
            if (!overwrite && await store.ExistsAsync(key, cancellationToken))
            {
                return (false, $"skipped {key}: the blob store already holds it (--overwrite replaces it)", null);
            }

            var bytes = await store.PutAsync(key, content, contentType, cancellationToken);
            return (true, $"put {key} ({contentType}, {bytes} bytes)", null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, "", $"blob store error for {key}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
