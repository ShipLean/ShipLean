using ShipLean.Contracts.Screens;

namespace ShipLean.TestSupport.Screens;

// INeedsYouCounts for tests of its callers: answers with the counts it was given, for the asked ids that have one, and records
// each call (the user and the ids asked) in order.
public sealed class FakeNeedsYouCounts(IReadOnlyDictionary<Guid, NeedsYouDto> counts) : INeedsYouCounts
{
    private readonly List<(Guid UserId, IReadOnlyList<Guid> ItemIds)> _calls = [];

    public FakeNeedsYouCounts()
        : this(new Dictionary<Guid, NeedsYouDto>())
    {
    }

    public IReadOnlyList<(Guid UserId, IReadOnlyList<Guid> ItemIds)> Calls => _calls;

    public Task<IReadOnlyDictionary<Guid, NeedsYouDto>> ForItemsAsync(Guid userId, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        _calls.Add((userId, itemIds.ToList()));
        IReadOnlyDictionary<Guid, NeedsYouDto> answer = itemIds
            .Distinct()
            .Where(counts.ContainsKey)
            .ToDictionary(id => id, id => counts[id]);
        return Task.FromResult(answer);
    }
}
