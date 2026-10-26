using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Planning.Capture;

// What an apply wrote: the Feature's key, its stories (in wave order) and the files attached to the Feature.
public sealed record CapturePlanApplied(string Feature, IReadOnlyList<CapturedStory> Stories, IReadOnlyList<string> Attachments);
