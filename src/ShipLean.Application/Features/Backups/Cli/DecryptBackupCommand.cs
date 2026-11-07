using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Cli;

// decrypt-backup: each encrypted file is decrypted into <target>.partial (created new, owner-only, refusing anything
// already at either path), which is renamed to the target, never over a file, only after DecryptAsync returns. On a
// failure the .partial is deleted, so a wrong passphrase or a damaged file leaves no output for it. A folder stops at
// the first file that fails; the ones before it stay published.
public sealed class DecryptBackupCommand(IBackupCipher cipher, TextWriter output, TextWriter error)
{
    // The files to decrypt with their targets, or null with a usage problem. Nothing is written.
    public static IReadOnlyList<CliFile>? Plan(string inPath, string outPath, out string problem)
    {
        problem = "";
        IReadOnlyList<CliFile> files;
        if (File.Exists(inPath))
        {
            files = [new CliFile(inPath, outPath)];
        }
        else if (Directory.Exists(inPath))
        {
            if (CliPaths.Taken(outPath) && !Directory.Exists(outPath))
            {
                problem = $"decrypt-backup: --out {outPath} is a file; give a folder for a folder of backups.";
                return null;
            }

            var sources = CliPaths.EncryptedFiles(inPath);
            if (sources.Count == 0)
            {
                problem = $"decrypt-backup: there are no {CliPaths.EncryptedSuffix} files under {inPath}.";
                return null;
            }

            files = sources
                .Select(source => new CliFile(source, Path.Combine(outPath, Path.GetRelativePath(inPath, source)[..^CliPaths.EncryptedSuffix.Length])))
                .ToList();
        }
        else
        {
            problem = $"decrypt-backup: --in {inPath} does not exist.";
            return null;
        }

        foreach (var file in files)
        {
            var target = file.Target!;
            if (Path.GetFileName(target).Length == 0)
            {
                problem = $"decrypt-backup: {file.Source} has no name before {CliPaths.EncryptedSuffix}.";
                return null;
            }

            var taken = new[] { target, target + CliPaths.PartialSuffix }.FirstOrDefault(CliPaths.Taken);
            if (taken is not null)
            {
                problem = $"decrypt-backup: {taken} already exists; nothing was written. Move it away and run again.";
                return null;
            }
        }

        return files;
    }

    public async Task<int> RunAsync(IReadOnlyList<CliFile> files, string passphrase, CancellationToken cancellationToken)
    {
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var problem = await DecryptAsync(file.Source, file.Target!, passphrase, cancellationToken);
            if (problem is not null)
            {
                await error.WriteLineAsync($"decrypt-backup: {file.Source}: {problem}");
                if (files.Count > 1)
                {
                    await error.WriteLineAsync($"Stopped there: {i} of {files.Count} files were decrypted before it.");
                }

                return BackupCommands.Failed;
            }

            await output.WriteLineAsync($"{file.Source} -> {file.Target}");
        }

        await output.WriteLineAsync($"Decrypted {files.Count} file(s).");
        return BackupCommands.Ok;
    }

    // Null when the target was published; otherwise what went wrong (never the passphrase).
    private async Task<string?> DecryptAsync(string source, string target, string passphrase, CancellationToken cancellationToken)
    {
        var partial = target + CliPaths.PartialSuffix;
        var created = false;
        var published = false;
        try
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(target));
            if (folder is not null)
            {
                Directory.CreateDirectory(folder);
            }

            await using (var encrypted = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous))
            await using (var plain = new FileStream(partial, PartialOptions()))
            {
                created = true;
                await cipher.DecryptAsync(encrypted, plain, passphrase, cancellationToken);
            }

            File.Move(partial, target, overwrite: false);
            published = true;
            return null;
        }
        catch (BackupDecryptException ex)
        {
            return ex.Message;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
        finally
        {
            if (created && !published)
            {
                File.Delete(partial);
            }
        }
    }

    // CreateNew refuses anything already at the path, a link included, so the plaintext never goes through one.
    private static FileStreamOptions PartialOptions()
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return options;
    }
}
