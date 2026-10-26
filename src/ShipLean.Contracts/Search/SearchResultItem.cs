using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Search;

// ParentPath is the keys of the ancestors from the top down (["SHIP-2", "SHIP-7"]); empty for a top-level item.
// MatchedIn is the field of the best row. Attachment is the file name when an attachment matched, else null.
public sealed record SearchResultItem(
    Guid WorkItemId,
    string Key,
    string Title,
    WorkItemTypeId Type,
    string State,
    StateCategory StateCategory,
    string ProjectKey,
    IReadOnlyList<string> ParentPath,
    string? Assignee,
    string? Sprint,
    decimal? Points,
    IReadOnlyList<string> Tags,
    SearchField MatchedIn,
    string? Attachment,
    string Snippet,
    double Score);
