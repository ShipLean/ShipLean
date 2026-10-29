using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Contracts.Views;

// One Feature of the tree. Depth 0 is the board's Feature; ParentFeature names the Feature above for Depth 2 and deeper. The counts
// cover this section's own stories and bugs only (as returned in Rows). Points are null when none of them has points.
public sealed record FeatureBoardSectionDto(
    Guid Id,
    string Key,
    string Title,
    StateRef State,
    int Depth,
    ParentRefDto? ParentFeature,
    int StoriesClosed,
    int StoriesTotal,
    decimal? PointsClosed,
    decimal? PointsTotal);
