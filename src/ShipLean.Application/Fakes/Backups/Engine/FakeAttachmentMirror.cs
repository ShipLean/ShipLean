using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Fakes.Backups.Engine;

// Stands in for the attachment mirror: copies nothing.
public sealed class FakeAttachmentMirror : IAttachmentMirror
{
    public Task<MirrorResult> MirrorAsync(IBackupDestination destination, string prefix, int generation, string passphrase, CancellationToken cancellationToken)
        => Task.FromResult(new MirrorResult(0, 0));
}
