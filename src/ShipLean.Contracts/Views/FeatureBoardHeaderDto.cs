using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views;

// The page header: the Feature and a roll-up of the whole tree, computed from the rows returned. Sprints are the sprints the
// rows' stories sit in, by start date; AnyInBacklog: some row has no sprint. Points are null when no row has points.
public sealed record FeatureBoardHeaderDto(
    Guid Id,
    string Key,
    string Title,
    StateRef State,
    int FeaturesUnder,
    int StoriesClosed,
    int StoriesTotal,
    decimal? PointsClosed,
    decimal? PointsTotal,
    int TasksClosed,
    int TasksTotal,
    IReadOnlyList<SprintRef> Sprints,
    bool AnyInBacklog);
