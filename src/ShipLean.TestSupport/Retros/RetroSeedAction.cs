using ShipLean.Contracts.Retros;

namespace ShipLean.TestSupport.Retros;

// One retro_action row for RetroSeed: the work item exists already (the caller arranged it). PointId and PointIds each add a live join row (retro_action_point); retro_action.point_id stays null.
public sealed record RetroSeedAction(Guid WorkItemId, Guid? ThemeId = null, Guid? PointId = null, Guid? RaisedSprintId = null, RetroFixType? FixType = null, IReadOnlyList<Guid>? PointIds = null);
