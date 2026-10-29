using Dapper;
using System.Data.Common;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core.Sprints;

namespace ShipLean.Application.Features.WorkItems.Rules;

// A story or bug with no sprint that a person moves from a Proposed state into an In Progress state goes into the sprint running today (UTC),
// when there is one. A write with no person behind it (a webhook) and the bulk moves of a settings save or rollover don't count as starting work.
internal static class SprintRule
{
    public static async Task<ProposedChange> ApplyAsync(
        WorkItemSnapshot before, ProposedChange change, Guid? actorId, DateOnly today, DbTransaction transaction, IReadOnlyDictionary<Guid, RuleState> states, List<string> notices, CancellationToken cancellationToken)
    {
        if (actorId is null || change.Bulk || change.Type is not (WorkItemTypeId.Story or WorkItemTypeId.Bug) || before.SprintId is not null || change.SprintId is not null || before.StateId == change.StateId)
        {
            return change;
        }

        var after = states.TryGetValue(change.StateId, out var target) ? target : (await RuleReads.StatesAsync(transaction, [change.StateId], cancellationToken))[change.StateId];
        if (states[before.StateId].Category != "proposed" || after.Category != "inProgress")
        {
            return change;
        }

        var sprintId = await transaction.Connection!.QueryFirstOrDefaultAsync<Guid?>(new CommandDefinition(
            $"SELECT s.id FROM sprint s WHERE s.project_id = @projectId AND {CurrentSprintSql.Where} ORDER BY s.start_date LIMIT 1",
            new { projectId = change.ProjectId, today }, transaction, cancellationToken: cancellationToken));
        if (sprintId is null)
        {
            return change;
        }

        notices.Add(WorkItemRules.SPRINTED);
        return change with { SprintId = sprintId };
    }
}
