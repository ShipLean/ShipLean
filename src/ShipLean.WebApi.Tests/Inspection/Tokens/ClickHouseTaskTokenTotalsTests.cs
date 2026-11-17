using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.ClickHouse;
using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Contracts.Inspection;
using ShipLean.LocalEnvironment.Inspection;
using ShipLean.TestSupport.Inspection;
using ShipLean.WebApi.Tests.Inspection.Contract;

namespace ShipLean.WebApi.Tests.Inspection.Tokens;

// The ClickHouse tokens per task beyond the contract (SHIP-1398 CT1): another project's run, an even split over equally strong
// links, and logs standing in for spans. Variant fixtures are the claude-code fixture with its OTLP edited in a temp file.
[TestFixture(TestName = "ClickHouse tokens per task cases, SHIP-1398 CT1")]
[Category("SHIP-1398")]
public sealed class ClickHouseTaskTokenTotalsTests
{
    private const string FixtureName = "claude-code-synthetic";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(StoreTimeouts.WaitSeconds);
    private readonly List<ServiceProvider> providers = [];
    private readonly List<string> files = [];
    private InspectionStack stack = null!;

    [OneTimeSetUp]
    public async Task StartStack() => stack = await InspectionStack.GetAsync();

    [OneTimeTearDown]
    public async Task Cleanup()
    {
        foreach (var provider in providers)
        {
            await provider.DisposeAsync();
        }
        foreach (var file in files)
        {
            File.Delete(file);
        }
    }

    [Test]
    public async Task ReadAsync_WhenRunLinkedToOtherProject_ShouldNotCount()
    {
        var mine = await LoadAsync(InspectionFixtures.Get(FixtureName));
        var other = await LoadAsync(InspectionFixtures.Get(FixtureName));
        var tagged = mine.Expected.Summary.Links.Single(link => link.Source == LinkSource.Tagged).ItemKey;

        var totals = await Source().ReadAsync(new TaskTokenQuery(mine.ProjectKey, [tagged], mine.Load.StartedAt.AddMinutes(-1), mine.Load.StartedAt.AddHours(1)), CancellationToken.None);

        // Both runs are tagged to the same item key; only the one in this project counts.
        totals.Available.ShouldBeTrue(totals.Problem);
        totals.ByItem[tagged].Runs.ShouldBe(1);
        totals.ByItem[tagged].Total.ShouldBe(mine.Expected.Summary.TokensIn.ShouldNotBeNull() + mine.Expected.Summary.TokensOut.ShouldNotBeNull());
        other.ProjectKey.ShouldNotBe(mine.ProjectKey);
    }

    [Test]
    public async Task ReadAsync_WhenRunHasTwoTaggedItems_ShouldSplitTokens()
    {
        // 901 in and 101 out, both odd, reported by the logs; a second Tagged link row is added for the same run.
        var run = await LoadAsync(Variant(root => MoveTokensToLogs(root, 901, 101)));
        var source = Source(out var provider);
        var second = run.ProjectKey + "-99";
        var seen = run.Load.StartedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        await ClickHouseDb.From(stack.Options("clickhouse")).ExecuteAsync(
            $"INSERT INTO {provider.GetRequiredService<ClickHouseSchemaMigrator>().Names.RunItems} (project, item_key, session_id, link_version, seen_at) VALUES ('{run.ProjectKey}', '{second}', '{run.Load.SessionIds.Values.Single()}', {ClickHouseViewSql.TaggedVersion}, '{seen}')",
            null,
            CancellationToken.None);

        var totals = await source.ReadAsync(new TaskTokenQuery(run.ProjectKey, ["SHIP-1", "SHIP-2", second], run.Load.StartedAt.AddMinutes(-1), run.Load.StartedAt.AddHours(1)), CancellationToken.None);

        // SHIP-1 sorts before the second key, so it takes the odd token; SHIP-2 is only a tool-call link and gets nothing.
        totals.Available.ShouldBeTrue(totals.Problem);
        totals.ByItem.Keys.ShouldBe(["SHIP-1", second], ignoreOrder: true);
        totals.ByItem["SHIP-1"].ShouldSatisfyAllConditions(t => t.Runs.ShouldBe(1), t => t.TokensIn.ShouldBe(451), t => t.TokensOut.ShouldBe(51));
        totals.ByItem[second].ShouldSatisfyAllConditions(t => t.Runs.ShouldBe(1), t => t.TokensIn.ShouldBe(450), t => t.TokensOut.ShouldBe(50));
    }

