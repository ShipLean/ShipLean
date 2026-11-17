using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Inspection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Inspection.ClickHouse;
using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Contracts.Inspection;
using ShipLean.LocalEnvironment.Inspection;
using ShipLean.TestSupport.Inspection;
using ShipLean.WebApi.Tests.Inspection.Contract;

namespace ShipLean.WebApi.Tests.Inspection.Tokens;

// What the ClickHouse ITaskTokenTotals (SHIP-1398 CK1) must return over the shared stack. Each test loads the claude-code fixture
// under a fresh project key: its run is Tagged to one item and linked FromToolCall to another. The source comes from SHIP-1463's
// service module with the stack's ClickHouse options, so the store switch is pinned too and the constructor is not.
// Every negative test first checks that the right query does find the tagged item (the positive control).
[TestFixture(TestName = "ClickHouse tokens per task, SHIP-1398 CT1")]
[Category("SHIP-1398")]
public sealed class ClickHouseTaskTokenTotalsContractTests
{
    private const string FixtureName = "claude-code-synthetic";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(StoreTimeouts.WaitSeconds);
    private readonly List<ServiceProvider> providers = [];
    private readonly FakeTimeProvider clock = new();
    private InspectionStack stack = null!;

    [OneTimeSetUp]
    public async Task StartStack() => stack = await InspectionStack.GetAsync();

    [OneTimeTearDown]
    public async Task DisposeProviders()
    {
        foreach (var provider in providers)
        {
            await provider.DisposeAsync();
        }
    }

    [Test]
    public async Task ReadAsync_WhenTheRunIsTagged_ShouldGiveTheTaggedItemItsTokens()
    {
        var run = await LoadAsync();
        var summary = run.Expected.Summary;

        var totals = await Build(stack.Options("clickhouse")).ReadAsync(Query(run), CancellationToken.None);

        totals.Available.ShouldBeTrue(totals.Problem);
        totals.ByItem.ShouldContainKey(Tagged(run));
        var tokens = totals.ByItem[Tagged(run)];
        tokens.Runs.ShouldBe(1);
        tokens.Total.ShouldBe(summary.TokensIn.ShouldNotBeNull() + summary.TokensOut.ShouldNotBeNull());
        tokens.Models.ShouldBe(summary.Models, ignoreOrder: true);
    }

    [Test]
    public async Task ReadAsync_WhenTheRunHasATaggedLink_ShouldGiveToolCallItemsNothing()
    {
        var run = await LoadAsync();

        var totals = await Build(stack.Options("clickhouse")).ReadAsync(Query(run), CancellationToken.None);

        totals.ByItem.Keys.ShouldBe([Tagged(run)]);
        foreach (var link in run.Expected.Summary.Links.Where(link => link.Source == LinkSource.FromToolCall))
        {
            totals.ByItem.ShouldNotContainKey(link.ItemKey);
        }
    }

    [Test]
    public async Task ReadAsync_WhenAnotherProjectKeyIsUsed_ShouldFindNothing()
    {
        var run = await LoadAsync();
        var source = Build(stack.Options("clickhouse"));
        await AssertFoundAsync(source, run);

        foreach (var other in new[] { run.ProjectKey + "X", run.ProjectKey[..^1], "ZZZZ-NOPE" })
        {
            var totals = await source.ReadAsync(Query(run) with { ProjectKey = other }, CancellationToken.None);

            totals.Available.ShouldBeTrue(totals.Problem);
            totals.ByItem.ShouldBeEmpty($"project {other}");
        }
    }

    [Test]
    public async Task ReadAsync_WhenTheLinkIsOutsideTheWindow_ShouldFindNothing()
    {
        var run = await LoadAsync();
        var source = Build(stack.Options("clickhouse"));
        await AssertFoundAsync(source, run);

        var started = run.Load.StartedAt;
        var before = await source.ReadAsync(Query(run) with { From = started.AddHours(-3), To = started.AddHours(-2) }, CancellationToken.None);
        var after = await source.ReadAsync(Query(run) with { From = started.AddHours(2), To = started.AddHours(3) }, CancellationToken.None);

        before.ByItem.ShouldBeEmpty("window before the run");
        after.ByItem.ShouldBeEmpty("window after the run");
    }

    [Test]
    public async Task ReadAsync_WhenTheStoreIsUnreachable_ShouldReportUnavailableWithoutThrowing()
    {
        var run = await LoadAsync();
        await AssertFoundAsync(Build(stack.Options("clickhouse")), run);
        using var closed = new TcpListener(IPAddress.Loopback, 0);
        closed.Start();
        var port = ((IPEndPoint)closed.LocalEndpoint).Port;
        closed.Stop();
        var options = stack.Options("clickhouse") with
        {
            ClickHouseConnection = $"Host=127.0.0.1;Port={port};Protocol=http;Username=u;Password=p;Database=otel;Timeout=2",
        };

        var totals = await Build(options).ReadAsync(Query(run), CancellationToken.None);

        totals.Available.ShouldBeFalse();
        totals.Problem.ShouldNotBeNullOrWhiteSpace();
        totals.ByItem.ShouldBeEmpty();
    }

    [Test]
    public async Task ReadAsync_WhenTheStoreAnswers_ShouldSayHowFarBackItKeepsRuns()
    {
        var run = await LoadAsync();
        var options = stack.Options("clickhouse");

        var totals = await Build(options).ReadAsync(Query(run), CancellationToken.None);

        totals.KeptSince.ShouldBe(clock.GetUtcNow().AddDays(-(options.RetentionDays + ClickHouseViewSql.ExpiredKeepDays)));
    }

    // A fresh run under its own project key, so no test sees another's.
    private Task<LoadedRun> LoadAsync() => LoadedRun.LoadAsync(stack, InspectionFixtures.Get(FixtureName), 1, Timeout);

    private static string Tagged(LoadedRun run) => run.Expected.Summary.Links.Single(link => link.Source == LinkSource.Tagged).ItemKey;

    private static TaskTokenQuery Query(LoadedRun run)
        => new(run.ProjectKey, [.. run.Expected.Summary.Links.Select(link => link.ItemKey)], run.Load.StartedAt.AddMinutes(-1), run.Load.StartedAt.AddHours(1));

    private static async Task AssertFoundAsync(ITaskTokenTotals source, LoadedRun run)
    {
        var totals = await source.ReadAsync(Query(run), CancellationToken.None);
        totals.ByItem.ShouldContainKey(Tagged(run), $"positive control: {totals.Problem}");
    }

    // The registered ITaskTokenTotals for these options, through SHIP-1463's service module and its store switch.
    private ITaskTokenTotals Build(InspectionOptions options)
    {
        var services = new ServiceCollection();
        new InspectionServices().AddServices(services, new ConfigurationBuilder().Build());
        services.AddSingleton(options);
        services.AddSingleton<TimeProvider>(clock);
        var provider = (ServiceProvider)new DefaultServiceProviderFactory().CreateServiceProvider(services);
        providers.Add(provider);
        var source = provider.GetRequiredService<ITaskTokenTotals>();
        source.ShouldBeOfType<ClickHouseTaskTokenTotals>();
        return source;
    }
}
