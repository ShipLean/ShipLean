using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Modules;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Backups;
using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Features.Backups.Cli;

// The restore verbs of the image (`decrypt-backup`, `restore-blobs`). Program.cs hands them the arguments before it
// builds the web host, so they never start the app: each builds a host with only the blob store and the cipher, and
// resolves from it without starting it. The passphrase comes from SHIPLEAN_BACKUP_PASSPHRASE or one line of input.
// Exit codes: 0 ok, 1 wrong passphrase, damaged file or store error (naming the file, never the passphrase), 2 usage.
public static class BackupCommands
{
    public const int Ok = 0;
    public const int Failed = 1;
    public const int Usage = 2;

    public static bool Handles(string verb) => verb is BackupCommandLine.DecryptVerb or BackupCommandLine.RestoreVerb;

    // Configuration from the environment, as the image runs it.
    public static Task<int> RunAsync(IReadOnlyList<string> args, TextReader input, TextWriter output, TextWriter error)
        => RunAsync(args, new ConfigurationBuilder().AddEnvironmentVariables().Build(), input, output, error);

    public static async Task<int> RunAsync(IReadOnlyList<string> args, IConfiguration configuration, TextReader input, TextWriter output, TextWriter error)
    {
        var line = BackupCommandLine.Parse(args, out var problem);
        if (line is null)
        {
            await error.WriteLineAsync(problem);
            await error.WriteLineAsync(BackupCommandLine.UsageText);
            return Usage;
        }

        var work = line.Verb == BackupCommandLine.DecryptVerb
            ? DecryptBackupCommand.Plan(line.In, line.Out!, out problem)
            : RestoreBlobsCommand.Plan(line.In, out problem);
        if (work is null)
        {
            await error.WriteLineAsync(problem);
            return Usage;
        }

        var passphrase = await BackupPassphrase.ReadAsync(configuration, input, error);
        if (passphrase is null)
        {
            await error.WriteLineAsync($"No passphrase: set {BackupPassphrase.Key} or give it as one line on standard input.");
            return Usage;
        }

        using var host = Build(configuration);
        var cipher = host.Services.GetRequiredService<IBackupCipher>();
        if (line.Verb == BackupCommandLine.DecryptVerb)
        {
            return await new DecryptBackupCommand(cipher, output, error).RunAsync(work, passphrase, CancellationToken.None);
        }

        var restore = new RestoreBlobsCommand(
            cipher,
            host.Services.GetRequiredService<BackupTempFiles>(),
            host.Services.GetRequiredService<BlobStoreChoice>(),
            () => host.Services.GetRequiredService<IBlobStore>(),
            configuration,
            output,
            error);
        return await restore.RunAsync(work, line.Overwrite, passphrase, CancellationToken.None);
    }

    // Only what the verbs need: the blob store SHIPLEAN_BLOB_* select, the cipher and the temp files. Built, never started.
    private static IHost Build(IConfiguration configuration)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            DisableDefaults = true,
        });
        builder.Configuration.AddConfiguration(configuration);
        new BlobStoreServices().AddServices(builder.Services, builder.Configuration);
        new BackupCipherServices().AddServices(builder.Services, builder.Configuration);
        builder.Services.AddSingleton<BackupTempFiles>();
        return builder.Build();
    }
}
