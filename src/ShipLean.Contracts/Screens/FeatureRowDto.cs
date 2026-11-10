using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Contracts.Screens;

// One Feature on the Features overview. StatusStale is true when the status line is older than FeatureRules.StatusStaleAfter.
// Epic is the Feature's parent Epic, null when it has none.
public sealed record FeatureRowDto(
    Guid Id,
    string Key,
    string Title,
    int Revision,
    StateRef State,
    FeatureHealth? Health,
    string? StatusLine,
    StatusVerifiedDto? StatusVerified,
    bool StatusStale,
    IReadOnlyList<RollupCountDto> Rollups,
    NeedsYouDto? NeedsYou,
    int Rank,
    ParentRefDto? Epic);
