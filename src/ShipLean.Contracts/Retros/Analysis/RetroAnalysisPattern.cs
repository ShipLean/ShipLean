namespace ShipLean.Contracts.Retros.Analysis;

// Ref is the agent's own label, unique in the run. Continues is an earlier pattern of the same project and embedding model that this run's points join.
public sealed record RetroAnalysisPattern(
    string Ref, RetroPatternKind Kind, string Title, string Lesson, IReadOnlyList<string> Asks, IReadOnlyList<Guid> PointIds, IReadOnlyList<float>? Centroid, Guid? Continues);
