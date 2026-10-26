using Microsoft.Extensions.Configuration;
using ShipLean.Application.Features.Search;

namespace ShipLean.UnitTest.Search.Lexical;

[TestFixture]
public class SearchOptionsDefaultsTests
{
    [Test]
    public void From_WhenNothingConfigured_ShouldUseTheTunedCutoff()
    {
        var options = SearchOptions.From(new ConfigurationBuilder().Build());

        options.Cutoff.ShouldBe(0.84);
    }

    [Test]
    public void From_WhenCutoffConfigured_ShouldUseIt()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["SHIPLEAN_SEARCH_CUTOFF"] = "0.7" }).Build();

        SearchOptions.From(configuration).Cutoff.ShouldBe(0.7);
    }
}
