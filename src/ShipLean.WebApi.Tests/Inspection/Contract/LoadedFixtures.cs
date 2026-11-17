using ShipLean.Contracts.Inspection;
using ShipLean.LocalEnvironment.Inspection;
using ShipLean.TestSupport.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.Contract;

// One fixture sent through the collector under a project key of its own, with the RawRun a store must return for it.
public sealed record LoadedRun(InspectionFixture Fixture, LoadedFixture Load, RawRun Expected)
{
    public string ProjectKey => Load.ProjectKey;

    // The item the run is tagged with (the first link).
    public string ItemKey => Expected.Summary.Links[0].ItemKey;

    public static async Task<LoadedRun> LoadAsync(InspectionStack stack, InspectionFixture fixture, int batches, TimeSpan timeout)
    {
        var key = "T" + Guid.NewGuid().ToString("N")[..10];
        // Microsecond precision, like the stores.
        var now = new DateTimeOffset(TimeProvider.System.GetUtcNow().UtcTicks / 10 * 10, TimeSpan.Zero);
        var load = await OtlpFixtureLoader.SendAsync(stack.CollectorUrl, fixture.OtlpPath, key, now, batches);
        var expected = RawRunJson.ParseRun(load.Rewrite(System.Text.Json.JsonSerializer.Serialize(fixture.Expected, RawRunJson.Options)));
        await stack.WaitUntilVisibleAsync(load, fixture.Expected.Summary.SpanCount, fixture.Expected.Summary.LogCount, timeout);
        // On a fresh stack the first load migrates the ShipLean schema, once, before any test's own migrator can race it.
        await ClickHouseSchemaReady.EnsureAsync(stack, timeout);
        return new LoadedRun(fixture, load, expected);
    }
}
