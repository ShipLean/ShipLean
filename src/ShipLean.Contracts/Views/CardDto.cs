using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Contracts.Views;

// A card on a level board or the Taskboard (task cards and story or bug row headers too). Assignee is the Owner on the
// Features and Epics boards. ChildPoints is the sum of the points of all Stories and Bugs below the card, including under nested Features (null when none have any); SumWarn is true when it
// exceeds Points by more than 25 %. OfferDone: every child is closed and the card is not yet in Done. ExtraFields leaves out
// the fields the card already carries (state, parent, assignee, points, tags), even when the settings list them.
public sealed record CardDto(
    Guid Id,
    string Key,
    TypeRef Type,
    string Title,
    StateRef State,
    UserRef? Assignee,
    decimal? Points,
    decimal? ChildPoints,
    bool SumWarn,
    ParentRefDto? Parent,
    IReadOnlyList<CardTagDto> Tags,
    IReadOnlyList<RollupCountDto> Rollups,
    bool OfferDone,
    string? StyleRuleId,
    IReadOnlyList<CardFieldDto> ExtraFields,
    int Revision);
