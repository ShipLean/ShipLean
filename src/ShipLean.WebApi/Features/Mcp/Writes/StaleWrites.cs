using Agoda.IoC.Core;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Features.Mcp.Writes;

// The stale-write rule every MCP tool that patches one item shares (#13 principle 6). With the revision the caller supplied, a
// stale write is a conflict. Without one the tool read the revision itself, so on a stale write it retries once with the
// fresh revision, only when the other change touched none of the fields this call writes; otherwise it reports the conflict,
// naming who changed what.
[RegisterPerRequest]
public sealed class StaleWrites(IWorkItemStore store)
{
    public async Task<WriteResult> PatchAsync(Guid id, string key, WorkItemChanges changes, int revision, bool callerSuppliedRevision, CancellationToken cancellationToken)
        => await PatchAsync(id, key, changes, revision, callerSuppliedRevision, null, cancellationToken);

    // via: the MCP tool name a Feature status stamp records ("set_status").
    public async Task<WriteResult> PatchAsync(Guid id, string key, WorkItemChanges changes, int revision, bool callerSuppliedRevision, string? via, CancellationToken cancellationToken)
    {
        var options = WriteOptions.User with { Via = via };
        var outcome = await store.PatchAsync(id, new WorkItemPatch(revision, changes), options, cancellationToken);
        if (outcome.Written is { } written)
        {
            return written;
        }

        var conflict = outcome.Conflict!;
        var mine = Fields(changes);
        if (callerSuppliedRevision || conflict.TheirChanges.SelectMany(r => r.Changes).Any(c => mine.Contains(c.Field)))
        {
            throw ApiException.Conflict(Describe(key, conflict));
        }

        var retry = await store.PatchAsync(id, new WorkItemPatch(conflict.Current.Revision, changes), options, cancellationToken);
        return retry.Written ?? throw ApiException.Conflict(Describe(key, retry.Conflict!));
    }

    // The field ids a patch writes, as a revision records them.
    public static IReadOnlyList<string> Fields(WorkItemChanges changes)
    {
        var fields = new List<string>();
        void Add(bool set, string field)
        {
            if (set)
            {
                fields.Add(field);
            }
        }

        Add(changes.Title is not null, FieldIds.Title);
        Add(changes.State is not null, FieldIds.State);
        Add(changes.Assignee is not null, FieldIds.Assignee);
        Add(changes.Priority is not null, FieldIds.Priority);
        Add(changes.Points is not null, FieldIds.Points);
        Add(changes.Tags is not null, FieldIds.Tags);
        Add(changes.Sprint is not null, FieldIds.Sprint);
        Add(changes.Parent is not null, FieldIds.Parent);
        Add(changes.Severity is not null, FieldIds.Severity);
        Add(changes.Description is not null, FieldIds.Description);
        Add(changes.Goal is not null, FieldIds.Goal);
        Add(changes.StatusLine is not null, FieldIds.StatusLine);
        Add(changes.AcceptanceCriteria is not null, FieldIds.AcceptanceCriteria);
        Add(changes.ReproSteps is not null, FieldIds.ReproSteps);
        Add(changes.AddLinks.Count + changes.RemoveLinkIds.Count > 0, FieldIds.Links);
        return fields;
    }

    private static string Describe(string key, ConflictDto conflict)
    {
        var theirs = conflict.TheirChanges.Select(r =>
        {
            var who = r.ChangedBy?.Name ?? "Someone";
            var via = r.AgentName is { } agent ? $" via {agent}" : string.Empty;
            var what = string.Join(", ", r.Changes.Select(c => c.ToLabel is { } to ? $"{c.Field} to {to}" : c.Field));
            return $"{who}{via} changed {what}";
        });
        return $"{key} changed while this was being written (it is now at revision {conflict.Current.Revision}), so nothing was saved. {string.Join("; ", theirs)}. Look at {key} again and decide.";
    }
}
