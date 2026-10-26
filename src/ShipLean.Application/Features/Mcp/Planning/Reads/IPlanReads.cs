using ShipLean.Application.Features.Mcp.Planning.Tree;

namespace ShipLean.Application.Features.Mcp.Planning.Reads;

// Reads a plan back as a PlanTree. Stories are the Feature's Story children, ordered by wave then rank; their tasks by rank. A
// story's wave is its wave: tag; one without (made by hand) is a wave of its own after the tagged waves, in rank order.
// Removed items are left out. Callers check project access first.
public interface IPlanReads
{
    // Throws ApiException.NotFound (unknown id), ApiException.Validation (not a Feature, it was removed, or it has no stories).
    Task<PlanTree> TreeAsync(Guid featureId, CancellationToken cancellationToken = default);

    // The plan a Feature, Story or Task belongs to, and the item itself. Throws ApiException.NotFound (unknown id),
    // ApiException.Validation (the item is not in a plan: no Feature above it, or removed).
    Task<(PlanTree Tree, Guid ItemId)> TreeOfAsync(Guid itemId, CancellationToken cancellationToken = default);
}
