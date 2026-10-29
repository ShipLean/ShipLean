using System.Net;
using System.Text.Json;
using Dapper;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Projects.Api;

// One seeded scenario for creating projects and checking keys. Joel is an instance admin and a member of SHIP but not PART.
[TestFixture]
public class ProjectCreationApiTests : ApiFixture
{
    private static int _keys;

    private static string NextKey() => "Q" + (char)('A' + (Interlocked.Increment(ref _keys) % 26)) + (char)('A' + (_keys / 26 % 26));

    private static ProjectCreate Request(string key, Guid? copyFrom = null, SprintCreate? sprint = null, int[]? days = null)
        => new($"Created {key}", key, copyFrom, [], days ?? [1, 2, 3, 4, 5], sprint);

    private async Task<ProjectDto> CreateAsync(ProjectCreate create)
    {
        using var response = await Api.PostJsonAsync("api/projects", create);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadAsync<ProjectDto>();
    }

    [Test]
    public async Task Create_WhenCopiedFromProject_ShouldCopySettingsOnly()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE project SET velocity_window = 5 WHERE id = @id", new { id = SeedIds.Projects.Ship });
        var shipStates = (await connection.QueryAsync<Guid>("SELECT id FROM state WHERE project_id = @id", new { id = SeedIds.Projects.Ship })).ToHashSet();
        var shipStory = await connection.ExecuteScalarAsync<Guid>("SELECT id FROM state WHERE project_id = @id AND type = 'story' AND name = 'Active'", new { id = SeedIds.Projects.Ship });
        await AddStateClausesAsync(connection, shipStory);

        var created = await CreateAsync(Request(NextKey(), SeedIds.Projects.Ship, days: [1, 2, 3]));

        created.WorkingDays.ShouldBe([1, 2, 3]);
        created.VelocityWindow.ShouldBe(5);
        created.Role.ShouldBe(ProjectRole.Admin);
        created.MemberCount.ShouldBe(1);
        var live = (await connection.QueryAsync<(string Type, string Name)>("SELECT type, name FROM state WHERE project_id = @id AND archived_at IS NULL", new { id = created.Id })).Order().ToList();
        live.ShouldBe((await connection.QueryAsync<(string Type, string Name)>("SELECT type, name FROM state WHERE project_id = @id AND archived_at IS NULL", new { id = SeedIds.Projects.Ship })).Order().ToList());
        var copiedStates = (await connection.QueryAsync<Guid>("SELECT id FROM state WHERE project_id = @id", new { id = created.Id })).ToHashSet();
        copiedStates.Overlaps(shipStates).ShouldBeFalse();
        var copiedStory = await connection.ExecuteScalarAsync<Guid>("SELECT id FROM state WHERE project_id = @id AND type = 'story' AND name = 'Active'", new { id = created.Id });

