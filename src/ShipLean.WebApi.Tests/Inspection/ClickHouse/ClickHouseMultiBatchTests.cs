using ShipLean.Application.Features.Inspection.ClickHouse;
using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Contracts.Inspection;
using ShipLean.LocalEnvironment.Inspection;
using ShipLean.TestSupport.Inspection;
using ShipLean.WebApi.Tests.Inspection.Contract;

namespace ShipLean.WebApi.Tests.Inspection.ClickHouse;

// One run arrives in several collector batches and ClickHouse merges the partial rows: counts and tokens must still add up.
[TestFixture(TestName = "ClickHouse multi batch, SHIP-1556")]
[Category("SHIP-1556")]
public sealed class ClickHouseMultiBatchTests
{
    [Test]
    public async Task FindRuns_WhenTheRunCameInTwoBatchesAndTheRowsMerged_HasTheFullTotals()
    {
        var stack = await InspectionStack.GetAsync();
        var fixture = InspectionFixtures.Get("claude-code-synthetic");
        var run = await LoadedRun.LoadAsync(stack, fixture, 2, TimeSpan.FromSeconds(StoreTimeouts.WaitSeconds));
        var options = stack.Options("clickhouse");
        var migrator = new ClickHouseSchemaMigrator(options);
        await migrator.EnsureAsync(CancellationToken.None);
        await ClickHouseDb.From(options).ExecuteAsync($"OPTIMIZE TABLE {migrator.Names.Runs} FINAL", null, CancellationToken.None);
        var query = new RunQuery(run.ProjectKey, run.ItemKey, run.Load.StartedAt.AddMinutes(-1), run.Load.StartedAt.AddHours(1), null, null);

        var search = await new ClickHouseRunFinder(options, migrator).FindAsync(query, CancellationToken.None);

        var summary = search.Runs.ShouldHaveSingleItem(search.Problem);
        var expected = run.Expected.Summary;
        (summary.SpanCount, summary.LogCount, summary.ErrorCount, summary.TokensIn, summary.TokensOut)
            .ShouldBe((expected.SpanCount, expected.LogCount, expected.ErrorCount, expected.TokensIn, expected.TokensOut));
    }
}
