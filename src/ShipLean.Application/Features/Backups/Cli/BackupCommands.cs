using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Modules;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Backups;
using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Features.Backups.Cli;

// The restore verbs of the image (`decrypt-backup`, `restore-blobs`). Program.cs hands them the arguments before it
// builds the web host, so they never start the app: the services are built from the environment, resolved and used,
// and the host's StartAsync is never called. The passphrase comes from SHIPLEAN_BACKUP_PASSPHRASE, else one line of
// input. Exit codes: 0 ok, 1 wrong passphrase, damaged file or store error (the message names the file, never the
// passphrase), 2 usage.
public static class BackupCommands
{
    public const string PassphraseKey = "SHIPLEAN_BACKUP_PASSPHRASE";

    private const string Usage = """
        Usage:
          decrypt-backup --in <file|folder> --out <file|folder>
          restore-blobs --in <folder of attachment .slbk objects> [--overwrite]
        The passphrase is read from SHIPLEAN_BACKUP_PASSPHRASE, or from one line of standard input.
        restore-blobs puts into the blob store that SHIPLEAN_BLOB_* select.
        """;

    public static bool Handles(string verb) => verb is "decrypt-backup" or "restore-blobs";

    public static Task<int> RunAsync(IReadOnlyList<string> args, TextReader input, TextWriter output, TextWriter error)
        => RunAsync(args, input, output, error, new ConfigurationBuilder().AddEnvironmentVariables().Build());

    // configuration is the only source of settings: the host's appsettings and user secrets are never read.
    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextReader input, TextWriter output, TextWriter error, IConfiguration configuration)
    {
        var decrypt = args.Count > 0 && args[0] == "decrypt-backup";
        var (options, problem) = args.Count > 0 && Handles(args[0])
            ? CliOptions.Parse(args, takesOut: decrypt, takesOverwrite: !decrypt)
            : (null, "Unknown verb.");
        if (options is null)
        {
            await error.WriteLineAsync(problem);
            await error.WriteLineAsync(Usage);
            return 2;
        }

        using var host = Build(configuration);
        var passphrase = host.Services.GetRequiredService<IConfiguration>()[PassphraseKey];
        if (string.IsNullOrEmpty(passphrase))
        {
            passphrase = await input.ReadLineAsync();
        }

        if (string.IsNullOrEmpty(passphrase))
        {
            await error.WriteLineAsync($"No passphrase: set {PassphraseKey} or give one line on standard input.");
            return 2;
        }

        var cipher = host.Services.GetRequiredService<IBackupCipher>();
        return decrypt
            ? await new DecryptBackupVerb(cipher).RunAsync(options, passphrase, output, error, CancellationToken.None)
            : await RunRestoreAsync(host, cipher, options, passphrase, output, error);
    }

    // Only what the verbs need: the blob store, the cipher and the temp files. Build() never starts the hosted services.
    private static IHost Build(IConfiguration configuration)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.AddConfiguration(configuration);

        new BlobStoreServices().AddServices(builder.Services, builder.Configuration);
        new BackupCipherServices().AddServices(builder.Services, builder.Configuration);
        builder.Services.AddSingleton<BackupTempFiles>();
        return builder.Build();
    }

    private static async Task<int> RunRestoreAsync(IHost host, IBackupCipher cipher, CliOptions options, string passphrase, TextWriter output, TextWriter error)
    {
        IBlobStore store;
        try
        {
            store = host.Services.GetRequiredService<IBlobStore>();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            // These come from parsing the SHIPLEAN_BLOB_* settings; their messages name the setting, not its value.
            await error.WriteLineAsync($"blob store from SHIPLEAN_BLOB_* could not be opened: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }

        var tempFiles = host.Services.GetRequiredService<BackupTempFiles>();
        return await new RestoreBlobsVerb(cipher, store, tempFiles).RunAsync(options, passphrase, output, error, CancellationToken.None);
    }
}
