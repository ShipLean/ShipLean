using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Search;

namespace ShipLean.WebApi.Tests.Search.Index.Service;

// One test per fixture: the failure count is the service's state.
[TestFixture]
public class SearchIndexHostedServiceSyncFailureTests : ServiceFixture
{
    [Test]
    public async Task Service_WhenSyncKeepsFailing_ShouldDropThoseSourcesAndStillRunThePass()
    {
        var events = Host.Services.GetRequiredService<IProjectEvents>();
        var bad = Guid.NewGuid();
        Writer.FailAlways();

        await events.PublishAsync(new SprintChanged(Project, bad));
        (await Writer.NextSyncAsync()).ShouldBe([new SearchSource(SearchSourceKind.Sprint, bad)]);
        await Indexer.NextPassAsync();
        var other = Guid.NewGuid();
        await events.PublishAsync(new SprintChanged(Project, other));
        (await Writer.NextSyncAsync()).ShouldBe(
            [new SearchSource(SearchSourceKind.Sprint, bad), new SearchSource(SearchSourceKind.Sprint, other)], ignoreOrder: true);
        await Indexer.NextPassAsync();
        Writer.FailNext(0);
        var good = Guid.NewGuid();
        await events.PublishAsync(new SprintChanged(Project, good));
        var sources = await Writer.NextSyncAsync();
        await Indexer.NextPassAsync();

        sources.ShouldBe([new SearchSource(SearchSourceKind.Sprint, good)]);
        Indexer.Calls.Count.ShouldBe(3);
    }
}
