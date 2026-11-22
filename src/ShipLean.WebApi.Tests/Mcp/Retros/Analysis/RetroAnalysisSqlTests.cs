using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Retros.Analysis;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Retros.Analysis;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Mcp.Retros.Analysis;

// The two shared SQL fragments of C1 to C3 on seeded rows: the covered rule (RetroCoveredSql, D22) and the evidence rule (RetroEvidenceSql, D18).
// These read the C5 tables (retro_analysis, retro_analysis_point), which the schema task's migration (SHIP-2204) adds: this class needs that migration merged and is not green without it.
public sealed class RetroAnalysisSqlTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    [TestCase(RetroPointOutcome.Noise, true)]
    [TestCase(RetroPointOutcome.Folded, true)]
    [TestCase(RetroPointOutcome.OneOff, true)]
    [TestCase(RetroPointOutcome.Clustered, false)]
    [TestCase(RetroPointOutcome.Unclustered, false)]
    public async Task Covered_WhenLiveAnalysisRecordedTheOutcome_ShouldFollowTheRule(RetroPointOutcome outcome, bool covered)
    {
        await using var connection = await OpenDatabaseAsync();
        var point = await PointAsync(connection);
        await AnalysisAsync(connection, Project, archived: false, (point, outcome));

        (await CoveredAsync(connection, Project, point)).ShouldBe(covered);
    }

    [Test]
    public async Task Covered_WhenTheAnalysisIsArchived_ShouldBeFalse()
    {
        await using var connection = await OpenDatabaseAsync();
        var point = await PointAsync(connection);
        await AnalysisAsync(connection, Project, archived: true, (point, RetroPointOutcome.Noise));

        (await CoveredAsync(connection, Project, point)).ShouldBeFalse();
    }

    [Test]
    public async Task Covered_WhenTheAnalysisIsOfAnotherProject_ShouldBeFalse()
    {
        await using var connection = await OpenDatabaseAsync();
        var point = await PointAsync(connection);
        await AnalysisAsync(connection, SeedIds.Projects.Pay, archived: false, (point, RetroPointOutcome.Noise));

        (await CoveredAsync(connection, Project, point)).ShouldBeFalse();
    }

    [Test]
    public async Task Evidence_ShouldSpanTheItemsFeaturesSprintsAgentsAndDaysOfThePoints()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var now = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        var feature = await new WorkItemBuilder(Project).WithType("feature").InsertAsync(connection);
        var story = await new WorkItemBuilder(Project).WithType("story").WithParent(feature).InsertAsync(connection);
        var sprint = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@sprint, @Project, @name, DATE '2001-01-01', DATE '2001-01-14')",
            new { sprint, Project, name = $"Evidence {sprint:N}" });
        var onStory = await seed.PointAsync(
            await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: story, AgentName: "agent-evidence-a")), new RetroSeedPoint(RetroCategory.WentBadly, "On the story", CreatedAt: now.AddDays(-3)));
        var onFeature = await seed.PointAsync(
            await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: feature, AgentName: "agent-evidence-b")), new RetroSeedPoint(RetroCategory.Improve, "On the feature", CreatedAt: now.AddDays(-1)));
        var onSprint = await seed.PointAsync(
            await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, SprintId: sprint, AgentName: "agent-evidence-a")), new RetroSeedPoint(RetroCategory.NotInPlan, "On the sprint", CreatedAt: now.AddDays(-2)));
        var storyKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story });
        var featureKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });

        var evidence = await connection.QuerySingleAsync<EvidenceRow>(
            $"""
            SELECT e.points, e.items, e.features, e.sprints, e.agents, e.first_logged AS firstLogged, e.last_logged AS lastLogged
            FROM ({RetroEvidenceSql.ForPoints()}) e
            """,
            new { pointIds = new[] { onStory, onFeature, onSprint } });

        evidence.Points.ShouldBe(3);
        evidence.Items.Cast<string>().ShouldBe(new[] { featureKey, storyKey }.Order(StringComparer.Ordinal).ToArray());
        evidence.Features.Cast<string>().ShouldBe([featureKey]);
        evidence.Sprints.Length.ShouldBe(1);
        evidence.Agents.Cast<string>().ShouldBe(["agent-evidence-a", "agent-evidence-b"]);
        (evidence.LastLogged - evidence.FirstLogged).ShouldBe(TimeSpan.FromDays(2));
    }

    [Test]
    public async Task Evidence_WhenNoPoints_ShouldBeEmpty()
    {
        await using var connection = await OpenDatabaseAsync();

        var evidence = await connection.QuerySingleAsync<EvidenceRow>(
            $"""
            SELECT e.points, e.items, e.features, e.sprints, e.agents, e.first_logged AS firstLogged, e.last_logged AS lastLogged
            FROM ({RetroEvidenceSql.ForPoints()}) e
            """,
            new { pointIds = Array.Empty<Guid>() });

        evidence.Points.ShouldBe(0);
        evidence.Items.Length.ShouldBe(0);
        evidence.FirstLogged.ShouldBeNull();
    }

    private static async Task<Guid> PointAsync(NpgsqlConnection connection)
    {
        var seed = new RetroSeed(connection, Project);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: SeedIds.WorkItems.Ship92, AgentName: $"agent-{Guid.NewGuid():N}"));
        return await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "A point."));
    }

    private static async Task AnalysisAsync(NpgsqlConnection connection, Guid project, bool archived, params (Guid Point, RetroPointOutcome Outcome)[] points)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO retro_analysis (id, project_id, scope_kind, range_from, range_to, created_by_user_id, agent_name, embedding_model, report_markdown, archived_at)
            VALUES (@id, @project, 'range', DATE '2001-01-01', DATE '2001-01-31', @user, 'agent-sql', 'e5-small', '# Report', CASE WHEN @archived THEN now() END)
            """,
            new { id, project, user = SeedIds.Users.Joel, archived });
        foreach (var (point, outcome) in points)
        {
            await connection.ExecuteAsync(
                "INSERT INTO retro_analysis_point (analysis_id, point_id, outcome) VALUES (@id, @point, @outcome)",
                new { id, point, outcome = outcome switch { RetroPointOutcome.OneOff => "oneOff", var other => other.ToString().ToLowerInvariant() } });
        }
    }

    private static Task<bool> CoveredAsync(NpgsqlConnection connection, Guid project, Guid point)
        => connection.ExecuteScalarAsync<bool>($"SELECT {RetroCoveredSql.Covered("p")} FROM retro_point p WHERE p.id = @point", new { projectId = project, point });

    // Array, not string[]: Dapper's constructor matching sees text[] as System.Array (as SprintStatusReads.ItemRow does).
    private sealed record EvidenceRow(int Points, Array Items, Array Features, Array Sprints, Array Agents, DateTime? FirstLogged, DateTime? LastLogged);
}
