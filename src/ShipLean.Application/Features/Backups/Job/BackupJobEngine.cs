using Agoda.IoC.Core;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Job;

// The engine pieces one backup run drives (contract C-ENGINE), resolved together for BackupJob.
[RegisterSingleton]
public sealed class BackupJobEngine(IBackupDestinationFactory destinations, IDatabaseDumper dumper, IBackupCipher cipher, IAttachmentMirror mirror)
{
    public IBackupDestinationFactory Destinations => destinations;

    public IDatabaseDumper Dumper => dumper;

    public IBackupCipher Cipher => cipher;

    public IAttachmentMirror Mirror => mirror;
}
