using Dapper;
using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Plans;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Reports;

// A seeded plan in SHIP, a read-write token for Joel, and readers for what report_task wrote. Every test takes its own task,
// so tests do not see each other's reports.
public abstract class ReportTestBase : McpFixture
{
    private static readonly PlanFixture Plan = PlanFixture.FeatureEditor;
    private int _taken;

    protected PlanSeedKeys Keys { get; private set; } = null!;

    protected override bool WithProbeTools => false;

    protected async Task SeedPlanAsync()
    {
        Host.ActAs(SeedIds.Users.Joel);
        Keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, Plan);
    }

    // The key of a task nobody has reported on, in plan order.
    protected string NextTask() => Keys.Tasks[Plan.Stories.SelectMany(s => s.Tasks).ElementAt(_taken++).Title];

    // The last task of the plan, for tests whose call is refused and so writes nothing; NextTask is not meant to reach it.
    protected string RefusedTask => Keys.Tasks[Plan.Stories.SelectMany(s => s.Tasks).Last().Title];

    // The fixture's task behind a key NextTask gave out.
    protected PlanFixtureTask FixtureTaskOf(string key)
        => Plan.Stories.SelectMany(s => s.Tasks).Single(t => Keys.Tasks[t.Title] == key);

    protected async Task<McpReply> ReportAsync(string json)
        => await CallToolAsync(await TokenAsync(TestUser.Joel, ReadWrite), "report_task", json);

    protected async Task<McpReply> PickUpAsync(string key)
        => await CallToolAsync(await TokenAsync(TestUser.Joel, ReadWrite), "pick_up", $$"""{"item":"{{key}}"}""");

    protected async Task<string> StateOfAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT s.name FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.key = @key", new { key });
    }

    protected async Task<Guid?> AssigneeOfAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<Guid?>("SELECT assigned_to FROM work_item WHERE key = @key", new { key });
    }

    protected async Task<IReadOnlyList<CommentRow>> CommentsOfAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return [.. await connection.QueryAsync<CommentRow>(
            "SELECT c.body, c.agent_name AS AgentName, c.superseded_at IS NOT NULL AS Superseded FROM comment c JOIN work_item w ON w.id = c.work_item_id WHERE w.key = @key AND c.archived_at IS NULL ORDER BY c.created_at",
            new { key })];
    }

    protected async Task<IReadOnlyList<LinkRow>> LinksOfAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return [.. await connection.QueryAsync<LinkRow>(
            "SELECT l.url, l.title FROM work_item_link l JOIN work_item w ON w.id = l.source_id WHERE w.key = @key AND l.link_type = 'url' AND l.archived_at IS NULL ORDER BY l.created_at",
            new { key })];
    }

    // The live tasks under the same story as the task that carry the orphan tag, with all their tags.
    protected async Task<IReadOnlyList<OrphanRow>> OrphansUnderStoryOfAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return [.. await connection.QueryAsync<OrphanRow>(
            "SELECT o.key, o.title, o.description, array_to_string(o.tags, ',') AS tags FROM work_item t JOIN work_item o ON o.parent_id = t.parent_id WHERE t.key = @key AND @orphan = ANY (o.tags) ORDER BY o.number",
            new { key, orphan = PlanTags.Orphan })];
    }

    protected sealed record CommentRow(string Body, string? AgentName, bool Superseded);

    protected sealed record LinkRow(string Url, string Title);

    protected sealed class OrphanRow
    {
        public string Key { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string? Description { get; init; }

        public string Tags { get; init; } = string.Empty;

        public IReadOnlyList<string> TagList => Tags.Split(',');

        public PlanTaskTags ParsedTags => PlanTags.Parse(TagList);
    }
}
