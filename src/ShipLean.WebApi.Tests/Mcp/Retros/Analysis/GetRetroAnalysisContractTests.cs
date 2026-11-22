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

// get_retro_analysis (C3): the tool surface and the shape of its two results, over analyses seeded by SQL into the C5 tables.
[TestFixture]
public class GetRetroAnalysisContractTests : McpFixture
{
    private Guid _analysisId;

    protected override bool WithProbeTools => false;

    [SetUp]
    public async Task SeedAnalysisAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = SeedIds.Projects.Ship;
        var seed = new RetroSeed(connection, project);
        var item = await new WorkItemBuilder(project).WithType("task").InsertAsync(connection);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: item, AgentName: "agent-contract"));
        var bad = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Checks run too late."));
        var good = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Small commits."));
        var action = await new WorkItemBuilder(project).WithType("action").InsertAsync(connection);
        _analysisId = Guid.NewGuid();
        var problem = Guid.NewGuid();
        var wentWell = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO retro_analysis (id, project_id, scope_kind, range_from, range_to, created_by_user_id, agent_name, embedding_model, rewrite_model, report_markdown)
            VALUES (@analysis, @project, 'range', DATE '2001-01-01', DATE '2001-01-31', @user, 'agent-contract', 'e5-small', 'claude-sonnet', '# Report');
            INSERT INTO retro_pattern (id, analysis_id, ref, kind, title, lesson, rank) VALUES
                (@problem, @analysis, 'p1', 'problem', 'Checks run too late', 'Run the check first.', 1),
                (@wentWell, @analysis, 'w1', 'wentWell', 'Small commits', 'Commit small.', 1);
            INSERT INTO retro_analysis_point (analysis_id, point_id, outcome, pattern_id) VALUES
                (@analysis, @bad, 'clustered', @problem), (@analysis, @good, 'clustered', @wentWell);
            INSERT INTO retro_pattern_fix (id, pattern_id, title, fix_type, enforcement, enforced_by, reason, action_work_item_id, rank) VALUES
                (gen_random_uuid(), @problem, 'Check before apply', 'analyzer', 'notCaught', NULL, 'Nothing checks it.', @action, 1),
                (gen_random_uuid(), @problem, 'Add a lint rule', 'analyzer', 'discarded', NULL, 'Only sounds right.', NULL, 2);
            """,
            new { analysis = _analysisId, project, user = SeedIds.Users.Joel, problem, wentWell, bad, good, action });
    }

    [Test]
    public void Tool_WhenListed_ShouldHaveItsScope()
    {
        var tool = typeof(ApiMarker).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Single(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name == "get_retro_analysis");

        tool.GetCustomAttribute<McpScopeAttribute>()!.Scope.ShouldBe(McpScopes.Read);
    }

    [Test]
    public async Task Get_WithoutId_ShouldListAnalyses()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_retro_analysis", """{"project":"SHIP"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        Names(reply.Structured()).ShouldBe(["project", "open_url", "analyses"], ignoreOrder: true);
        Names(reply.Structured().GetProperty("analyses")[0]).ShouldBe(["id", "scope", "created_at", "agent_name", "patterns", "actions_new"], ignoreOrder: true);
    }

    [Test]
    public async Task Get_WithId_ShouldReturnTheC3Shape()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_retro_analysis", $$"""{"project":"SHIP","id":"{{_analysisId}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var result = reply.Structured();
        Names(result).ShouldBe(
            ["project", "open_url", "id", "scope", "created_at", "agent_name", "embedding_model", "rewrite_model", "funnel", "patterns", "went_well", "fixes_by_tier", "discarded", "report_markdown"],
            ignoreOrder: true);
        result.GetProperty("id").GetGuid().ShouldBe(_analysisId);
        Names(result.GetProperty("scope")).ShouldBe(["kind", "feature", "sprint", "from", "to"], ignoreOrder: true);
        Names(result.GetProperty("funnel")).ShouldBe(
            ["points_in", "noise", "folded", "one_off", "generic", "clustered", "clustered_percent", "unclustered", "patterns_problem", "patterns_went_well", "fixes_proposed", "already_enforced_fully", "already_enforced_partly", "discarded", "actions_new", "actions_added"],
            ignoreOrder: true);
        var pattern = result.GetProperty("patterns")[0];
        Names(pattern).ShouldBe(["id", "ref", "kind", "title", "lesson", "rank", "continues", "evidence", "fixes"], ignoreOrder: true);
        pattern.GetProperty("kind").GetString().ShouldBe("problem");
        Names(pattern.GetProperty("evidence")).ShouldBe(["points", "items", "features", "sprints", "agents", "first_logged", "last_logged"], ignoreOrder: true);
        var fix = pattern.GetProperty("fixes")[0];
        Names(fix).ShouldBe(["title", "fix_type", "enforcement", "enforced_by", "reason", "action"], ignoreOrder: true);
        fix.GetProperty("enforcement").GetString().ShouldBe("notCaught");
        Names(fix.GetProperty("action")).ShouldBe(["key", "state"], ignoreOrder: true);
        result.GetProperty("went_well")[0].GetProperty("kind").GetString().ShouldBe("wentWell");
        var tier = result.GetProperty("fixes_by_tier").EnumerateObject().First();
        Names(tier.Value[0]).ShouldBe(["title", "fix_type", "enforcement", "enforced_by", "reason", "action"], ignoreOrder: true);
        Names(result.GetProperty("discarded")[0]).ShouldBe(["title", "fix_type", "enforcement", "enforced_by", "reason", "action"], ignoreOrder: true);
    }

    [Test]
    public async Task Get_WhenIdIsNotAGuid_ShouldNameId()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_retro_analysis", """{"project":"SHIP","id":"abc"}""");

        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("id");
    }

    private static string[] Names(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name)];
}
