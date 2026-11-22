using System.Reflection;
using System.Text.Json;
using Dapper;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Retros.Analysis;

// export_retro_points (C1): the tool surface and the shape of its result, over the fake export service. Shapes only: the names stay true when the real service replaces the fake.
[TestFixture]
public class ExportRetroPointsContractTests : McpFixture
{
    private readonly Guid _analysis = Guid.NewGuid();
    private readonly Guid _repository = Guid.NewGuid();
    private string _featureKey = string.Empty;

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task ArrangeFeature()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = SeedIds.Projects.Ship;
        var feature = await new WorkItemBuilder(project).WithType("feature").WithTitle("Export contract").InsertAsync(connection);
        _featureKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
        var seed = new RetroSeed(connection, project);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: feature, AgentName: "agent-contract"));
        var point = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "A point for the shape.", RootCause: "A cause."));
        await seed.FailureAsync(retro, RetroFailureKind.Blocker, "A blocker.");
        await seed.ActionAsync(new RetroSeedAction(await new WorkItemBuilder(project).WithType("action").InsertAsync(connection), FixType: RetroFixType.Analyzer, PointId: await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Actioned."))));
        await connection.ExecuteAsync(
            "INSERT INTO code_repository (id, provider, host, path) VALUES (@repository, 'github', 'github.com', @path)", new { repository = _repository, path = $"acme/{_repository:N}" });
        await connection.ExecuteAsync(
            """
            INSERT INTO code_commit (repository_id, sha, parent_shas, subject, author_name, authored_at, additions, deletions, files_changed, paths, read_at)
            VALUES (@repository, @sha, '{}', 'A commit', 'Author', now(), 1, 1, 1, '{a.cs}', now())
            """,
            new { repository = _repository, sha = new string('a', 40) });
        await connection.ExecuteAsync(
            "INSERT INTO work_item_commit (id, work_item_id, repository_id, sha, position, added_via) VALUES (@id, @feature, @repository, @sha, 1, 'form')",
            new { id = Guid.NewGuid(), feature, repository = _repository, sha = new string('a', 40) });
        await connection.ExecuteAsync(
            """
            INSERT INTO retro_analysis (id, project_id, scope_kind, range_from, range_to, created_by_user_id, agent_name, embedding_model, report_markdown)
            VALUES (@analysis, @project, 'range', DATE '2001-01-01', DATE '2001-01-31', @user, 'agent-contract', 'e5-small', '# Report');
            INSERT INTO retro_pattern (id, analysis_id, ref, kind, title, lesson, centroid, rank) VALUES (@pattern, @analysis, 'p1', 'problem', 'A pattern', 'A lesson.', '{0.5,0.25}', 1);
            INSERT INTO retro_analysis_point (analysis_id, point_id, outcome, pattern_id) VALUES (@analysis, @point, 'clustered', @pattern);
            """,
            new { analysis = _analysis, project, user = SeedIds.Users.Joel, pattern = Guid.NewGuid(), point });
    }

    [Test]
    public void Tool_WhenListed_ShouldHaveItsScope()
    {
        var tool = typeof(ApiMarker).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Single(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name == "export_retro_points");

        tool.GetCustomAttribute<McpScopeAttribute>()!.Scope.ShouldBe(McpScopes.Read);
    }

    [TestCase("""{"project":"SHIP"}""")]
    [TestCase("""{"project":"SHIP","sprint":"current","from":"2026-10-01","to":"2026-10-02"}""")]
    [TestCase("""{"project":"SHIP","from":"2026-10-01"}""")]
    public async Task Call_WhenNoScopeOrTwoScopes_ShouldBeValidationNamingScope(string arguments)
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "export_retro_points", arguments);

        ErrorField(reply).ShouldBe(("validation", "scope"));
    }

    [Test]
    public async Task Call_WhenBadCursor_ShouldNameCursor()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "export_retro_points", $$"""{"project":"SHIP","feature":"{{_featureKey}}","cursor":"not a cursor!"}""");

        ErrorField(reply).ShouldBe(("validation", "cursor"));
    }

    [TestCase("0")]
    [TestCase("101")]
    public async Task Call_WhenLimitOutOfRange_ShouldNameLimit(string limit)
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "export_retro_points", $$"""{"project":"SHIP","feature":"{{_featureKey}}","limit":{{limit}}}""");

        ErrorField(reply).ShouldBe(("validation", "limit"));
    }

    [Test]
    public async Task Call_WhenWithPatternsFromIsNeitherLatestNorAnId_ShouldNameIt()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "export_retro_points", $$"""{"project":"SHIP","feature":"{{_featureKey}}","with_patterns_from":"last week"}""");

        ErrorField(reply).ShouldBe(("validation", "with_patterns_from"));
    }

    [Test]
    public async Task Call_WhenFeatureScope_ShouldReturnTheC1Shape()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "export_retro_points", $$"""{"project":"SHIP","feature":"{{_featureKey}}","with_patterns_from":"{{_analysis}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var result = reply.Structured();
        Names(result).ShouldBe(["project", "open_url", "scope", "total_points", "next_cursor", "points", "retros", "open_actions", "previous_patterns"], ignoreOrder: true);
        Names(result.GetProperty("scope")).ShouldBe(["kind", "feature", "sprint", "from", "to"], ignoreOrder: true);
        result.GetProperty("scope").GetProperty("kind").GetString().ShouldBe("feature");
        var point = result.GetProperty("points")[0];
        Names(point).ShouldBe(["id", "retro_id", "category", "text", "root_cause", "theme", "logged_at", "agent_name", "role", "item", "feature", "sprint", "commits"], ignoreOrder: true);
        Names(point.GetProperty("item")).ShouldBe(["key", "type", "title", "brief"], ignoreOrder: true);
        Names(point.GetProperty("feature")).ShouldBe(["key", "title", "goal"], ignoreOrder: true);
        Names(point.GetProperty("commits")[0]).ShouldBe(["repository", "sha", "subject", "paths"], ignoreOrder: true);
        var retro = result.GetProperty("retros").EnumerateObject().Single();
        retro.Name.ShouldBe(point.GetProperty("retro_id").GetString());
        Names(retro.Value).ShouldBe(["item", "sprint", "agent_name", "role", "failure_log"], ignoreOrder: true);
        Names(retro.Value.GetProperty("failure_log")).ShouldBe(["blockers", "assumptions_disproved", "friction"], ignoreOrder: true);
        Names(result.GetProperty("open_actions")[0]).ShouldBe(["key", "title", "state", "fix_type", "theme", "point_count"], ignoreOrder: true);
        Names(result.GetProperty("previous_patterns")[0]).ShouldBe(["id", "kind", "title", "lesson", "embedding_model", "centroid"], ignoreOrder: true);
    }

    [Test]
    public async Task Call_WhenNotTheFirstPage_ShouldLeaveOpenActionsAndPatternsOut()
    {
        var cursor = "MTIzNDU2OjAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAw";

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "export_retro_points", $$"""{"project":"SHIP","feature":"{{_featureKey}}","with_patterns_from":"latest","cursor":"{{cursor}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("open_actions").GetArrayLength().ShouldBe(0);
        reply.Structured().GetProperty("previous_patterns").GetArrayLength().ShouldBe(0);
    }

    private static (string Code, string? Field) ErrorField(McpReply reply)
    {
        var error = reply.Structured().GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.TryGetProperty("field", out var field) ? field.GetString() : null);
    }

    private static string[] Names(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name)];
}
