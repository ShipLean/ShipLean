using System.Data.Common;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.States;

public sealed partial class StateCatalog
{
    // Locks the project row (serialising state editors), checks every change against the live states, then writes: items
    // of a removed state move to its target (a caller that wants revisions moves them through IWorkItemStore first, in
    // this same transaction, and finds none left), the state is archived, renames go through a temporary name so two
    // states can swap names, then adds and orders.
    public async Task ApplyAsync(Guid projectId, StateChanges changes, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection!;
        var locked = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM (SELECT id FROM project WHERE id = @projectId FOR NO KEY UPDATE) p)", new { projectId }, transaction, cancellationToken: cancellationToken));
        if (!locked)
        {
            throw ApiException.NotFound("There is no such project.");
        }

        var live = (await connection.QueryAsync<StateRow>(new CommandDefinition(
            $"SELECT {StateRow.Columns} FROM state s WHERE s.project_id = @projectId AND s.archived_at IS NULL ORDER BY s.sort_order, s.name",
            new { projectId }, transaction, cancellationToken: cancellationToken)))
            .Select(r => new StateDraft(r.Id, r.TypeId, r.Name, r.CategoryId, r.SortOrder, isNew: false)).ToList();
        var plan = await PlanAsync(connection, transaction, live, changes, cancellationToken);
        await WriteAsync(connection, transaction, projectId, plan, cancellationToken);
    }

    private static async Task<StatePlan> PlanAsync(DbConnection connection, DbTransaction transaction, List<StateDraft> live, StateChanges changes, CancellationToken cancellationToken)
    {
        var drafts = new List<StateDraft>(live);
        var touched = new HashSet<WorkItemTypeId>();
        var renumber = new HashSet<WorkItemTypeId>();
        var added = changes.Add ?? [];
        var addIds = added.Select(a => a.Id).ToArray();
        var used = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM state WHERE id = ANY(@addIds))", new { addIds }, transaction, cancellationToken: cancellationToken));
        if (used || addIds.Distinct().Count() != addIds.Length)
        {
            throw ApiException.Validation("A new state needs an id that is not used yet.");
        }

        foreach (var add in added)
        {
            if (string.IsNullOrWhiteSpace(add.Name))
            {
                throw ApiException.Validation("A state needs a name.");
            }

            var order = drafts.Where(d => d.Type == add.Type).Select(d => d.Order).DefaultIfEmpty(0).Max() + 1;
            drafts.Add(new StateDraft(add.Id, add.Type, add.Name.Trim(), add.Category, order, isNew: true));
            touched.Add(add.Type);
            renumber.Add(add.Type);
        }

        foreach (var update in changes.Update ?? [])
        {
            if (string.IsNullOrWhiteSpace(update.Name))
            {
                throw ApiException.Validation("A state needs a name.");
            }

            var draft = Find(drafts, update.StateId);
            draft.Name = update.Name.Trim();
            draft.Category = update.Category;
            touched.Add(draft.Type);
        }

        var orders = new Dictionary<WorkItemTypeId, List<Guid>>();
        foreach (var reorder in changes.Reorder ?? [])
        {
            var ids = reorder.StateIds ?? [];
            if (ids.Distinct().Count() != ids.Count || ids.Any(id => Find(drafts, id).Type != reorder.Type) || orders.ContainsKey(reorder.Type))
            {
                throw ApiException.Validation("A reorder lists each state of one type once.");
            }

            orders[reorder.Type] = [.. ids];
            touched.Add(reorder.Type);
            renumber.Add(reorder.Type);
        }

        var removals = new List<(StateDraft State, StateDraft Target)>();
        foreach (var remove in changes.Remove ?? [])
        {
            var state = Find(drafts, remove.StateId);
            var target = Find(drafts, remove.TargetStateId);
            if (target.Type != state.Type || target.Id == state.Id || removals.Any(r => r.State == state))
            {
                throw ApiException.Validation("Items must move to another state of the same type.");
            }

            removals.Add((state, target));
            touched.Add(state.Type);
            renumber.Add(state.Type);
        }

        if (removals.Any(r => removals.Any(o => o.State == r.Target)))
        {
            throw ApiException.Validation("Items must move to a state that stays.");
        }

        var removed = removals.Select(r => r.State).ToHashSet();
        var remaining = drafts.Where(d => !removed.Contains(d)).ToList();
        foreach (var type in touched)
        {
            Check(remaining.Where(d => d.Type == type).ToList());
        }

        foreach (var type in renumber)
        {
            var wanted = orders.GetValueOrDefault(type) ?? [];
            var ofType = remaining.Where(d => d.Type == type).ToList();
            var sequence = wanted.Select(id => ofType.FirstOrDefault(d => d.Id == id)).OfType<StateDraft>()
                .Concat(ofType.Where(d => !wanted.Contains(d.Id)).OrderBy(d => d.Order));
            var position = 1;
            foreach (var draft in sequence)
            {
                draft.Order = position++;
            }
        }

        return new StatePlan(remaining, removals);
    }

    private static StateDraft Find(List<StateDraft> drafts, Guid id)
        => drafts.FirstOrDefault(d => d.Id == id) ?? throw ApiException.NotFound("There is no such state.");

    private static void Check(List<StateDraft> states)
    {
        if (states.Select(s => s.Name.ToUpperInvariant()).Distinct().Count() != states.Count)
        {
            throw ApiException.Validation("A state needs a name that is not already used by its type.");
        }

        var missing = new[] { StateCategory.Proposed, StateCategory.Completed }.Where(c => states.All(s => s.Category != c)).ToList();
        if (missing.Count > 0)
        {
            throw ApiException.Validation($"A type needs at least one {missing[0]} state.");
        }
    }

    private static async Task WriteAsync(DbConnection connection, DbTransaction transaction, Guid projectId, StatePlan plan, CancellationToken cancellationToken)
    {
        foreach (var (state, target) in plan.Removals)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE work_item SET state_id = @target, state_changed_at = now() WHERE state_id = @state",
                new { state = state.Id, target = target.Id }, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE state SET archived_at = now(), updated_at = now() WHERE id = @id", new { id = state.Id }, transaction, cancellationToken: cancellationToken));
        }

        var renamed = plan.Remaining.Where(d => d.Renamed).ToList();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE state SET name = id::text WHERE id = ANY(@ids)", new { ids = renamed.Select(d => d.Id).ToArray() }, transaction, cancellationToken: cancellationToken));
        foreach (var draft in plan.Remaining.Where(d => d.IsNew))
        {
            await InsertAsync(connection, transaction, draft.Id, projectId, draft.Type, draft.Name, draft.Category, draft.Order, cancellationToken);
        }

        foreach (var draft in plan.Remaining.Where(d => d.Changed))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE state SET name = @name, category = @category, sort_order = @order, updated_at = now() WHERE id = @id",
                new { id = draft.Id, name = draft.Name, category = StateRow.CategoryText(draft.Category), order = draft.Order }, transaction, cancellationToken: cancellationToken));
        }
    }
}
