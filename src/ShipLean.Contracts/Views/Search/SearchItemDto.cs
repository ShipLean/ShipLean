using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views.Search;

// ParentPath is the keys of the ancestors from the top down; empty for a top-level item. Attachment is the file name when an
// attachment matched, else null. Url is a relative SPA path.
public sealed record SearchItemDto(
    string Key,
    string Title,
    WorkItemTypeId Type,
    string State,
    StateCategory StateCategory,
    string ProjectKey,
    IReadOnlyList<string> ParentPath,
    SearchMatchedIn MatchedIn,
    string Snippet,
    string Url,
    string? Attachment);
