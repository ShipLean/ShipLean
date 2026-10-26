using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Search.Index;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Index.Service;

// The real hosted service (ShipLeanHost removes it by default) over recording doubles. The timers are an hour, so only events
// (and a test that sets them small) wake it; tests publish an event and await the double's next sync and pass. The host is shared by a class's tests, so only a class with a single test can assert which pass was the first.
public abstract class ServiceFixture : ApiFixture
{
    protected static readonly Guid Project = SeedIds.Projects.Ship;

    protected ObservedSearchDocWriter Writer { get; } = new();

    protected ObservedSearchIndexer Indexer { get; } = new();

    protected virtual string FirstPassSeconds => "3600";

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_SEARCH_FIRST_PASS_SECONDS"] = FirstPassSeconds,
        ["SHIPLEAN_SEARCH_INTERVAL_SECONDS"] = "3600",
    };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.Replace(ServiceDescriptor.Singleton<ISearchDocWriter>(Writer));
        services.Replace(ServiceDescriptor.Singleton<ISearchIndexer>(Indexer));
        services.AddHostedService<SearchIndexHostedService>();
    };

    // Publishes the event, then waits for the sync it causes and the pass that follows.
    protected async Task<IReadOnlyList<SearchSource>> PublishAsync(ProjectEvent projectEvent)
    {
        await Host.Services.GetRequiredService<IProjectEvents>().PublishAsync(projectEvent);
        var sources = await Writer.NextSyncAsync();
        await Indexer.NextPassAsync();
        return sources;
    }
}
