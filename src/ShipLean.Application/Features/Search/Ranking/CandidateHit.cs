using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Ranking;

// Field is the search_doc field of the row that hit. Attachment is the file name when that row is an attachment's.
public sealed record CandidateHit(Guid WorkItemId, SearchField Field, double Score, string Snippet, string? Attachment = null);
