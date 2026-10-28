using System.Data.Common;

namespace ShipLean.Contracts.WorkItems;

// InTransaction runs once per call (after every item of a PatchMany is written), inside the write's transaction; if it
// throws, the whole write is rolled back.
// BeforePatch runs once per Patch or PatchMany call, inside the store's transaction, after the store has taken its locks
// (the work item advisory lock, then the item rows) and checked every expected revision, and before any patch is written:
// a caller that must create something the patches refer to does it there. If it throws, or a later patch fails, the whole
// write is rolled back, and a stale batch never runs it. Create and Import don't run it.
// Origin is stamped on the revision. A Webhook write has no acting user: the revision and the links it adds have no
// author, the rules run with no actor, and Webhook names the pull or merge request on the revision. Via is the MCP tool
// name a Feature status stamp records ("set_status"); null from the app. Not a wire shape.
public sealed record WriteOptions(Func<DbTransaction, CancellationToken, Task>? InTransaction, RevisionOrigin Origin, Func<DbTransaction, CancellationToken, Task>? BeforePatch = null, string? Via = null)
{
    public static WriteOptions User { get; } = new(null, RevisionOrigin.User);

    public string? Webhook { get; private init; }

    // True for a write that moves many items at once on one person's behalf (settings save, sprint rollover, backlog move): its
    // revisions are marked bulk and never start an agent session.
    public bool Bulk { get; init; }

    // webhook: "GitHub acme/api#12".
    public static WriteOptions FromWebhook(string webhook, Func<DbTransaction, CancellationToken, Task>? inTransaction = null)
        => new(inTransaction, RevisionOrigin.Webhook) { Webhook = webhook };
}
