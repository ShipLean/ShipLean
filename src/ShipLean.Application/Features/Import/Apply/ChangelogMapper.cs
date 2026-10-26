using System.Text.Json;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Import.Apply;

// Jira's changelog as work item revisions. Kept: summary (title), status (state) and the story points field; every other change is not
// carried over, because Jira names a person or sprint by display string only. An entry with nothing kept makes no revision.
internal static class ChangelogMapper
{
    public static async Task<IReadOnlyList<ImportedRevision>> MapAsync(
        JiraIssue issue, string pointsFieldName, Func<string, Task<Guid?>> stateOf, Func<JiraUser?, Task<Guid>> authorOf)
    {
        var revisions = new List<ImportedRevision>();
        foreach (var entry in issue.Changelog.OrderBy(e => e.Created))
        {
            var changes = new List<FieldChange>();
            foreach (var item in entry.Items)
            {
                if (await ChangeAsync(item, pointsFieldName, stateOf) is { } change)
                {
                    changes.Add(change);
                }
            }

            if (changes.Count > 0)
            {
                revisions.Add(new ImportedRevision(await authorOf(entry.Author), entry.Created, changes));
            }
        }

        return revisions;
    }

    private static async Task<FieldChange?> ChangeAsync(JiraChangeItem item, string pointsFieldName, Func<string, Task<Guid?>> stateOf)
    {
        if (string.Equals(item.Field, "summary", StringComparison.OrdinalIgnoreCase))
        {
            return new FieldChange(FieldIds.Title, Json(item.From ?? string.Empty), Json(item.To ?? string.Empty), null, null);
        }

        if (string.Equals(item.Field, "status", StringComparison.OrdinalIgnoreCase))
        {
            var from = item.From is null ? null : await stateOf(item.From);
            var to = item.To is null ? null : await stateOf(item.To);
            return from is null || to is null ? null : new FieldChange(FieldIds.State, Json(from), Json(to), item.From, item.To);
        }

        if (pointsFieldName.Length > 0 && string.Equals(item.Field, pointsFieldName, StringComparison.OrdinalIgnoreCase))
        {
            return new FieldChange(FieldIds.Points, Number(item.From), Number(item.To), null, null);
        }

        return null;
    }

    private static JsonElement Number(string? text)
        => decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value) ? Json(value) : Json((decimal?)null);

    private static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value, PersistedJson.Options);
}
