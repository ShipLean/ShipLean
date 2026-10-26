using System.Data.Common;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems.Rules;

// An unassigned story, bug or task that moves from a Proposed state into an In Progress state is taken by the person who moved it.
// A write with no person behind it (a webhook) takes nothing.
internal static class PullRule
{
    public static async Task<ProposedChange> ApplyAsync(
        WorkItemSnapshot before, ProposedChange change, Guid? actorId, DbTransaction transaction, IReadOnlyDictionary<Guid, RuleState> states, List<string> notices, CancellationToken cancellationToken)
    {
        if (actorId is null || change.Type is WorkItemTypeId.Feature or WorkItemTypeId.Epic || before.AssigneeId is not null || change.AssigneeId is not null || before.StateId == change.StateId)
        {
            return change;
        }

        var after = states.TryGetValue(change.StateId, out var target) ? target : (await RuleReads.StatesAsync(transaction, [change.StateId], cancellationToken))[change.StateId];
        if (states[before.StateId].Category != "proposed" || after.Category != "inProgress")
        {
            return change;
        }

        notices.Add(WorkItemRules.PULLED);
        return change with { AssigneeId = actorId };
    }
}