        var boards = await connection.QueryAsync<(string Level, string Settings)>("SELECT level, settings::text FROM board WHERE project_id = @id", new { id = created.Id });
        boards.Select(b => b.Level).Order().ShouldBe(["epics", "features", "stories", "taskboard"]);
        foreach (var (_, settings) in boards)
        {
            foreach (var id in Guids(settings))
            {
                shipStates.ShouldNotContain(id);
                copiedStates.ShouldContain(id);
            }
        }
        var stories = boards.Single(b => b.Level == "stories").Settings;
        Guids(stories).ShouldContain(copiedStory);
        var copiedSettings = JsonSerializer.Deserialize<BoardSettingsJson>(stories, PersistedJson.Options)!;
        copiedSettings.Swimlanes.ShouldContain(l => l.Name == "Active only");
        copiedSettings.StyleRules.ShouldContain(r => r.Name == "Active");
        copiedSettings.Swimlanes.ShouldNotContain(l => l.Name == "Sprint only");
        copiedSettings.StyleRules.ShouldNotContain(r => r.Name == "Under parent");
        var shipTaskboard = await connection.ExecuteScalarAsync<string>("SELECT settings::text FROM board WHERE project_id = @id AND level = 'taskboard'", new { id = SeedIds.Projects.Ship });
        Normalise(boards.Single(b => b.Level == "taskboard").Settings).ShouldBe(Normalise(shipTaskboard!));

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @id", new { id = created.Id })).ShouldBe(0);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM sprint WHERE project_id = @id", new { id = created.Id })).ShouldBe(0);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM project_member WHERE project_id = @id", new { id = created.Id })).ShouldBe(1);
    }

    [Test]
    public async Task Create_WhenCopiedRuleNamesDeletedState_ShouldDropIt()
    {
        await using var connection = await OpenDatabaseAsync();
        var live = await connection.ExecuteScalarAsync<Guid>("SELECT id FROM state WHERE project_id = @id AND type = 'story' AND name = 'Active'", new { id = SeedIds.Projects.Ship });
        var deleted = Guid.NewGuid();
        var text = await connection.ExecuteScalarAsync<string>("SELECT settings::text FROM board WHERE project_id = @id AND level = 'stories'", new { id = SeedIds.Projects.Ship });
        var settings = JsonSerializer.Deserialize<BoardSettingsJson>(text!, PersistedJson.Options)!;
        var onlyDeleted = new CriteriaJson(CriteriaMatch.All, [new CriteriaClause(FieldIds.State, CriteriaOp.Equal, JsonSerializer.SerializeToElement(deleted.ToString()))]);
        var mixed = new CriteriaJson(CriteriaMatch.All, [new CriteriaClause(FieldIds.State, CriteriaOp.In, JsonSerializer.SerializeToElement(new[] { deleted.ToString(), live.ToString() }))]);
        var changed = settings with
        {
            Swimlanes = [.. settings.Swimlanes, new SwimlaneJson("lane-gone", "Gone lane", null, SwimlaneKind.Criteria, onlyDeleted)],
            StyleRules = [.. settings.StyleRules, new StyleRuleJson("rule-gone", "Gone rule", true, null, true, false, false, onlyDeleted), new StyleRuleJson("rule-mixed", "Mixed rule", true, null, true, false, false, mixed)],
        };
        await connection.ExecuteAsync("UPDATE board SET settings = @json::jsonb WHERE project_id = @id AND level = 'stories'", new { id = SeedIds.Projects.Ship, json = JsonSerializer.Serialize(changed, PersistedJson.Options) });

        var created = await CreateAsync(Request(NextKey(), SeedIds.Projects.Ship));

        var copied = JsonSerializer.Deserialize<BoardSettingsJson>(
            (await connection.ExecuteScalarAsync<string>("SELECT settings::text FROM board WHERE project_id = @id AND level = 'stories'", new { id = created.Id }))!, PersistedJson.Options)!;
        copied.Swimlanes.ShouldNotContain(l => l.Name == "Gone lane");
        copied.StyleRules.ShouldNotContain(r => r.Name == "Gone rule");
        var copiedLive = await connection.ExecuteScalarAsync<Guid>("SELECT id FROM state WHERE project_id = @id AND type = 'story' AND name = 'Active'", new { id = created.Id });
        Guids(JsonSerializer.Serialize(copied.StyleRules.Single(r => r.Name == "Mixed rule"))).ShouldBe([copiedLive]);
    }

    [Test]
    public async Task Create_WhenCopiedBoardHasTransitions_ShouldBindThemToTheCopiesStatesAndDropOneNamingAnArchivedState()
    {
        await using var connection = await OpenDatabaseAsync();
        Task<Guid> StateAsync(Guid project, string name) => connection.ExecuteScalarAsync<Guid>("SELECT id FROM state WHERE project_id = @project AND type = 'story' AND name = @name", new { project, name });
        var (sourceNew, sourceActive, sourceResolved) = (await StateAsync(SeedIds.Projects.Ship, "New"), await StateAsync(SeedIds.Projects.Ship, "Active"), await StateAsync(SeedIds.Projects.Ship, "Resolved"));
        var text = await connection.ExecuteScalarAsync<string>("SELECT settings::text FROM board WHERE project_id = @id AND level = 'stories'", new { id = SeedIds.Projects.Ship });
        var settings = JsonSerializer.Deserialize<BoardSettingsJson>(text!, PersistedJson.Options)!;
        ActionJson[] Actions(string id) => [new ActionJson(id, "agent", "Go.", null)];
        var archived = new StateRefJson(Guid.NewGuid(), "Gone");
        var transitions = new[]
        {
            new TransitionJson("t-valid", WorkItemTypeId.Story, new StateRefJson(sourceNew, "New"), new StateRefJson(sourceActive, "Active"), Actions("a1")),
            new TransitionJson("t-any", WorkItemTypeId.Story, null, new StateRefJson(sourceResolved, "Resolved"), Actions("a2")),
            new TransitionJson("t-archived", WorkItemTypeId.Story, archived, new StateRefJson(sourceActive, "Active"), Actions("a3")),
        };
        await connection.ExecuteAsync("UPDATE board SET settings = @json::jsonb WHERE project_id = @id AND level = 'stories'", new { id = SeedIds.Projects.Ship, json = JsonSerializer.Serialize(settings with { Transitions = transitions }, PersistedJson.Options) });

        var created = await CreateAsync(Request(NextKey(), SeedIds.Projects.Ship));

        var copied = JsonSerializer.Deserialize<BoardSettingsJson>(
            (await connection.ExecuteScalarAsync<string>("SELECT settings::text FROM board WHERE project_id = @id AND level = 'stories'", new { id = created.Id }))!, PersistedJson.Options)!;
        var (copyNew, copyActive, copyResolved) = (await StateAsync(created.Id, "New"), await StateAsync(created.Id, "Active"), await StateAsync(created.Id, "Resolved"));
        copied.Transitions.Select(t => t.Id).ShouldBe(["t-valid", "t-any"]);
        copied.Transitions[0].From.ShouldBe(new StateRefJson(copyNew, "New"));
        copied.Transitions[0].To.ShouldBe(new StateRefJson(copyActive, "Active"));
        copied.Transitions[1].From.ShouldBeNull();
        copied.Transitions[1].To.ShouldBe(new StateRefJson(copyResolved, "Resolved"));
    }

    [Test]
    public async Task Create_WhenCopyFromProjectNotMember_ShouldBeNotFound()
    {
        var key = NextKey();

        using var response = await Api.PostJsonAsync("api/projects", Request(key, SeedIds.Projects.Part));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM project WHERE key = @key", new { key })).ShouldBe(0);
    }

    [Test]
    public async Task Create_WhenSprintOneRequested_ShouldCreateItInSameTransaction()
    {
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

        var created = await CreateAsync(Request(NextKey(), sprint: new SprintCreate("Sprint 1", today, today.AddDays(13), "Start")));

        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<(string Name, string Goal)>("SELECT name, goal FROM sprint WHERE project_id = @id", new { id = created.Id })).ShouldBe(("Sprint 1", "Start"));
    }

    [Test]
    public async Task Create_WhenSprintOneInvalid_ShouldLeaveNoProject()
    {
        var key = NextKey();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

        using var response = await Api.PostJsonAsync("api/projects", Request(key, sprint: new SprintCreate("Sprint 1", today, today.AddDays(-3), "")));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM project WHERE key = @key", new { key })).ShouldBe(0);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM state WHERE project_id NOT IN (SELECT id FROM project)")).ShouldBe(0);
    }

    [Test]
    public async Task KeyCheck_WhenTakenByForeignProject_ShouldSayAnotherProject()
    {
        using var response = await Api.GetAsync("api/projects/key-check?key=PART&name=Anything");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();
        text.ShouldNotContain("Partner Portal");
        var check = JsonSerializer.Deserialize<KeyCheckDto>(text, SprintWire.Options)!;
        check.Available.ShouldBeFalse();
        check.Reason.ShouldBe(KeyCheckReason.TakenByAnotherProject);
        check.Alternatives.ShouldNotBeEmpty();
        check.Alternatives.ShouldNotContain("PART");
        JsonDocument.Parse(text).RootElement.GetProperty("reason").GetString().ShouldBe("takenByAnotherProject");
    }

    [Test]
    public async Task KeyCheck_WhenKeyAndNameBlank_ShouldBeInvalid()
    {
        var check = await Api.GetAsync<KeyCheckDto>("api/projects/key-check?key=&name=");

        check.Available.ShouldBeFalse();
        check.Reason.ShouldBe(KeyCheckReason.Invalid);
    }

    [Test]
    public async Task KeyCheck_WhenTakenByOwnProject_ShouldSayTaken()
    {
        var check = await Api.GetAsync<KeyCheckDto>("api/projects/key-check?key=SHIP&name=Storefront");

        check.Reason.ShouldBe(KeyCheckReason.Taken);
    }

    [Test]
    public async Task KeyCheck_WhenFree_ShouldBeAvailable()
    {
        var check = await Api.GetAsync<KeyCheckDto>($"api/projects/key-check?key={NextKey()}&name=Anything");

        check.Available.ShouldBeTrue();
        check.Reason.ShouldBeNull();
    }

    // A lane and a style rule that name the Story Active state, so the copy has state clauses to remap.
    private static async Task AddStateClausesAsync(Npgsql.NpgsqlConnection connection, Guid stateId)
    {
        var text = await connection.ExecuteScalarAsync<string>("SELECT settings::text FROM board WHERE project_id = @id AND level = 'stories'", new { id = SeedIds.Projects.Ship });
        var settings = JsonSerializer.Deserialize<BoardSettingsJson>(text!, PersistedJson.Options)!;
        var one = new CriteriaJson(CriteriaMatch.All, [new CriteriaClause(FieldIds.State, CriteriaOp.Equal, JsonSerializer.SerializeToElement(stateId.ToString()))]);
        var many = new CriteriaJson(CriteriaMatch.All, [new CriteriaClause(FieldIds.State, CriteriaOp.In, JsonSerializer.SerializeToElement(new[] { stateId.ToString() }))]);
        // Sprint and work-item clauses point at the source's own rows, so a copy drops them.
        var sprint = new CriteriaJson(CriteriaMatch.All, [new CriteriaClause(FieldIds.Sprint, CriteriaOp.Equal, JsonSerializer.SerializeToElement(Guid.NewGuid().ToString()))]);
        var parent = new CriteriaJson(CriteriaMatch.All, [new CriteriaClause(FieldIds.Parent, CriteriaOp.Equal, JsonSerializer.SerializeToElement(Guid.NewGuid().ToString()))]);
        var changed = settings with
        {
            Swimlanes = [.. settings.Swimlanes, new SwimlaneJson("lane-active", "Active only", null, SwimlaneKind.Criteria, one), new SwimlaneJson("lane-sprint", "Sprint only", null, SwimlaneKind.Criteria, sprint)],
            StyleRules = [.. settings.StyleRules, new StyleRuleJson("rule-active", "Active", true, null, true, false, false, many), new StyleRuleJson("rule-parent", "Under parent", true, null, true, false, false, parent)],
        };
        await connection.ExecuteAsync("UPDATE board SET settings = @json::jsonb WHERE project_id = @id AND level = 'stories'", new { id = SeedIds.Projects.Ship, json = JsonSerializer.Serialize(changed, PersistedJson.Options) });
    }

    // Every guid-shaped string in the document. The source here holds only state ids (mappings and state clauses) plus the sprint and
    // parent ids that a copy must drop; settings can also hold user ids, which a copy keeps.
    private static IEnumerable<Guid> Guids(string json)
    {
        using var document = JsonDocument.Parse(json);
        var found = new List<Guid>();
        void Walk(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.String when e.TryGetGuid(out var id):
                    found.Add(id);
                    break;
                case JsonValueKind.Array:
                    e.EnumerateArray().ToList().ForEach(Walk);
                    break;
                case JsonValueKind.Object:
                    e.EnumerateObject().ToList().ForEach(p => Walk(p.Value));
                    break;
            }
        }
        Walk(document.RootElement);
        return found;
    }

    private static string Normalise(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);
}
