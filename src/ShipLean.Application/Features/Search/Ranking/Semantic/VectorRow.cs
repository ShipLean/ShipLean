using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Ranking.Semantic;

// One embedded chunk of an item, comment or attachment, as the cache holds it. The text stays in the database: only the
// rows that make a list are read back for their snippet.
public sealed record VectorRow(Guid Id, Guid WorkItemId, SearchField Field, float[] Vector);
