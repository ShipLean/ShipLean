using System.Globalization;

namespace ShipLean.Application.Features.Backups.Engine;

// The object keys a backup writes, the bucket layout the runbook and restore rely on. Prefix is the destination's
// prefix exactly as configured (empty, or ending in "/"), put in front unchanged. Every key carries the passphrase
// generation that encrypted it.
public static class BackupObjectKeys
{
    // <prefix>database/shiplean-<yyyyMMddTHHmmssZ>-<first 8 hex of the run id>-g<generation>.dump.slbk, startedAt in UTC.
    public static string Database(string prefix, DateTimeOffset startedAt, Guid runId, int generation)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}database/shiplean-{startedAt.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}-{runId.ToString("N")[..8]}-g{generation}.dump.slbk");

    // <prefix>attachments/g<generation>/, the folder one generation's attachment objects live in.
    public static string AttachmentPrefix(string prefix, int generation)
        => string.Create(CultureInfo.InvariantCulture, $"{prefix}attachments/g{generation}/");

    // <prefix>attachments/g<generation>/<blob key>.slbk
    public static string Attachment(string prefix, int generation, string blobKey)
        => $"{AttachmentPrefix(prefix, generation)}{blobKey}.slbk";
}
