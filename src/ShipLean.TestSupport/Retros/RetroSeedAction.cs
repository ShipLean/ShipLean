namespace ShipLean.TestSupport.Retros;

// One retro_action row for RetroSeed: the work item exists already (the caller arranged it).
public sealed record RetroSeedAction(Guid WorkItemId, Guid? ThemeId = null, Guid? PointId = null, Guid? RaisedSprintId = null);