    [Test]
    public async Task ReadAsync_WhenRunReportedNoTokens_ShouldNotCountIt()
    {
        var run = await LoadAsync(InspectionFixtures.Get("cursor-synthetic"));
        run.Expected.Summary.TokensIn.ShouldBeNull();
        var linked = run.Expected.Summary.Links.Single(link => link.Source == LinkSource.ShipleanFile).ItemKey;

        var totals = await Source().ReadAsync(new TaskTokenQuery(run.ProjectKey, [.. run.Expected.Summary.Links.Select(link => link.ItemKey)], run.Load.StartedAt.AddMinutes(-1), run.Load.StartedAt.AddHours(1)), CancellationToken.None);

        totals.Available.ShouldBeTrue(totals.Problem);
        totals.ByItem.ShouldNotContainKey(linked);
        totals.ByItem.ShouldBeEmpty();
    }

    [Test]
    public async Task ReadAsync_WhenSpansHaveNoTokens_ShouldUseLogTokens()
    {
        var run = await LoadAsync(Variant(root => MoveTokensToLogs(root, 900, 100)));

        var totals = await Source().ReadAsync(new TaskTokenQuery(run.ProjectKey, ["SHIP-1"], run.Load.StartedAt.AddMinutes(-1), run.Load.StartedAt.AddHours(1)), CancellationToken.None);

        totals.Available.ShouldBeTrue(totals.Problem);
        totals.ByItem["SHIP-1"].TokensIn.ShouldBe(900);
        totals.ByItem["SHIP-1"].TokensOut.ShouldBe(100);
    }

    private Task<LoadedRun> LoadAsync(InspectionFixture fixture) => LoadedRun.LoadAsync(stack, fixture, 1, Timeout);

    // The fixture's OTLP with the edit applied, written to a temp file.
    private InspectionFixture Variant(Action<JsonObject> edit)
    {
        var fixture = InspectionFixtures.Get(FixtureName);
        var root = JsonNode.Parse(File.ReadAllText(fixture.OtlpPath))!.AsObject();
        edit(root);
        var path = Path.Combine(Path.GetTempPath(), $"shiplean-tokens-{Guid.NewGuid():N}.otlp.json");
        File.WriteAllText(path, root.ToJsonString());
        files.Add(path);
        return fixture with { OtlpPath = path };
    }

    // Spans stop reporting tokens (their attribute is renamed) and the first log record reports the given tokens.
    private static void MoveTokensToLogs(JsonObject root, int tokensIn, int tokensOut)
    {
        Walk(root["resourceSpans"]!, node =>
        {
            if (node["key"]?.GetValue<string>() is "input_tokens" or "output_tokens")
            {
                node["key"] = "renamed_" + node["key"]!.GetValue<string>();
            }
        });
        var attributes = root["resourceLogs"]![0]!["scopeLogs"]![0]!["logRecords"]![0]!["attributes"]!.AsArray();
        attributes.Add(new JsonObject { ["key"] = "input_tokens", ["value"] = new JsonObject { ["intValue"] = tokensIn.ToString(CultureInfo.InvariantCulture) } });
        attributes.Add(new JsonObject { ["key"] = "output_tokens", ["value"] = new JsonObject { ["intValue"] = tokensOut.ToString(CultureInfo.InvariantCulture) } });
    }

    private static void Walk(JsonNode? node, Action<JsonObject> visit)
    {
        switch (node)
        {
            case JsonObject obj:
                visit(obj);
                foreach (var (_, child) in obj.ToList())
                {
                    Walk(child, visit);
                }
                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Walk(child, visit);
                }
                break;
        }
    }

    private ITaskTokenTotals Source() => Source(out _);

    private ITaskTokenTotals Source(out ServiceProvider provider)
    {
        var services = new ServiceCollection();
        new InspectionServices().AddServices(services, new ConfigurationBuilder().Build());
        services.AddSingleton(stack.Options("clickhouse"));
        provider = (ServiceProvider)new DefaultServiceProviderFactory().CreateServiceProvider(services);
        providers.Add(provider);
        return provider.GetRequiredService<ITaskTokenTotals>();
    }
}
