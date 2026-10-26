using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.WorkItems.History;

// The FieldChange list between two views, in the canonical field order (field-ids.json).
public static class WorkItemFieldDiff
{
    public static IReadOnlyList<FieldChange> Between(WorkItemFieldView before, WorkItemFieldView after, WorkItemFieldLabels labels)
    {
        var changes = new List<FieldChange>();
        Add(changes, "title", before.Title, after.Title);
        Add(changes, "type", before.Type, after.Type);
        Add(changes, "state", before.State, after.State, labels.State);
        Add(changes, "assignee", before.Assignee, after.Assignee, labels.User);
        Add(changes, "priority", before.Priority, after.Priority);
        Add(changes, "points", before.Points, after.Points);
        Add(changes, "tags", before.Tags, after.Tags);
        Add(changes, "sprint", before.Sprint, after.Sprint, labels.Sprint);
        Add(changes, "parent", before.Parent, after.Parent, labels.Item);
        Add(changes, "severity", before.Severity, after.Severity);
        Add(changes, "description", before.Description, after.Description);
        Add(changes, "acceptanceCriteria", before.AcceptanceCriteria, after.AcceptanceCriteria);
        Add(changes, "reproSteps", before.ReproSteps, after.ReproSteps);
        Add(changes, "goal", before.Goal, after.Goal);
        Add(changes, "statusLine", before.StatusLine, after.StatusLine);
        Add(changes, "statusVerifiedAt", before.StatusVerifiedAt, after.StatusVerifiedAt, instant: labels.Instant);
        Add(changes, "links", before.Links, after.Links);
        return changes;
    }

    private static void Add<T>(
        List<FieldChange> changes, string field, T from, T to, Func<Guid, string?>? label = null, Func<DateTimeOffset, string>? instant = null)
    {
        var fromNode = JsonSerializer.SerializeToNode(from, PersistedJson.Options);
        var toNode = JsonSerializer.SerializeToNode(to, PersistedJson.Options);
        if (JsonNode.DeepEquals(fromNode, toNode))
        {
            return;
        }

        changes.Add(new FieldChange(
            field,
            JsonSerializer.SerializeToElement(from, PersistedJson.Options),
            JsonSerializer.SerializeToElement(to, PersistedJson.Options),
            LabelOf(from, label, instant),
            LabelOf(to, label, instant)));
    }

    // An instant is held as an ISO 8601 UTC string (WorkItemFieldView.Instant); a null one has no label.
    private static string? LabelOf<T>(T value, Func<Guid, string?>? label, Func<DateTimeOffset, string>? instant) => value switch
    {
        Guid id when label is not null => label(id),
        string text when instant is not null => instant(DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)),
        _ => null,
    };
}
