using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.ProjectSettings;

// The patches that move items out of the states being removed, and where each removed state's items go.
internal sealed record StateMoves(IReadOnlyList<(Guid Id, WorkItemPatch Patch)> Patches, IReadOnlyDictionary<Guid, Guid> Targets);
