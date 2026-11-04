namespace ShipLean.Contracts.Backlog;

// The Features a sprint's work belongs to, reached through Contracts so a feature outside Features/Mcp (a sprint demo) can read them.
// Callers resolve the sprint and check access first.
public interface ISprintFeatureReads
{
    // The features that hold the sprint's story-level items (their direct parents), by number.
    Task<IReadOnlyList<SprintFeature>> FeaturesAsync(Guid sprintId, CancellationToken cancellationToken = default);
}
