namespace ShipLean.Contracts.Demos;

public sealed record DemoChaptersJson(decimal DurationSeconds, IReadOnlyList<DemoChapterJson> Chapters);
