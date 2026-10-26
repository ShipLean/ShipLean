namespace ShipLean.TestSupport.Plans;

// The keys PlanTreeSeed wrote: the Feature's, each story's by its title and each task's by its title (titles are unique in a fixture).
// Ids maps every one of those keys to its work item id, so a consumer can write comments and links without SQL.
public sealed record PlanSeedKeys(string Feature, IReadOnlyDictionary<string, string> Stories, IReadOnlyDictionary<string, string> Tasks, IReadOnlyDictionary<string, Guid> Ids);
