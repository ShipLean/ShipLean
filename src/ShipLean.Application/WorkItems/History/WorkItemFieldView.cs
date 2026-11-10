using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;

namespace ShipLean.Application.WorkItems.History;

// The history-visible fields of an item, in the form a FieldChange compares them: empty text is null. The blank view
// (everything null or empty) is what a new item is compared against.
public sealed record WorkItemFieldView(
    string? Title,
    WorkItemTypeId? Type,
    Guid? State,
    Guid? Assignee,
    int? Priority,
    decimal? Points,
    IReadOnlyList<string> Tags,
    Guid? Sprint,
    Guid? Parent,
    int? Severity,
    string? Description,
    IReadOnlyList<ChecklistItem> AcceptanceCriteria,
    string? ReproSteps,
    IReadOnlyList<LinkValue> Links,
    string? Goal = null,
    string? StatusLine = null,
    string? StatusVerifiedAt = null,
    string? Health = null)
{
    public static WorkItemFieldView Blank { get; } = new(null, null, null, null, null, null, [], null, null, null, null, [], null, []);

    public static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    // A Feature's health as it is stored and as history holds it: the camelCase name (onTrack, atRisk, blocked).
    public static string? HealthValue(FeatureHealth? value) => value switch
    {
        FeatureHealth.OnTrack => "onTrack",
        FeatureHealth.AtRisk => "atRisk",
        FeatureHealth.Blocked => "blocked",
        _ => null,
    };

    public static FeatureHealth? ParseHealth(string? value) => value is null ? null : Enum.Parse<FeatureHealth>(value, ignoreCase: true);

    // The words history shows for a stored health ("On track", "At risk", "Blocked"): the health field's values in FieldIds.
    public static string? HealthLabel(string value)
        => FieldIds.All.Single(f => f.Id == FieldIds.Health).Values!.FirstOrDefault(v => v.Value.GetString() == value)?.Label;

    // An instant as history stores it: ISO 8601 UTC with a Z, the fraction only when there is one.
    public static string? Instant(DateTimeOffset? value)
        => value?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
