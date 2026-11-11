using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Engine;

// Copies every blob not yet in the destination under prefix's attachments/g<generation>/ (BackupObjectKeys.Attachment),
// each one encrypted with passphrase in a BlobEnvelope. Incremental per generation (D5): a blob already there is
// skipped. A blob that is listed but can't be read fails the mirror.
public interface IAttachmentMirror
{
    Task<MirrorResult> MirrorAsync(IBackupDestination destination, string prefix, int generation, string passphrase, CancellationToken cancellationToken);
}
