namespace ShipLean.Contracts.Views;

// Count includes both halves of a split column across every lane and the cards the filter hides; HiddenCount is how many of
// those the filter hides (they still count toward WIP). DoingCount and DoneCount
// are 0 when the column is not split. DefinitionOfDoneHtml is the rendered markdown.
public sealed record BoardColumnDto(
    string Id,
    string Name,
    int? WipLimit,
    int Count,
    int HiddenCount,
    bool OverLimit,
    bool Split,
    int DoingCount,
    int DoneCount,
    string DefinitionOfDone,
    string DefinitionOfDoneHtml,
    IReadOnlyList<ColumnStateDto> States);
