using System.Globalization;
using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Schema;

[TestFixture]
public class SeedMetricHistoryTests
{
    // Everything below is derived from revision rows alone: the sprint, state and points of an item at an instant are the
    // `to` of its latest change to that field at or before the instant.
    private const string ReplayPrefix =
        """
        WITH chg AS (
            SELECT r.work_item_id, r.rev, r.changed_at, c ->> 'field' AS field, c -> 'to' AS to_v, c -> 'from' AS from_v
            FROM revision r CROSS JOIN LATERAL jsonb_array_elements(r.changes) AS c
        ),
        item AS (
            SELECT w.id, w.type FROM work_item w WHERE w.project_id = @projectId AND w.type = ANY(@types)
        ),
        points_at AS (
            SELECT i.id, t.at,
                   (SELECT (c.to_v #>> '{}')::numeric FROM chg c WHERE c.work_item_id = i.id AND c.field = 'points' AND c.changed_at <= t.at ORDER BY c.rev DESC LIMIT 1) AS points
            FROM item i CROSS JOIN (SELECT DISTINCT changed_at AS at FROM revision UNION SELECT @probe) AS t
        )
        """;

    private ClonedDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await ShipLeanDatabase.CloneSeedAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Seed_WhenLoaded_ShouldCommitAndCompleteAsDesigned()
    {
        await using var connection = await _database.OpenAsync();

        var figures = await FiguresAsync(connection, ["story", "bug"]);

        figures.Where(f => f.Sprint is >= 19 and <= 23).OrderBy(f => f.Sprint).Select(f => $"{f.Sprint}: {f.Committed}/{f.Completed}")
            .ShouldBe(["19: 18/16", "20: 22/19", "21: 20/20", "22: 24/22", "23: 23/21"]);
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldBurnDownSprint24As21To18To16()
    {
        await using var connection = await _database.OpenAsync();
        var sprint = await connection.QuerySingleAsync<(DateTime Start, DateTime End)>(
            "SELECT start_date::timestamp AS start, end_date::timestamp AS \"end\" FROM sprint WHERE id = @id", new { id = SeedIds.Sprints.Sprint24 });
        var workingDays = Enumerable.Range(0, (sprint.End - sprint.Start).Days + 1).Select(i => sprint.Start.AddDays(i))
            .Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Take(3).ToArray();

        var remaining = new List<decimal>();
        foreach (var day in workingDays)
        {
            var endOfDay = new DateTimeOffset(DateTime.SpecifyKind(day.AddDays(1), DateTimeKind.Utc));
            remaining.Add(await connection.ExecuteScalarAsync<decimal>(
                """
                SELECT coalesce(sum(p.points), 0) FROM (
                    SELECT i.id,
                           (SELECT (c.to_v #>> '{}')::numeric FROM (
                                SELECT r.rev, r.changed_at, c.value ->> 'field' AS field, c.value -> 'to' AS to_v
                                FROM revision r CROSS JOIN LATERAL jsonb_array_elements(r.changes) AS c
                                WHERE r.work_item_id = i.id) AS c
                            WHERE c.field = 'points' AND c.changed_at <= @at ORDER BY c.rev DESC LIMIT 1) AS points
                    FROM work_item i
                    WHERE i.project_id = @projectId AND i.type IN ('story', 'bug')
                      AND (SELECT (c.to_v #>> '{}')::uuid FROM (
                                SELECT r.rev, r.changed_at, c.value ->> 'field' AS field, c.value -> 'to' AS to_v
                                FROM revision r CROSS JOIN LATERAL jsonb_array_elements(r.changes) AS c
                                WHERE r.work_item_id = i.id) AS c
                            WHERE c.field = 'sprint' AND c.changed_at <= @at ORDER BY c.rev DESC LIMIT 1) = @sprint
                      AND coalesce((SELECT s.category FROM (
                                SELECT r.rev, r.changed_at, c.value ->> 'field' AS field, c.value -> 'to' AS to_v
                                FROM revision r CROSS JOIN LATERAL jsonb_array_elements(r.changes) AS c
                                WHERE r.work_item_id = i.id) AS c
                            JOIN state s ON s.id = (c.to_v #>> '{}')::uuid
                            WHERE c.field = 'state' AND c.changed_at <= @at ORDER BY c.rev DESC LIMIT 1), 'proposed') <> 'completed'
                ) AS p
                """,
                new { projectId = SeedIds.Projects.Ship, at = endOfDay, sprint = SeedIds.Sprints.Sprint24 }));
        }

        remaining.ShouldBe([21m, 18m, 16m]);
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldCarrySHIP138FromSprint23()
    {
        await using var connection = await _database.OpenAsync();
        var sprint24Start = await connection.ExecuteScalarAsync<DateTime>("SELECT start_date::timestamp FROM sprint WHERE id = @id", new { id = SeedIds.Sprints.Sprint24 });
        var lastWorkingDayBefore = sprint24Start.AddDays(-1);
        while (lastWorkingDayBefore.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            lastWorkingDayBefore = lastWorkingDayBefore.AddDays(-1);
        }

        var moves = (await connection.QueryAsync<(int Rev, DateTime At, string FromLabel, string ToLabel, string FromId, string ToId)>(
            """
            SELECT r.rev, r.changed_at AT TIME ZONE 'utc' AS at, c ->> 'fromLabel' AS fromLabel, c ->> 'toLabel' AS toLabel, c ->> 'from' AS fromId, c ->> 'to' AS toId
            FROM revision r CROSS JOIN LATERAL jsonb_array_elements(r.changes) AS c
            WHERE r.work_item_id = @id AND c ->> 'field' = 'sprint' AND c ->> 'from' IS NOT NULL
            """,
            new { id = SeedIds.WorkItems.Ship138 })).ToArray();
        var item = await connection.QuerySingleAsync<(string Sprint, int Revision)>(
            "SELECT sprint_id::text AS sprint, revision FROM work_item WHERE id = @id", new { id = SeedIds.WorkItems.Ship138 });
        var revisions = (await connection.QueryAsync<int>("SELECT rev FROM revision WHERE work_item_id = @id ORDER BY rev", new { id = SeedIds.WorkItems.Ship138 })).ToArray();

        var move = moves.ShouldHaveSingleItem();
        move.FromLabel.ShouldBe("Sprint 23");
        move.ToLabel.ShouldBe("Sprint 24");
        move.FromId.ShouldBe(SeedIds.Sprints.Sprint23.ToString());
        move.ToId.ShouldBe(SeedIds.Sprints.Sprint24.ToString());
        move.At.ShouldBe(lastWorkingDayBefore.AddHours(17));
        item.Sprint.ShouldBe(SeedIds.Sprints.Sprint24.ToString());
        revisions.ShouldBe(Enumerable.Range(1, item.Revision));
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldNeverCountTaskPoints()
    {
        await using var connection = await _database.OpenAsync();

        var pastTasks = await connection.ExecuteScalarAsync<int>(
            """
            SELECT count(*)::int FROM work_item w JOIN sprint ON sprint.id = w.sprint_id
            WHERE w.type = 'task' AND w.story_points > 0 AND sprint.end_date < (now() AT TIME ZONE 'utc')::date
            """);
        var withTasks = await FiguresAsync(connection, ["story", "bug", "task"]);
        var withoutTasks = await FiguresAsync(connection, ["story", "bug"]);

        pastTasks.ShouldBeGreaterThan(0);
        withTasks.Single(f => f.Sprint == 22).ShouldNotBe(withoutTasks.Single(f => f.Sprint == 22));
        withoutTasks.Single(f => f.Sprint == 22).ShouldBe(new Figures(22, 24m, 22m));
    }

    // Committed and completed points per sprint for the given item types, from the revisions only.
    private async Task<Figures[]> FiguresAsync(Npgsql.NpgsqlConnection connection, string[] types) => (await connection.QueryAsync<Figures>(
        ReplayPrefix +
        """
        , sprint_at AS (
            SELECT i.id AS item_id, t.at,
                   (SELECT (c.to_v #>> '{}')::uuid FROM chg c WHERE c.work_item_id = i.id AND c.field = 'sprint' AND c.changed_at <= t.at ORDER BY c.rev DESC LIMIT 1) AS sprint_id
            FROM item i CROSS JOIN (SELECT DISTINCT changed_at AS at FROM revision UNION SELECT @probe) AS t
        ),
        committed AS (
            SELECT sp.id AS sprint_id, sum(p.points) AS points
            FROM sprint sp
            JOIN sprint_at s ON s.at = (SELECT max(x.at) FROM sprint_at x WHERE x.item_id = s.item_id AND x.at <= (sp.start_date::timestamp AT TIME ZONE 'utc'))
                            AND s.sprint_id = sp.id
            JOIN points_at p ON p.id = s.item_id AND p.at = s.at
            WHERE sp.project_id = @projectId
            GROUP BY sp.id
        ),
        completed AS (
            SELECT sp.id AS sprint_id, sum(p.points) AS points
            FROM chg c
            JOIN state done ON done.id = (c.to_v #>> '{}')::uuid AND done.category = 'completed'
            LEFT JOIN state before ON before.id = (c.from_v #>> '{}')::uuid
            JOIN item i ON i.id = c.work_item_id
            JOIN sprint_at s ON s.item_id = i.id AND s.at = c.changed_at
            JOIN sprint sp ON sp.id = s.sprint_id
            JOIN points_at p ON p.id = i.id AND p.at = c.changed_at
            WHERE c.field = 'state' AND before.category IS DISTINCT FROM 'completed'
              AND c.changed_at >= (sp.start_date::timestamp AT TIME ZONE 'utc')
              AND c.changed_at < ((sp.end_date + 1)::timestamp AT TIME ZONE 'utc')
            GROUP BY sp.id
        )
        SELECT regexp_replace(sp.name, '\D', '', 'g')::int AS sprint, coalesce(committed.points, 0) AS committed, coalesce(completed.points, 0) AS completed
        FROM sprint sp
        LEFT JOIN committed ON committed.sprint_id = sp.id
        LEFT JOIN completed ON completed.sprint_id = sp.id
        WHERE sp.project_id = @projectId
        ORDER BY sp.start_date
        """,
        new { projectId = SeedIds.Projects.Ship, types, probe = DateTimeOffset.UnixEpoch })).ToArray();

    private sealed record Figures(int Sprint, decimal Committed, decimal Completed);
}
