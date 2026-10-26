using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts.Search;

namespace ShipLean.WebApi.Tests.Search.Endpoint;

// A leg that returns the lists it was given, whatever the query.
public sealed class StubItemCandidates(CandidateLeg leg) : IItemCandidates
{
    public CandidateLeg Leg => leg;

    public IReadOnlyList<RankedList> Lists { get; set; } = [];

    public Task<CandidateLists> ListsAsync(CandidateQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(new CandidateLists(true, 0, Lists));

    public static CandidateHit Hit(Guid id, string snippet, string? attachment = null)
        => new(id, attachment is null ? SearchField.Title : SearchField.Attachment, 1, snippet, attachment);
}
