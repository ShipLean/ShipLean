using Agoda.IoC.Core;
using Microsoft.Extensions.Configuration;

namespace ShipLean.Application.Features.Backups.Engine;

// Temporary files for backup work (the encrypted dump before upload, a decrypted object before restore). Each one is
// in SHIPLEAN_BACKUP_TEMP, or the system temp folder when that is unset, named shiplean-backup-<guid>.tmp, readable
// and writable by its owner only on Unix, and deleted when the stream closes.
[RegisterSingleton]
public sealed class BackupTempFiles(IConfiguration configuration)
{
    public const string TempKey = "SHIPLEAN_BACKUP_TEMP";

    public FileStream Create()
    {
        var configured = configuration[TempKey];
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.DeleteOnClose | FileOptions.Asynchronous,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        // A fresh random name, CreateNew and owner-only: safe in a shared temp folder, as S3BlobStore's buffer is.
        var name = $"shiplean-backup-{Guid.NewGuid():N}.tmp";
        return new FileStream(Path.Combine(string.IsNullOrWhiteSpace(configured) ? Path.GetTempPath() : configured, name), options);
    }
}
