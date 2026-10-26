using ShipLean.Contracts.Views.Search;
using ShipLean.UnitTest.Dtos.Boards;

namespace ShipLean.UnitTest.Dtos.Search;

[TestFixture]
public class SearchViewDtoTests
{
    [Test]
    public void SearchViewDto_WhenRoundTripped_ShouldMatchFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<SearchViewDto>(BoardDtoFixture.Read("search", "search-view.json")).ShouldBeTrue();
    }
}
