namespace ShipLean.Contracts.Demos;

// One wave of the plan: Label is the wave: tag's text, null for a story with no wave tag (a wave of its own). Start is the earliest
// story start, End the latest story end and null while any story is open; Hours then runs to the time the data was read.
public sealed record SprintDemoWave(string? Label, IReadOnlyList<string> StoryKeys, DateTimeOffset? Start, DateTimeOffset? End, double? Hours, string? DurationLabel);
