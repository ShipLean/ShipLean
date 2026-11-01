using ShipLean.Contracts.Retros;

namespace ShipLean.TestSupport.Retros;

// One retro for RetroSeed. Give a work item, a sprint or both (the sprint is what ties the retro to a sprint view); AgentName must differ per work item for live agent retros.
public sealed record RetroSeedRetro(Guid AuthorId, RetroRole Role = RetroRole.SubAgent, Guid? WorkItemId = null, Guid? SprintId = null, string? AgentName = null, bool Partial = false, DateTimeOffset? CreatedAt = null);
