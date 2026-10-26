namespace ShipLean.Application.Features.Search.Ranking.Semantic;

// The cached vectors of one project for one model, and the cache key they were read under: the newest indexed_at (in
// microseconds), the row count (deleted rows don't move indexed_at) and the count of embedded rows (an embedding written
// to an existing row may not move indexed_at either).
public sealed record ProjectVectors(Guid ProjectId, string Model, long MaxIndexedAt, int Rows, int Embedded, IReadOnlyList<VectorRow> Vectors);
