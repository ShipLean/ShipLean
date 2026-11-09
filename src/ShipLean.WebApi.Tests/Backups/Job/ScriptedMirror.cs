using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts.Backups;

namespace ShipLean.WebApi.Tests.Backups.Job;

// Records each mirror call and answers Result, after running the test's step when one is set.
internal sealed class ScriptedMirror : IAttachmentMirror
{
    private readonly List<(IBackupDestination Destination, string Prefix, int Generation, string Passphrase)> _calls = [];

    public IReadOnlyList<(IBackupDestination Destination, string Prefix, int Generation, string Passphrase)> Calls => _calls;

    public MirrorResult Result { get; set; } = new(0, 0);

    public Func<Task>? Step { get; set; }

    public async Task<MirrorResult> MirrorAsync(IBackupDestination destination, string prefix, int generation, string passphrase, CancellationToken cancellationToken)
    {
        _calls.Add((destination, prefix, generation, passphrase));
        if (Step is { } step)
        {
            await step();
        }
        return Result;
    }
}
