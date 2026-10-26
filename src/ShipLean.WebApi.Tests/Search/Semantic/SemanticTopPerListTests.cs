using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Search.Semantic;

// The host runs with a list length of one, so a filter that drops the best match is only survivable by looking further down.
[TestFixture]
public class SemanticTopPerListTests : SemanticFixture
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { ["SHIPLEAN_SEARCH_TOP_PER_LIST"] = "1" };

    [Test]
    public async Task Semantic_WhenFilteredItemsScoreHigher_ShouldStillFillTheList()
    {
        await using var connection = await OpenDatabaseAsync();
        var closed = await ItemAsync(connection, "Sign-in fails", stateId: SeedIds.States.Ship.StoryClosed);
        await RowAsync(connection, closed, SearchField.Title, "Sign-in fails", embedAs: "login broken");
        var open = await ItemAsync(connection, "Sign-in fails for SSO users");
        await RowAsync(connection, open, SearchField.Title, "Sign-in fails for SSO users");

        var other = await ItemAsync(connection, "Authentication crashes on mobile");
        await RowAsync(connection, other, SearchField.Title, "Authentication crashes on mobile");

        var lists = await ListsAsync("login broken", true, [SeedIds.Projects.Ship], q => q with { IncludeClosed = false });

        Ids(lists, RankedListName.TitleVector).Count.ShouldBe(1);
        Ids(lists, RankedListName.TitleVector).ShouldNotContain(closed);
    }
}
