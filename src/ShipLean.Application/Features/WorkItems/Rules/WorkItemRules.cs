using System.Data.Common;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems.Rules;

// The rules the store applies inside each write: the pull rule, the sprint rule, column and state kept in step, and Card reordering.
// Reads only through the write's transaction; the project's write lock is already held.
public sealed class WorkItemRules(TimeProvider time) : IWorkItemRules
{
    // The notice the pull rule returns; the store marks the update event as pulled when it sees it.
    public const string PULLED = "pulled";

    // The notice the sprint rule returns: a story or bug started with no sprint went into the current one.
    public const string SPRINTED = "sprinted";

    public async Task<RuleOutcome> ApplyAsync(WorkItemSnapshot? before, ProposedChange change, Guid? actorId, DbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (before is null)
        {
            return new RuleOutcome(change, []);
        }

        var notices = new List<string>();
        var board = await RuleReads.BoardAsync(transaction, change.ProjectId, change.Type, cancellationToken);
        var states = await RuleReads.StatesAsync(transaction, [before.StateId, change.StateId], cancellationToken);
        var amended = change;
        if (board is not null)
        {
            amended = await ColumnStateSync.ApplyAsync(before, amended, board, states, transaction, notices, cancellationToken);
        }

        amended = await PullRule.ApplyAsync(before, amended, actorId, transaction, states, notices, cancellationToken);
        amended = await SprintRule.ApplyAsync(before, amended, actorId, DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime), transaction, states, notices, cancellationToken);
        if (board is not null)
        {
            amended = await CardReorder.ApplyAsync(before, change, amended, board, transaction, cancellationToken);
        }

        return new RuleOutcome(amended, notices);
    }
}
