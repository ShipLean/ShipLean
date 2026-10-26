using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Backlog.Stories;

// The story rows tree mode works from: Visible are the rows shown (the filter and includeCompleted applied), WithClosed the same
// filter with completed items too (the group counts include them), Rank each story's place in the whole backlog.
public sealed record TreeStories(IReadOnlyList<StackRowDto> Visible, IReadOnlyList<StackRowDto> WithClosed, IReadOnlyDictionary<Guid, int> Rank);
