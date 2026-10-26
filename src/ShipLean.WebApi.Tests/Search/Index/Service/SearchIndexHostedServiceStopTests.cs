using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Features.Search.Index;
using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Tests.Search.Index.Service;

[TestFixture]
public class SearchIndexHostedServiceStopTests : ServiceFixture
{
    [Test]
    public async Task Service_WhenStopped_ShouldCancelAndAwaitTheInFlightPass()
    {
        Indexer.HoldUntilCancelled = true;
        await Host.Services.GetRequiredService<IProjectEvents>().PublishAsync(new ProjectChanged(Project));
        await Writer.NextSyncAsync();
        await Indexer.NextPassAsync();

        await Host.Services.GetServices<IHostedService>().OfType<SearchIndexHostedService>().Single().StopAsync(CancellationToken.None);

        Indexer.Cancelled.ShouldBeTrue();
        Indexer.Finished.ShouldBeTrue();
    }
}
