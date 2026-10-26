using ShipLean.Contracts.Search;
using ShipLean.UnitTest.Dtos.Boards;

namespace ShipLean.UnitTest.Dtos.Search;

[TestFixture]
public class SearchDtoTests
{
    [Test]
    public void SearchResultsShape_ShouldRoundTrip()
    {
        BoardDtoFixture.SurvivesRoundTrip<SearchResults>(BoardDtoFixture.Read("search", "search-results.json")).ShouldBeTrue();
    }
}
