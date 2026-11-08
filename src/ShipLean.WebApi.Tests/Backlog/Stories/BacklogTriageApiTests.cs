using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Backlog.Stories;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// Items in an intake (Triage) state: one Story, one Bug and one Feature are moved into Triage states this class adds.
[TestFixture]
public class BacklogTriageApiTests : ApiFixture
{
    private static readonly string Root = $"api/projects/{SeedIds.Projects.Ship}";

    private string _story = "";
    private string _bug = "";
    private string _feature = "";
    private Guid _storyTriage;

    [OneTimeSetUp]
    public async Task Arrange()
    {
        await using var connection = await OpenDatabaseAsync();
        _story = await MoveToTriageAsync(connection, "story");
        _bug = await MoveToTriageAsync(connection, "bug");
        _feature = await MoveToTriageAsync(connection, "feature");
        _storyTriage = await connection.ExecuteScalarAsync<Guid>("SELECT state_id FROM work_item WHERE key = @key", new { key = _story });
    }

    [Test]
    public async Task StoriesBacklog_WhenTriageItem_ShouldHideIt()
    {
        var view = await Api.GetBacklogAsync($"{Root}/backlog?mode=list");

        view.Rows.ShouldNotContain(row => row.Key == _story || row.Key == _bug);
        view.TotalCount.ShouldBe(view.Rows.Count);
        view.Rows.Select(row => row.Rank).ShouldBe(Enumerable.Range(1, view.Rows.Count).Select(n => (int?)n));
    }

    [Test]
    public async Task LevelBacklog_WhenTriageFeature_ShouldHideIt()
    {
        var view = await Api.GetBacklogAsync($"{Root}/backlogs/features");

        view.Rows.ShouldNotContain(row => row.Key == _feature);
    }

    [Test]
    public async Task Backlog_WhenFilterNamesTriage_ShouldShowIt()
    {
        var filter = $$"""{"match":"all","clauses":[{"field":"state","op":"in","value":["{{_storyTriage}}"]}]}""";

        var view = await Api.GetBacklogAsync($"{Root}/backlog?mode=list&filter={Uri.EscapeDataString(filter)}");

        view.Rows.Select(row => row.Key).ShouldBe([_story]);
    }

    [Test]
    public async Task Backlog_WhenFilterNamesOneOfTwoIntakeStatesAmongOthers_ShouldKeepTheOtherHidden()
    {
        // The Bug in Triage B also matches the assignee clause, so only the triage rule keeps it out.
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE work_item SET assigned_to = @ana WHERE key = @bug", new { ana = SeedIds.Users.Ana, bug = _bug });
        var filter = $$"""{"match":"any","clauses":[{"field":"state","op":"in","value":["{{_storyTriage}}"]},{"field":"assignee","op":"in","value":["{{SeedIds.Users.Ana}}"]}]}""";

        var view = await Api.GetBacklogAsync($"{Root}/backlog?mode=list&filter={Uri.EscapeDataString(filter)}");

        view.Rows.Select(row => row.Key).ShouldContain(_story);
        view.Rows.Select(row => row.Key).ShouldNotContain(_bug);
    }

    [Test]
    public async Task Backlog_WhenTwoTypesHaveTriage_ShouldCountBoth()
    {
        var stories = await Api.GetBacklogAsync($"{Root}/backlog?mode=list");
        var features = await Api.GetBacklogAsync($"{Root}/backlogs/features");

        stories.TriageCount.ShouldBe(2);
        stories.TriageStateIds.Count.ShouldBe(2);
        stories.TriageStateIds.ShouldContain(_storyTriage);
        features.TriageCount.ShouldBe(1);
    }

    // Adds a Triage state for the type and moves the type's first open item into it; returns that item's key.
    private static async Task<string> MoveToTriageAsync(Npgsql.NpgsqlConnection connection, string type) =>
        (await connection.ExecuteScalarAsync<string>(
            """
            WITH project AS (SELECT id FROM project WHERE key = 'SHIP'),
            triage AS (
                INSERT INTO state (id, project_id, type, name, category, sort_order, is_intake)
                SELECT gen_random_uuid(), project.id, @type, 'Triage', 'proposed', -1, true FROM project
                RETURNING id, project_id
            ),
            moved AS (
                UPDATE work_item SET state_id = (SELECT id FROM triage)
                WHERE id = (SELECT w.id FROM work_item w JOIN state s ON s.id = w.state_id
                            WHERE w.project_id = (SELECT id FROM project) AND w.type = @type AND s.category <> 'completed' AND s.category <> 'removed'
                            ORDER BY w.key LIMIT 1)
                RETURNING key
            )
            SELECT key FROM moved
            """, new { type }))!;
}
