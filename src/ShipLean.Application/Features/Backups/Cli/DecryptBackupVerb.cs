using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Cli;

// decrypt-backup --in <file|folder> --out <file|folder>. Each output is written to <out>.partial and renamed into place
// only after the whole file decrypted, so a failure leaves nothing behind. Nothing is ever overwritten.
internal sealed class DecryptBackupVerb(IBackupCipher cipher)
{
    public async Task<int> RunAsync(CliOptions options, string passphrase, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var (jobs, problem) = Plan(options);
        if (jobs is null)
        {
            await error.WriteLineAsync(problem);
            return 2;
        }

        var failed = 0;
        foreach (var (source, target) in jobs)
        {
            var failure = await DecryptOneAsync(source, target, passphrase, cancellationToken);
            if (failure is null)
            {
                await output.WriteLineAsync($"Decrypted {source} -> {target}");
            }
            else
            {
                failed++;
                await error.WriteLineAsync($"{source}: {failure}");
            }
        }

        return failed == 0 ? 0 : 1;
    }

    private static (List<(string Source, string Target)>? Jobs, string? Problem) Plan(CliOptions options)
    {
        var input = options.In!;
        var output = options.Out!;
        var jobs = new List<(string, string)>();
        if (File.Exists(input))
        {
            var target = output;
            if (Directory.Exists(output))
            {
                if (!CliFiles.IsBackupObject(input))
                {
                    return (null, $"{input}: the name does not end in {CliFiles.Suffix}, so the output name is unclear; give --out as a file.");
                }

                target = Path.Combine(output, CliFiles.StripSuffix(Path.GetFileName(input)));
            }

            jobs.Add((input, target));
        }
        else if (Directory.Exists(input))
        {
            if (File.Exists(output))
            {
                return (null, $"{output}: --out must be a folder when --in is a folder.");
            }

            foreach (var source in CliFiles.ObjectsUnder(input))
            {
                jobs.Add((source, CliFiles.StripSuffix(Path.Combine(output, Path.GetRelativePath(input, source)))));
            }

            if (jobs.Count == 0)
            {
                return (null, $"{input}: no {CliFiles.Suffix} files found.");
            }
        }
        else
        {
            return (null, $"{input}: no such file or folder.");
        }

        foreach (var (_, target) in jobs)
        {
            if (CliFiles.Occupied(target) || CliFiles.Occupied(target + ".partial"))
            {
                return (null, $"{target}: already exists (or its .partial file does); nothing was written.");
            }
        }

        return (jobs, null);
    }

    // Null on success, otherwise what to tell the person (never the passphrase).
    private async Task<string?> DecryptOneAsync(string source, string target, string passphrase, CancellationToken cancellationToken)
    {
        var partial = target + ".partial";
        var created = false;
        var published = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
            await using (var encrypted = File.OpenRead(source))
            await using (var decrypted = CliFiles.CreateNewPrivate(partial))
            {
                created = true;
                await cipher.DecryptAsync(encrypted, decrypted, passphrase, cancellationToken);
            }

            File.Move(partial, target, overwrite: false);
            published = true;
            return null;
        }
        catch (Exception ex) when (ex is BackupDecryptException or IOException or UnauthorizedAccessException)
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
}
