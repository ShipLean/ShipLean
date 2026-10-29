using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Taskboard.FeatureBoard;

// One Feature of the tree as laid out on the board: its depth, the Feature above it for depth 2 and deeper, and its own stories and bugs (ids, in row order).
internal sealed record FeatureBoardSection(Guid Id, string Key, string Title, StateRef State, int Depth, ParentRefDto? ParentFeature, IReadOnlyList<Guid> StoryIds);
