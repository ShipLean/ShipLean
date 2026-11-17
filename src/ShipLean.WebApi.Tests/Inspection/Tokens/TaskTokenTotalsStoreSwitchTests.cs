using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.TempoLoki;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.Tokens;

// SHIP-1463's store switch for ITaskTokenTotals (SHIP-1398 CK1, item D20): a Tempo + Loki install gets the source that says Cost
// needs the ClickHouse store. No store is contacted.
[TestFixture]
[Category("SHIP-1398")]
public sealed class TaskTokenTotalsStoreSwitchTests
{
    // Mutation: register the tempo-loki case as DisabledTaskTokenTotals.
    [Test]
    public async Task Resolve_WhenStoreIsTempoLoki_ShouldSayItNeedsClickHouse()
    {
        var services = new ServiceCollection();
        new InspectionServices().AddServices(services, new ConfigurationBuilder().Build());
        services.AddSingleton(new InspectionOptions
        {
            Enabled = true,
            Store = InspectionOptionsReader.TempoLoki,
            TempoUrl = "http://127.0.0.1:9/tempo",
            LokiUrl = "http://127.0.0.1:9/loki",
        });
        await using var provider = (ServiceProvider)new DefaultServiceProviderFactory().CreateServiceProvider(services);

        var source = provider.GetRequiredService<ITaskTokenTotals>();
        var totals = await source.ReadAsync(new TaskTokenQuery("SHIP", ["SHIP-1"], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1)), CancellationToken.None);

        source.ShouldBeOfType<TempoLokiTaskTokenTotals>();
        totals.Available.ShouldBeFalse();
        totals.Problem.ShouldBe(TempoLokiTaskTokenTotals.NeedsClickHouse);
        totals.ByItem.ShouldBeEmpty();
    }
}
