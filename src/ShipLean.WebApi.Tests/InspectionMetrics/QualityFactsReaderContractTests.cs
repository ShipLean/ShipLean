using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.InspectionMetrics.Quality;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.InspectionMetrics;

// What the real quality facts reader (QT4) must return (QK4), on rows written in SQL: a task, two bugs filed in the window (one
// found before release, one caused by the task), a bug filed long before it, a pull request linked to the caused bug and merged in
// the window, and custom severity weights. Red until QT4 replaces the stub reader: each test fails because the stub reads nothing.
[TestFixture]
public class QualityFactsReaderContractTests : ApiFixture
{
    private const string PrUrl = "https://github.com/acme/ship/pull/7";

    private static readonly Guid Ship = SeedIds.Projects.Ship;
    private static readonly DateTimeOffset T0 = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    private string _task = string.Empty;
    private string _caused = string.Empty;
    private string _found = string.Empty;
    private string _old = string.Empty;
    private BugFacts _facts = null!;

    [OneTimeSetUp]
    public async Task Arrange()
    {
        await using var connection = await OpenDatabaseAsync();
        var task = await new WorkItemBuilder(Ship).WithType("task").WithTitle("Report date filter").WithCreatedAt(T0.AddDays(-3)).InsertAsync(connection);
        var caused = await new WorkItemBuilder(Ship).WithType("bug").WithTitle("Totals off by one day").WithCreatedAt(T0).InsertAsync(connection);
        var found = await new WorkItemBuilder(Ship).WithType("bug").WithTitle("CSV export breaks").WithCreatedAt(T0.AddDays(1)).InsertAsync(connection);
        var old = await new WorkItemBuilder(Ship).WithType("bug").WithTitle("Filed long ago").WithCreatedAt(T0.AddMonths(-9)).InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET severity = 1 WHERE id = @caused", new { caused });
        await connection.ExecuteAsync("UPDATE work_item SET severity = 3, found_before_release = true WHERE id = @found", new { found });
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, target_id, link_type) VALUES (gen_random_uuid(), @caused, @task, 'causedBy')", new { caused, task });
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, link_type, url, title) VALUES (gen_random_uuid(), @caused, 'url', @url, 'Fix totals')", new { caused, url = PrUrl });
        await connection.ExecuteAsync(
            """
            INSERT INTO link_state (url_key, url, state, as_of, source, merged_at)
            VALUES (link_url_key(@url), @url, 'merged', @asOf, 'github', @at)
            """, new { url = PrUrl, at = T0.AddDays(2), asOf = T0.AddDays(3) });
        await connection.ExecuteAsync(
            """UPDATE project SET bug_severity_weights = '{"1":10,"2":5,"3":2,"4":1}' WHERE id = @Ship""", new { Ship });
        var keys = (await connection.QueryAsync<(Guid Id, string Key)>("SELECT id, key FROM work_item WHERE id = ANY(@ids)", new { ids = new[] { task, caused, found, old } }))
            .ToDictionary(row => row.Id, row => row.Key);
        (_task, _caused, _found, _old) = (keys[task], keys[caused], keys[found], keys[old]);

        await using var scope = Host.Services.CreateAsyncScope();
        _facts = await scope.ServiceProvider.GetRequiredService<IQualityFactsReader>()
            .ReadAsync(Ship, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 16), CancellationToken.None);
    }

    // Mutation: select found_before_release as false.
    [Test]
    public void Read_WhenBugsFiledInTheWindow_ShouldReturnTheirFieldsTurnedGreenByQT4()
    {
        _facts.Bugs.Where(b => b.Key == _caused || b.Key == _found).Select(b => (b.Key, b.Severity, b.FiledAt, b.FoundBeforeRelease))
            .ShouldBe([(_caused, (int?)1, T0, false), (_found, 3, T0.AddDays(1), true)], ignoreOrder: true);
        _facts.Bugs.ShouldNotContain(b => b.Key == _old);
    }

    // Mutation: drop the causedBy join.
    [Test]
    public void Read_WhenBugHasCausedBy_ShouldCarryTheCauseTaskFactsTurnedGreenByQT4()
    {
        _facts.Bugs.Single(b => b.Key == _caused).CausedByKey.ShouldBe(_task);
        _facts.Bugs.Single(b => b.Key == _found).CausedByKey.ShouldBeNull();
        _facts.Causes.Tasks.Single(t => t.Key == _task).Title.ShouldBe("Report date filter");
    }

    // Mutation: read link_state.as_of instead of merged_at.
    [Test]
    public void Read_WhenPrLinkedAndMergedInTheWindow_ShouldReturnItTurnedGreenByQT4()
    {
        _facts.MergedPrs.Select(pr => (pr.Url, pr.MergedAt)).ShouldBe([(PrUrl, T0.AddDays(2))]);
    }

    // Mutation: return the column default instead of the project's weights.
    [Test]
    public void Read_WhenProjectWeightsSet_ShouldReturnThemTurnedGreenByQT4()
    {
        _facts.SeverityWeights.OrderBy(w => w.Key).Select(w => (w.Key, w.Value)).ShouldBe([(1, 10), (2, 5), (3, 2), (4, 1)]);
    }
}
