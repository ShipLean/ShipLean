using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Retros.Analysis;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.Analysis;

// get_retro_analysis (C3) on the real reads. Each test seeds its analyses by SQL into the C5 tables (never through the writer): Ship for the reads of one analysis, Mob for the list, Pay as the other project.
public sealed class RetroAnalysisReadTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    [Test]
    public async Task Get_WithId_ShouldReturnPatternsInRankOrderWithEvidence()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var feature = await ItemAsync(connection, "feature");
        var story = await ItemAsync(connection, "story", feature);
        var first = await PointOnAsync(seed, story, "agent-read-a");
        var second = await PointOnAsync(seed, feature, "agent-read-b");
        var third = await PointOnAsync(seed, await ItemAsync(connection, "task", story), "agent-read-a");
        var analysis = await AnalysisAsync(connection, Project, feature: feature);
        await PointRowAsync(connection, analysis, first, "clustered");
        await PointRowAsync(connection, analysis, second, "clustered");
        await PointRowAsync(connection, analysis, third, "clustered");
        var earlier = await PatternAsync(connection, analysis, "p-earlier", "problem", 1);
        var later = await PatternAsync(connection, analysis, "p-later", "problem", 2, continues: earlier);
        await AssignAsync(connection, earlier, first, second);
        await AssignAsync(connection, later, third);
        var storyKey = await KeyAsync(connection, story);
        var featureKey = await KeyAsync(connection, feature);

        var view = await GetAsync(Project, analysis);

        view.ShouldNotBeNull();
        view.Patterns.Select(pattern => pattern.Ref).ShouldBe(["p-earlier", "p-later"]);
        view.Patterns[0].Rank.ShouldBe(1);
        view.Patterns[0].Continues.ShouldBeNull();
        view.Patterns[0].Evidence.Points.ShouldBe(2);
        view.Patterns[0].Evidence.Items.ShouldBe([featureKey, storyKey], ignoreOrder: true);
        view.Patterns[1].Evidence.Items.Count.ShouldBe(1);
        view.Patterns[0].Evidence.Features.ShouldBe([featureKey]);
        view.Patterns[0].Evidence.Agents.ShouldBe(["agent-read-a", "agent-read-b"]);
        view.Patterns[1].Continues.ShouldBe(earlier);
        view.Patterns[1].Evidence.Points.ShouldBe(1);
        view.Patterns[1].Evidence.Agents.ShouldBe(["agent-read-a"]);
        view.Scope.Kind.ShouldBe(RetroScopeKind.Feature);
        view.Scope.Feature.ShouldBe(featureKey);
        view.Funnel.PointsIn.ShouldBe(3);
        view.Funnel.Clustered.ShouldBe(3);
        view.Funnel.PatternsProblem.ShouldBe(2);
        view.ReportMarkdown.ShouldBe("# Report");
    }

    [Test]
    public async Task Get_ShouldGroupFixesByTierAndShowActionState()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var point = await PointOnAsync(seed, await ItemAsync(connection, "task"), "agent-read-fixes");
        var analysis = await AnalysisAsync(connection, Project);
        await PointRowAsync(connection, analysis, point, "clustered");
        var pattern = await PatternAsync(connection, analysis, "p1", "problem", 1);
        await AssignAsync(connection, pattern, point);
        var action = await new WorkItemBuilder(Project).WithType("action").WithStateNamed("Active").InsertAsync(connection);
        var actionKey = await KeyAsync(connection, action);
        await connection.ExecuteAsync("UPDATE work_item SET created_at = now() - interval '10 days' WHERE id = @action", new { action });
        await FixAsync(connection, pattern, "A guide line", "guide", "partly", 2, action, "The guide mentions it once");
        await FixAsync(connection, pattern, "An analyzer", "analyzer", "notCaught", 1, action, null);
        await FixAsync(connection, pattern, "Already done", "test", "fully", 3, null, "CI");
        await FixAsync(connection, pattern, "Sounds right", "structure", "discarded", 4, null, null);

        var view = await GetAsync(Project, analysis);

        view.ShouldNotBeNull();
        view.FixesByTier.Keys.ShouldBe(["analyzer", "test", "guide"]);
        view.FixesByTier["analyzer"].Single().Action.ShouldBe(new RetroViewAction(actionKey, "Active"));
        view.FixesByTier["guide"].Single().EnforcedBy.ShouldBe("The guide mentions it once");
        view.FixesByTier["test"].Single().Action.ShouldBeNull();
        view.Patterns.Single().Fixes.Select(fix => fix.Title).ShouldBe(["An analyzer", "A guide line", "Already done"]);
        view.Patterns.Single().Fixes[0].Action.ShouldBe(view.Patterns.Single().Fixes[1].Action);
        view.Discarded.Single().Title.ShouldBe("Sounds right");
        view.Funnel.FixesProposed.ShouldBe(4);
        view.Funnel.AlreadyEnforcedFully.ShouldBe(1);
        view.Funnel.AlreadyEnforcedPartly.ShouldBe(1);
        view.Funnel.Discarded.ShouldBe(1);
        view.Funnel.ActionsNew.ShouldBe(0);
        view.Funnel.ActionsAdded.ShouldBe(1);
    }

    [Test]
    public async Task Get_ShouldListWentWellPatternsSeparately()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var item = await ItemAsync(connection, "task");
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: item, AgentName: "agent-read-ww"));
        var bad = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "A problem"));
        var good = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Small commits"));
        var analysis = await AnalysisAsync(connection, Project);
        await PointRowAsync(connection, analysis, bad, "clustered");
        await PointRowAsync(connection, analysis, good, "clustered");
        var problem = await PatternAsync(connection, analysis, "p1", "problem", 1);
        var wentWell = await PatternAsync(connection, analysis, "w1", "wentWell", 1);
        await AssignAsync(connection, problem, bad);
        await AssignAsync(connection, wentWell, good);

        var view = await GetAsync(Project, analysis);

        view.ShouldNotBeNull();
        view.Patterns.Select(pattern => pattern.Ref).ShouldBe(["p1"]);
        view.WentWell.Select(pattern => pattern.Ref).ShouldBe(["w1"]);
        view.WentWell.Single().Kind.ShouldBe(RetroPatternKind.WentWell);
        view.WentWell.Single().Evidence.Points.ShouldBe(1);
        view.Funnel.PatternsProblem.ShouldBe(1);
        view.Funnel.PatternsWentWell.ShouldBe(1);
    }

    [Test]
    public async Task Get_WhenOtherProjectOrArchived_ShouldBeNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var elsewhere = await AnalysisAsync(connection, SeedIds.Projects.Pay);
        var archived = await AnalysisAsync(connection, Project, archived: true);
        var live = await AnalysisAsync(connection, Project);

        (await GetAsync(Project, elsewhere)).ShouldBeNull();
        (await GetAsync(Project, archived)).ShouldBeNull();
        (await GetAsync(Project, Guid.NewGuid())).ShouldBeNull();
        (await GetAsync(Project, live)).ShouldNotBeNull();
    }

    [Test]
    public async Task List_ShouldReturnNewestTen()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = SeedIds.Projects.Mob;
        var ids = new List<Guid>();
        for (var day = 0; day < 12; day++)
        {
            ids.Add(await AnalysisAsync(connection, project, createdDaysAgo: 30 - day));
        }

        await AnalysisAsync(connection, project, archived: true, createdDaysAgo: 0);
        var pattern = await PatternAsync(connection, ids[11], "p1", "problem", 1);
        await PatternAsync(connection, ids[11], "w1", "wentWell", 1);
        var action = await ItemAsync(connection, "action", project: project);
        await FixAsync(connection, pattern, "Fix", "analyzer", "notCaught", 1, action, null);
        await connection.ExecuteAsync("UPDATE work_item SET created_at = (SELECT created_at FROM retro_analysis WHERE id = @id) - interval '1 minute' WHERE id = @action", new { id = ids[11], action });

        var rows = await ListAsync(project);

        rows.Select(row => row.Id).ShouldBe(Enumerable.Range(2, 10).Reverse().Select(index => ids[index]).ToArray());
        rows[0].Patterns.ShouldBe(2);
        rows[0].ActionsNew.ShouldBe(1);
        rows[0].Scope.Kind.ShouldBe(RetroScopeKind.Range);
        rows[0].Scope.From.ShouldNotBeNull();
    }

    private async Task<RetroAnalysisView?> GetAsync(Guid project, Guid id)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRetroAnalysisReads>().GetAsync(project, id, CancellationToken.None);
    }

    private async Task<IReadOnlyList<RetroAnalysisRow>> ListAsync(Guid project)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRetroAnalysisReads>().ListAsync(project, CancellationToken.None);
    }

    private static async Task<Guid> ItemAsync(NpgsqlConnection connection, string type, Guid? parent = null, Guid? project = null)
    {
        var builder = new WorkItemBuilder(project ?? Project).WithType(type);
        return await (parent is { } id ? builder.WithParent(id) : builder).InsertAsync(connection);
    }

    private static Task<string> KeyAsync(NpgsqlConnection connection, Guid item) => connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @item", new { item });

    private static async Task<Guid> PointOnAsync(RetroSeed seed, Guid item, string agent)
        => await seed.PointAsync(await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: item, AgentName: agent)), new RetroSeedPoint(RetroCategory.WentBadly, "A point."));

    private static async Task<Guid> AnalysisAsync(NpgsqlConnection connection, Guid project, Guid? feature = null, bool archived = false, int createdDaysAgo = 0)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO retro_analysis (id, project_id, scope_kind, feature_id, range_from, range_to, created_by_user_id, agent_name, embedding_model, rewrite_model, report_markdown, created_at, archived_at)
            VALUES (@id, @project, CASE WHEN @feature::uuid IS NULL THEN 'range' ELSE 'feature' END, @feature,
                    CASE WHEN @feature::uuid IS NULL THEN DATE '2001-01-01' END, CASE WHEN @feature::uuid IS NULL THEN DATE '2001-01-31' END,
                    @user, 'agent-read', 'e5-small', 'claude-sonnet', '# Report', now() - make_interval(days => @createdDaysAgo), CASE WHEN @archived THEN now() END)
            """,
            new { id, project, feature, user = SeedIds.Users.Joel, archived, createdDaysAgo });
        return id;
    }

    private static Task PointRowAsync(NpgsqlConnection connection, Guid analysis, Guid point, string outcome)
        => connection.ExecuteAsync("INSERT INTO retro_analysis_point (analysis_id, point_id, outcome) VALUES (@analysis, @point, @outcome)", new { analysis, point, outcome });

    private static async Task<Guid> PatternAsync(NpgsqlConnection connection, Guid analysis, string reference, string kind, int rank, Guid? continues = null)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO retro_pattern (id, analysis_id, ref, kind, title, lesson, rank, continues_pattern_id) VALUES (@id, @analysis, @reference, @kind, @reference, 'A lesson.', @rank, @continues)",
            new { id, analysis, reference, kind, rank, continues });
        return id;
    }

    private static Task AssignAsync(NpgsqlConnection connection, Guid pattern, params Guid[] points)
        => connection.ExecuteAsync("UPDATE retro_analysis_point SET pattern_id = @pattern WHERE point_id = ANY(@points) AND analysis_id = (SELECT analysis_id FROM retro_pattern WHERE id = @pattern)", new { pattern, points });

    private static Task FixAsync(NpgsqlConnection connection, Guid pattern, string title, string fixType, string enforcement, int rank, Guid? action, string? enforcedBy)
        => connection.ExecuteAsync(
            "INSERT INTO retro_pattern_fix (id, pattern_id, title, fix_type, enforcement, enforced_by, reason, action_work_item_id, rank) VALUES (@id, @pattern, @title, @fixType, @enforcement, @enforcedBy, 'Because.', @action, @rank)",
            new { id = Guid.NewGuid(), pattern, title, fixType, enforcement, enforcedBy, action, rank });
}
