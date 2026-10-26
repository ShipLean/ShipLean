using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Tests.Search.Index.Service;

// One test per fixture: the first pass of a host is full, which only the class's first round can show.
[TestFixture]
public class SearchIndexHostedServicePassKindTests : ServiceFixture
{
    [Test]
    public async Task Service_WhenRoundsRun_ShouldMakeTheFirstPassFullAndLaterOnesIncremental()
    {
        var events = Host.Services.GetRequiredService<IProjectEvents>();

        await events.PublishAsync(new SprintChanged(Project, Guid.NewGuid()));
        await Writer.NextSyncAsync();
        var first = await Indexer.NextPassAsync();
        await events.PublishAsync(new SprintChanged(Project, Guid.NewGuid()));
        await Writer.NextSyncAsync();
        var second = await Indexer.NextPassAsync();
        await events.PublishAsync(new ProjectChanged(Project));
        await Writer.NextSyncAsync();
        var third = await Indexer.NextPassAsync();

        (first, second, third).ShouldBe((true, false, false));
        Writer.SyncAllCalls.ShouldBe(0);
    }
}
