using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.InspectionMetrics;
using ShipLean.Contracts;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.InspectionMetrics;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.InspectionMetrics;

// SHIP-1398 wave 8, the cost journey in the browser, in the empty project EMPT (Joel is its admin). Browser hosts can't reach
// ClickHouse, so a FakeTaskTokenTotals stands in for the store (the real store is CostEndToEndTests in WebApi.Tests), with Inspection
// (agent runs) on for the instance and turned on for EMPT through the settings PUT. Seeded in SQL: a finished sprint (20 to 9 days
// ago) and the running one, and a Task whose lead time ends at its commit 14 days ago, so it heads the finished sprint. The fake
// gives it two runs and 123,456 tokens: the Overview's tokens card and the Cost view read 123.5k, and its Most tokens row opens it.
[TestFixture]
public class CostJourneyTests : SeededAcceptanceTest
{
    private const string Project = "EMPT";
    private const string Tokens = "123.5k";
    private static readonly Guid Empt = SeedIds.Projects.Empt;

    private readonly FakeTaskTokenTotals fake = new();
    private string task = null!;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_INSPECTION"] = "on",
        ["SHIPLEAN_INSPECTION_STORE"] = "tempo-loki",
        ["SHIPLEAN_INSPECTION_TEMPO_URL"] = "http://127.0.0.1:9/tempo",
        ["SHIPLEAN_INSPECTION_LOKI_URL"] = "http://127.0.0.1:9/loki",
    };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<ITaskTokenTotals>();
        services.AddSingleton<ITaskTokenTotals>(fake);
    };

    [OneTimeSetUp]
    public async Task SeedHistoryAsync()
    {
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        var start = new DateTimeOffset(today.AddDays(-16).ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await SprintAsync(connection, "Sprint 1", today.AddDays(-20), today.AddDays(-9));
        await SprintAsync(connection, "Sprint 2", today.AddDays(-6), today.AddDays(7));

        var id = await new WorkItemBuilder(Empt).WithType("task").WithTitle("Token heavy refactor").WithCreatedAt(start.AddHours(-1))
            .WithCreatedBy(SeedIds.Users.Joel).InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @at WHERE work_item_id = @id AND rev = 1", new { id, at = start.AddHours(-1) });
        await new RevisionBuilder(id).WithRev(2).WithChangedAt(start).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"state","from":null,"to":"{{SeedIds.States.Empt.TaskActive}}"}]""").InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @state, revision = 2 WHERE id = @id", new { id, state = SeedIds.States.Empt.TaskActive });
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, link_type, title, url, committed_at) VALUES (gen_random_uuid(), @id, 'url', 'commit c057a1b', @url, @at)",
            new { id, url = "https://git.example.test/acme/empt/commit/c057a1be1e1e1", at = start.AddDays(2) });
        task = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }) ?? throw new InvalidOperationException();
        fake.Totals = fake.Totals with
        {
            ByItem = new Dictionary<string, TaskTokens>(StringComparer.Ordinal) { [task] = new(2, 120_000, 3_456, ["claude-sonnet-5-5"]) },
        };

        using var http = new HttpClient(new HttpClientHandler { UseCookies = true }) { BaseAddress = new Uri(BaseUrl) };
        http.DefaultRequestHeaders.Add(ClientHeader.Name, "acceptance-test");
        using var login = await http.PostAsJsonAsync("/api/auth/login", new { email = TestUser.Joel.Email, password = TestUser.Joel.Password });
        login.EnsureSuccessStatusCode();
        using var put = await http.PutAsJsonAsync($"/api/inspection/projects/{Project}/settings", new InspectionProjectSettingsDto(true, false, null, null, "tempoLoki"));
        put.EnsureSuccessStatusCode();
    }

    // Mutation: in TokensCard, hide the card when cost.available (return null): the Overview has no tokens card.
    [Test]
    public async Task Cost_WhenTaskHasRecordedRuns_ShouldShowItsTokens()
    {
        var inspection = new InspectionMetricsPage(Page);

        // 1. Overview: the tokens card reads the finished sprint's mean tokens per task, and opens the Cost view.
        await Page.GotoAsync(InspectionMetricsPage.Path(Project));
        await Expect(inspection.OverviewTokensCardValue).ToHaveTextAsync(Tokens);
        await inspection.OverviewTokensCard.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/inspection\?view=cost$"));

        // 2. Cost: the per-task card and the task's Most tokens row show the same tokens and its two runs.
        await Expect(inspection.CostPerTaskValue).ToHaveTextAsync(Tokens);
        var row = inspection.CostMostTokensRow(task);
        await Expect(row).ToHaveAttributeAsync("data-key", task);
        await Expect(row).ToContainTextAsync(Tokens);
        await Expect(row).ToContainTextAsync("claude-sonnet-5-5");

        // 3. The row opens the task's item page.
        await inspection.CostMostTokensLink(task).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(Links.WorkItem(Project, task))}$"));
        await Expect(Page.GetByTestId(WorkItemTestIds.Key)).ToHaveTextAsync(task);
    }

    private static async Task SprintAsync(NpgsqlConnection connection, string name, DateOnly start, DateOnly end)
    {
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (gen_random_uuid(), @Empt, @name, CAST(@from AS date), CAST(@to AS date))",
            new { Empt, name, from = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), to = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
    }
}
