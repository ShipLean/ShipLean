using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Contracts.Views;

// One row for every mode. Kind says what the row is. On a NoParentGroup row Id is Guid.Empty, Key is empty, Title is "No parent",
// and Type, State, Points, Parent and Sprint are null. Group counts are filtered: Rollups and ChildPoints on a Group or NoParentGroup row
// count the children that pass the view's filter (ChildPoints sums the open and closed points of the filtered stories and bugs; the header labels it "Σ pts"). Depth is the indent in tree mode (0 in list and planning). Rank restarts in each planning section
// (null on a tree group row). Rollups, ChildPoints and SumWarn are set on Features, Epics and the NoParentGroup row (SumWarn false there), empty / null / false on the rest.
public sealed record BacklogRowDto(
    BacklogRowKind Kind,
    Guid Id,
    string Key,
    TypeRef? Type,
    string Title,
    StateRef? State,
    UserRef? Assignee,
    decimal? Points,
    decimal? ChildPoints,
    bool SumWarn,
    SprintRef? Sprint,
    ParentRefDto? Parent,
    int Depth,
    int? Rank,
    IReadOnlyList<CardTagDto> Tags,
    IReadOnlyList<RollupCountDto> Rollups,
    int Revision);
