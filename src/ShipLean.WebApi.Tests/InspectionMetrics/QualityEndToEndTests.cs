using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.InspectionMetrics;

// SHIP-1398 QTE. The real quality facts reader and aggregator after lead time, on one seeded history in the empty project, through
// GET inspection-metrics, with the clock fixed on Wednesday 18 Nov 2026. Sprints as InspectionMetricsEndToEndTests: Sprint 1 in Q3,
// Sprint 2 finished and Sprint 3 running in Q4. Task T1's lead time ends at its commit in Sprint 1. Bugs: B1 (sev 2) filed in
// Sprint 3 but Caused by T1, so it counts in Sprint 1; B2 (sev 1) escaping in Sprint 2; B3 (sev 3) found before release in Sprint 2;
// B4 (no severity) and B5 (sev 4) escaping in Sprint 3; B6 filed in Sprint 2 but Removed. PRs linked to T1 merged twice in Sprint 2
// and once in Sprint 3; one more merged in Sprint 2 with no link to the project. Weights are the default 8/4/2/1. One scenario per
// class.
[TestFixture]
public class QualityEndToEndTests : ApiFixture
{
    private static readonly Guid Empt = SeedIds.Projects.Empt;
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset S1Day = new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset S2Day = new(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset S3Day = new(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly Dictionary<string, Guid> _sprints = [];
    private readonly Dictionary<string, string> _keys = [];

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<TimeProvider>(_clock);

    [OneTimeSetUp]
    public async Task ArrangeHistory()
    {
        await SprintAsync("Sprint 1", "2026-09-14", "2026-09-25");
        await SprintAsync("Sprint 2", "2026-10-26", "2026-11-06");
        await SprintAsync("Sprint 3", "2026-11-09", "2026-11-20");
        await using var connection = await OpenDatabaseAsync();

        var t1 = await ItemAsync(connection, "T1", "task", "Report date filter", S1Day);
        await new RevisionBuilder(t1).WithRev(2).WithChangedAt(S1Day.AddHours(9)).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"state","from":null,"to":"{{SeedIds.States.Empt.TaskActive}}"}]""").InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @state, revision = 2 WHERE id = @t1", new { t1, state = SeedIds.States.Empt.TaskActive });
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, link_type, title, url, committed_at) VALUES (gen_random_uuid(), @t1, 'url', 'commit abc1234', @url, @at)",
            new { t1, url = "https://git.example.test/acme/empt/commit/abc1234e1e1e1", at = S1Day.AddHours(19) });

        var b1 = await ItemAsync(connection, "B1", "bug", "Date filter drops the last day", S3Day.AddHours(10), severity: 2);
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, target_id, link_type) VALUES (gen_random_uuid(), @b1, @t1, 'causedBy')", new { b1, t1 });
        await ItemAsync(connection, "B2", "bug", "Checkout crashes", S2Day.AddDays(1), severity: 1);
        await ItemAsync(connection, "B3", "bug", "Found in review", S2Day.AddDays(2), severity: 3, foundBeforeRelease: true);
        await ItemAsync(connection, "B4", "bug", "No severity yet", S3Day.AddDays(1));
        await ItemAsync(connection, "B5", "bug", "Typo on Settings", S3Day.AddDays(2), severity: 4);
        var b6 = await ItemAsync(connection, "B6", "bug", "Filed by mistake", S2Day.AddDays(3), severity: 1);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @removed WHERE id = @b6", new { b6, removed = SeedIds.States.Empt.BugRemoved });

        await PullRequestAsync(connection, t1, "https://github.com/acme/empt/pull/1", S2Day.AddDays(3));
        await PullRequestAsync(connection, t1, "https://github.com/acme/empt/pull/2", S2Day.AddDays(6));
        await PullRequestAsync(connection, t1, "https://github.com/acme/empt/pull/3", S3Day.AddDays(3));
        await PullRequestAsync(connection, null, "https://github.com/acme/empt/pull/4", S2Day.AddDays(4));
    }

    // Mutations: in QualityFactsSql.MergedPrs, drop the EXISTS over the project's links (Sprint 2 reads 3 PRs and 33.3); in
    // QualityAggregator, count every bug in its filed sprint (Sprint 1 reads 0 and Sprint 3 3).
    [Test]
    public async Task Get_WhenSeededBugsAndPrs_ShouldReturnExpectedCounts()
    {
        var view = await GetAsync($"?period={_sprints["Sprint 2"]}");

        view.Periods.Select(p => p.Name).ShouldBe(["Sprint 1", "Sprint 2", "Sprint 3"]);
        var stats = view.Periods.Select(p => p.Quality.ShouldNotBeNull()).ToList();
        stats.Select(q => (q.Escaping, q.Weighted, q.FoundBeforeRelease, q.MergedPrs, q.Per100Prs, q.ChangePctVsPrevious)).ShouldBe(
        [
            (1, 4, 0, null, null, null),
            (1, 8, 1, 2, 50.0, 0),
            (2, 1, 0, 1, 200.0, 100),
        ]);
        stats[2].EscapingBySeverity.ShouldBe(new InspectionSeverityCountsDto(0, 0, 0, 1, 1));

        var range = view.Quality.ShouldNotBeNull();
        (range.Escaping, range.Weighted, range.FoundBeforeRelease, range.MergedPrs, range.Per100Prs).ShouldBe((4, 13, 1, 3, 133.3));
        range.EscapingBySeverity.ShouldBe(new InspectionSeverityCountsDto(1, 1, 0, 1, 1));
        view.Quarters.Select(q => (q.Quarter, q.Quality!.Escaping, q.Quality.MergedPrs, q.Quality.Per100Prs)).ShouldBe(
            [("Q3 2026", 1, (int?)null, (double?)null), ("Q4 2026", 3, 3, 100.0)]);
        (view.Headline.Finished.ShouldNotBeNull().Name, view.Headline.Finished.Quality!.Escaping).ShouldBe(("Sprint 2", 1));
        (view.Headline.SoFar.ShouldNotBeNull().Name, view.Headline.SoFar.Quality!.Escaping).ShouldBe(("Sprint 3", 2));

        view.Bugs.ShouldNotBeNull().Select(b => (b.Key, b.PeriodId)).ShouldBe(
        [
            (_keys["B2"], _sprints["Sprint 2"]),
            (_keys["B3"], _sprints["Sprint 2"]),
            (_keys["B1"], _sprints["Sprint 1"]),
            (_keys["B4"], _sprints["Sprint 3"]),
            (_keys["B5"], _sprints["Sprint 3"]),
        ]);
        var caused = view.Bugs.Single(b => b.Key == _keys["B1"]);
        (caused.CausedByKey, caused.CausedByTitle).ShouldBe((_keys["T1"], "Report date filter"));

        var detail = view.PeriodDetail.ShouldNotBeNull();
        (detail.Quality.ShouldNotBeNull().Escaping, detail.Quality.FoundBeforeRelease).ShouldBe((1, 1));
        detail.Bugs.ShouldNotBeNull().Select(b => (b.Key, b.FoundBeforeRelease)).ShouldBe([(_keys["B2"], false), (_keys["B3"], true)]);
    }

    private async Task<InspectionMetricsViewDto> GetAsync(string query)
    {
        using var response = await Api.GetAsync($"api/projects/{Empt}/inspection-metrics{query}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<InspectionMetricsViewDto>(text, Wire)!;
    }

    private async Task SprintAsync(string name, string start, string end)
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @Empt, @name, CAST(@start AS date), CAST(@end AS date))",
            new { id, Empt, name, start, end });
        _sprints[name] = id;
    }

    private async Task<Guid> ItemAsync(
        NpgsqlConnection connection, string name, string type, string title, DateTimeOffset filed, int? severity = null, bool foundBeforeRelease = false)
    {
        var id = await new WorkItemBuilder(Empt).WithType(type).WithTitle(title).WithCreatedAt(filed).WithCreatedBy(SeedIds.Users.Joel).InsertAsync(connection);
        await connection.ExecuteAsync(
            "UPDATE work_item SET severity = @severity, found_before_release = @foundBeforeRelease WHERE id = @id", new { id, severity, foundBeforeRelease });
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @filed WHERE work_item_id = @id AND rev = 1", new { id, filed });
        _keys[name] = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }) ?? throw new InvalidOperationException();
        return id;
    }

    // A merged pull request; linked from the item when one is given.
    private static async Task PullRequestAsync(NpgsqlConnection connection, Guid? item, string url, DateTimeOffset mergedAt)
    {
        if (item is { } source)
        {
            await connection.ExecuteAsync(
                "INSERT INTO work_item_link (id, source_id, link_type, url, title) VALUES (gen_random_uuid(), @source, 'url', @url, 'PR')", new { source, url });
        }
        await connection.ExecuteAsync(
            """
            INSERT INTO link_state (url_key, url, state, as_of, source, merged_at)
            VALUES (link_url_key(@url), @url, 'merged', @mergedAt, 'github', @mergedAt)
            """, new { url, mergedAt });
    }
}
