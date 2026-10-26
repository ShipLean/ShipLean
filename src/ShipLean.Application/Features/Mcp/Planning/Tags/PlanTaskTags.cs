using ShipLean.Application.Features.Mcp.Planning.Waves;

namespace ShipLean.Application.Features.Mcp.Planning.Tags;

// What a plan keeps in an item's tags (placement is on PlanTags). A story carries Wave and Lane, a task those plus Model,
// ContextK and maybe Orphan, the Feature Branch (the shared branch). Null or false means the tag is absent. ContextK is the context estimate in thousands of tokens.
public sealed record PlanTaskTags(
    WaveLabel? Wave = null, string? Lane = null, string? Model = null, int? ContextK = null, string? Branch = null, bool Orphan = false);
