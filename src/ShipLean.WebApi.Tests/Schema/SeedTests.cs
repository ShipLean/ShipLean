using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.AspNetCore.Identity;
using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Schema;

[TestFixture]
public class SeedTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Seed_WhenLoaded_ShouldHaveSprint24Current()
    {
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        var anchor = today;
        while (anchor.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            anchor = anchor.AddDays(-1);
        }
        var start = anchor;
        for (var counted = 0; counted < 3;)
        {
            start = start.AddDays(-1);
            if (start.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                counted++;
            }
        }
        await using var connection = await _database.OpenAsync();

        var sprints = (await connection.QueryAsync<SprintRow>(
            "SELECT name, start_date::text AS startDate, end_date::text AS endDate, rollover_status AS rolloverStatus FROM sprint ORDER BY start_date")).ToArray();

        sprints.Select(s => s.Name).ShouldBe(Enumerable.Range(19, 8).Select(n => $"Sprint {n}"));
        var sprint24 = sprints.Single(s => s.Name == "Sprint 24");
        DateOnly.Parse(sprint24.StartDate, CultureInfo.InvariantCulture).ShouldBe(start);
        DateOnly.Parse(sprint24.EndDate, CultureInfo.InvariantCulture).ShouldBe(start.AddDays(13));
        DateOnly.Parse(sprint24.StartDate, CultureInfo.InvariantCulture).ShouldBeLessThanOrEqualTo(today);
        DateOnly.Parse(sprint24.EndDate, CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(today);
        for (var i = 0; i < sprints.Length; i++)
        {
            DateOnly.Parse(sprints[i].StartDate, CultureInfo.InvariantCulture).ShouldBe(start.AddDays((i - 5) * 14));
            DateOnly.Parse(sprints[i].EndDate, CultureInfo.InvariantCulture).ShouldBe(start.AddDays((i - 5) * 14 + 13));
            sprints[i].RolloverStatus.ShouldBe(i < 5 ? "decided" : null);
        }
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldMatchPinnedRefs()
    {
        var refs = PinnedFiles.Read("refs.json");
        await using var connection = await _database.OpenAsync();

        foreach (var user in refs["userRefs"]!.AsArray())
        {
            var row = await connection.QuerySingleAsync<UserRow>(
                "SELECT name, initials, avatar_colour AS avatarColour, avatar_photo_blob IS NULL AS noPhoto FROM app_user WHERE id = @id",
                new { id = Guid.Parse(user!["id"]!.GetValue<string>()) });
            row.Name.ShouldBe(user["name"]!.GetValue<string>());
            row.Initials.ShouldBe(user["initials"]!.GetValue<string>());
            row.AvatarColour.ShouldBe(user["avatarColour"]!.GetValue<string>());
            row.NoPhoto.ShouldBe(user["photoUrl"] is null);
        }
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user")).ShouldBe(typeof(SeedIds.Users).GetFields(BindingFlags.Public | BindingFlags.Static).Length);

        var projectId = SeedIds.Projects.Ship;
        var expectedStates = 0;
        foreach (var (type, states) in refs["stateRefs"]!.AsObject())
        {
            var order = 0;
            foreach (var state in states!.AsArray())
            {
                order++;
                expectedStates++;
                var row = await connection.QuerySingleAsync<StateRow>(
                    "SELECT name, category, type, sort_order AS sortOrder FROM state WHERE id = @id AND project_id = @projectId",
                    new { id = Guid.Parse(state!["id"]!.GetValue<string>()), projectId });
                row.Name.ShouldBe(state["name"]!.GetValue<string>());
                row.Category.ShouldBe(state["category"]!.GetValue<string>());
                row.Type.ShouldBe(type);
                row.SortOrder.ShouldBe(order);
            }
        }
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM state WHERE project_id = @projectId", new { projectId })).ShouldBe(expectedStates);

        foreach (var sprint in refs["sprintRefs"]!.AsArray())
        {
            var name = await connection.ExecuteScalarAsync<string>(
                "SELECT name FROM sprint WHERE id = @id AND project_id = @projectId",
                new { id = Guid.Parse(sprint!["id"]!.GetValue<string>()), projectId });
            name.ShouldBe(sprint["name"]!.GetValue<string>());
        }

        foreach (var type in refs["typeRefs"]!.AsArray())
        {
            var id = type!["id"]!.GetValue<string>();
            (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @projectId AND type = @id", new { projectId, id }))
                .ShouldBeGreaterThan(0, $"no SHIP work item of type {id}");
        }
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldMatchRevisionReplay()
    {
        await using var connection = await _database.OpenAsync();
        var items = (await connection.QueryAsync<ItemRow>(
            "SELECT id, state_id AS stateId, sprint_id AS sprintId, story_points::float8 AS points, assigned_to AS assignee, parent_id AS parentId, revision FROM work_item"))
            .ToArray();
        items.Length.ShouldBeGreaterThan(0);
        var revisions = (await connection.QueryAsync<RevisionRow>("SELECT work_item_id AS workItemId, rev, changes::text AS changes FROM revision ORDER BY work_item_id, rev"))
            .ToLookup(r => r.WorkItemId);

        foreach (var item in items)
        {
            var replay = new Dictionary<string, JsonNode?>();
            foreach (var revision in revisions[item.Id].OrderBy(r => r.Rev))
            {
                foreach (var change in JsonNode.Parse(revision.Changes)!.AsArray())
                {
                    var field = change!["field"]!.GetValue<string>();
                    var before = replay.GetValueOrDefault(field);
                    var from = change["from"];
                    var bothEmpty = (before is null || before is JsonArray { Count: 0 }) && (from is null || from is JsonArray { Count: 0 });
                    (bothEmpty || JsonNode.DeepEquals(before, from)).ShouldBeTrue($"from of {field} in rev {revision.Rev} of {item.Id} contradicts the replay so far");
                    replay[field] = change["to"]?.DeepClone();
                }
            }
            Guid? Id(string field) => replay.GetValueOrDefault(field) is { } v ? Guid.Parse(v.GetValue<string>()) : null;
            Id("state").ShouldBe(item.StateId, $"state of {item.Id}");
            Id("sprint").ShouldBe(item.SprintId, $"sprint of {item.Id}");
            Id("assignee").ShouldBe(item.Assignee, $"assignee of {item.Id}");
            Id("parent").ShouldBe(item.ParentId, $"parent of {item.Id}");
            (replay.GetValueOrDefault("points") is { } p ? p.GetValue<double>() : (double?)null).ShouldBe(item.Points, $"points of {item.Id}");
            revisions[item.Id].Max(r => r.Rev).ShouldBe(item.Revision);
        }
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldStoreThePinnedImportProgress()
    {
        var pinned = PinnedFiles.Read("import-job-state.json")["progress"]!["mobFinished"]!;
        await using var connection = await _database.OpenAsync();

        var job = await connection.QuerySingleAsync<ImportJobRow>(
            "SELECT progress::text AS progress, jira_project_key AS jiraProjectKey, project_key AS projectKey, status FROM import_job WHERE id = @id",
            new { id = SeedIds.ImportJobs.MobImport });

        JsonNode.DeepEquals(JsonNode.Parse(job.Progress), pinned["document"]).ShouldBeTrue();
        job.JiraProjectKey.ShouldBe(pinned["row"]!["jiraProjectKey"]!.GetValue<string>());
        job.ProjectKey.ShouldBe(pinned["row"]!["projectKey"]!.GetValue<string>());
        job.Status.ShouldBe(pinned["row"]!["status"]!.GetValue<string>());
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM import_job")).ShouldBe(1);
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldWriteRevisionChangesInThePinnedShape()
    {
        var fields = PinnedFiles.Read("field-ids.json").AsArray();
        var order = fields.Select(f => f!["id"]!.GetValue<string>()).ToList();
        var history = fields.Where(f => f!["usedIn"]!.AsArray().Any(u => u!.GetValue<string>() == "history")).Select(f => f!["id"]!.GetValue<string>()).ToHashSet();
        string[] keys = ["field", "from", "fromLabel", "to", "toLabel"];
        await using var connection = await _database.OpenAsync();

        var all = (await connection.QueryAsync<string>("SELECT changes::text FROM revision")).ToArray();

        all.Length.ShouldBeGreaterThan(0);
        foreach (var changes in all)
        {
            var previous = -1;
            foreach (var change in JsonNode.Parse(changes)!.AsArray())
            {
                change!.AsObject().Select(p => p.Key).Order().ShouldBe(keys);
                var field = change["field"]!.GetValue<string>();
                history.ShouldContain(field);
                var index = order.IndexOf(field);
                index.ShouldBeGreaterThan(previous, $"{field} is out of field-ids order");
                previous = index;
            }
        }
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldHaveVerifiablePasswords()
    {
        await using var connection = await _database.OpenAsync();
        var hasher = new PasswordHasher<object>();

        var users = (await connection.QueryAsync<PasswordRow>("SELECT name, password_hash AS hash, must_change_password AS mustChange, is_admin AS isAdmin FROM app_user WHERE password_hash IS NOT NULL")).ToArray();

        users.Length.ShouldBe(10);
        foreach (var user in users)
        {
            var first = user.Name.Split(' ')[0];
            var password = (string)typeof(SeedIds.Passwords).GetField(first, BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue()!;
            hasher.VerifyHashedPassword(new object(), user.Hash, password).ShouldBe(PasswordVerificationResult.Success, user.Name);
        }
        users.Where(u => u.MustChange).Select(u => u.Name).Order().ShouldBe(["Ana Costa", "Ravi Menon"]);
        users.Where(u => u.IsAdmin).Select(u => u.Name).Order().ShouldBe(["Joel Dickson", "Tom Okafor"]);
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldStoreThePinnedBoardSettings()
    {
        await using var connection = await _database.OpenAsync();

        foreach (var level in new[] { "stories", "features", "epics", "taskboard" })
        {
            var settings = await connection.ExecuteScalarAsync<string>(
                "SELECT settings::text FROM board WHERE project_id = @projectId AND level = @level",
                new { projectId = SeedIds.Projects.Ship, level });
            JsonNode.DeepEquals(JsonNode.Parse(settings!), PinnedFiles.Read($"board-settings.{level}.json")).ShouldBeTrue(level);
        }
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldGiveEveryProjectFourBoardsMappedToItsOwnStates()
    {
        await using var connection = await _database.OpenAsync();

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM project")).ShouldBe(8);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM board")).ShouldBe(32);
        var states = (await connection.QueryAsync<(Guid Id, Guid ProjectId, string Category)>("SELECT id, project_id AS projectId, category FROM state")).ToDictionary(s => s.Id);
        var boards = await connection.QueryAsync<BoardRow>("SELECT project_id AS projectId, level, settings::text AS settings FROM board");
        foreach (var board in boards)
        {
            var columns = JsonNode.Parse(board.Settings)!["columns"]!.AsArray();
            (board.Level == "taskboard").ShouldBe(columns.Count == 0);
            columns.Select(c => c!["id"]!.GetValue<string>()).Distinct().Count().ShouldBe(columns.Count);
            foreach (var mapping in columns.SelectMany(c => c!["stateMappings"]!.AsArray()))
            {
                var state = states[Guid.Parse(mapping!["stateId"]!.GetValue<string>())];
                state.ProjectId.ShouldBe(board.ProjectId);
                state.Category.ShouldNotBe("removed");
            }
        }
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldHaveNoFutureOrWeekendStateChanges()
    {
        await using var connection = await _database.OpenAsync();

        var bad = await connection.QueryAsync<string>(
            """
            SELECT w.key || ' state_changed_at ' || w.state_changed_at FROM work_item w
            LEFT JOIN sprint ON sprint.id = w.sprint_id
            LEFT JOIN state ON state.id = w.state_id
            WHERE w.state_changed_at > now()
               OR extract(isodow FROM w.state_changed_at AT TIME ZONE 'utc') > 5
               OR (state.category = 'completed' AND sprint.id IS NOT NULL
                   AND (w.state_changed_at AT TIME ZONE 'utc')::date NOT BETWEEN sprint.start_date AND sprint.end_date)
            UNION ALL
            SELECT w.key || ' revision ' || r.rev || ' changed_at ' || r.changed_at FROM revision r
            JOIN work_item w ON w.id = r.work_item_id
            LEFT JOIN sprint ON sprint.id = w.sprint_id
            LEFT JOIN state ON state.id = w.state_id
            WHERE r.changed_at > now()
               OR extract(isodow FROM r.changed_at AT TIME ZONE 'utc') > 5
               OR (state.category = 'completed' AND sprint.id IS NOT NULL AND r.changed_at = w.state_changed_at
                   AND (r.changed_at AT TIME ZONE 'utc')::date NOT BETWEEN sprint.start_date AND sprint.end_date)
            """);

        bad.ShouldBeEmpty();
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldKeepStateChangedAtInStepWithRevisions()
    {
        await using var connection = await _database.OpenAsync();

        var mismatches = await connection.QueryAsync<string>(
            """
            SELECT w.key FROM work_item w
            WHERE w.state_changed_at IS DISTINCT FROM (
                SELECT max(r.changed_at) FROM revision r
                WHERE r.work_item_id = w.id AND EXISTS (SELECT 1 FROM jsonb_array_elements(r.changes) c WHERE c ->> 'field' = 'state'))
            """);

        mismatches.ShouldBeEmpty();
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldGiveJoelTheTemporaryUserId()
    {
        await using var connection = await _database.OpenAsync();

        var email = await connection.ExecuteScalarAsync<string>("SELECT email FROM app_user WHERE id = @id", new { id = SeedIds.Users.Joel });

        email.ShouldBe("joel@shiplean.local");
        SeedIds.Users.Joel.ShouldBe(SeedIds.Users.Joel);
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldKeepJoelOutOfPart()
    {
        await using var connection = await _database.OpenAsync();

        var projects = (await connection.QueryAsync<string>(
            "SELECT project.key FROM project_member JOIN project ON project.id = project_id WHERE user_id = @id ORDER BY 1", new { id = SeedIds.Users.Joel })).ToArray();

        projects.ShouldBe(["DATA", "EMPT", "MOB", "OPS", "PAY", "SHIP"]);
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldCreateEverySeedIdsRow()
    {
        await using var connection = await _database.OpenAsync();

        foreach (var (table, type) in new (string, Type)[]
                 {
                     ("app_user", typeof(SeedIds.Users)), ("project", typeof(SeedIds.Projects)), ("sprint", typeof(SeedIds.Sprints)),
                     ("work_item", typeof(SeedIds.WorkItems)), ("import_job", typeof(SeedIds.ImportJobs)), ("board", typeof(SeedIds.Boards)),
                     ("sprint_capacity", typeof(SeedIds.SprintCapacities)), ("project_member", typeof(SeedIds.ProjectMembers)),
                     ("jira_connection", typeof(SeedIds.JiraConnections)),
                 })
        {
            var ids = type.GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (Guid)f.GetValue(null)!).Order().ToArray();
            var stored = (await connection.QueryAsync<Guid>($"SELECT id FROM {table}")).Order().ToArray();
            stored.ShouldBe(ids, table);
        }
        var stateIds = typeof(SeedIds.States).GetNestedTypes().SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static)).Select(f => (Guid)f.GetValue(null)!).Order().ToArray();
        (await connection.QueryAsync<Guid>("SELECT id FROM state")).Order().ToArray().ShouldBe(stateIds);
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldDrawTheSprintBacklogAsInTheDesign()
    {
        await using var connection = await _database.OpenAsync();

        var committed = await connection.QueryAsync<(string Name, decimal Points)>(
            """
            SELECT sprint.name, sum(w.story_points)
            FROM work_item w JOIN sprint ON sprint.id = w.sprint_id
            WHERE w.type IN ('story', 'bug') AND sprint.name = 'Sprint 24' GROUP BY sprint.name
            """);

        committed.Single().Points.ShouldBe(21m);
        (await connection.ExecuteScalarAsync<int>("SELECT next_item_number FROM project WHERE id = @id", new { id = SeedIds.Projects.Ship })).ShouldBe(184);
    }

    private sealed record SprintRow(string Name, string StartDate, string EndDate, string? RolloverStatus);

    private sealed record UserRow(string Name, string Initials, string AvatarColour, bool NoPhoto);

    private sealed record StateRow(string Name, string Category, string Type, int SortOrder);

    private sealed record ItemRow(Guid Id, Guid StateId, Guid? SprintId, double? Points, Guid? Assignee, Guid? ParentId, int Revision);

    private sealed record RevisionRow(Guid WorkItemId, int Rev, string Changes);

    private sealed record ImportJobRow(string Progress, string JiraProjectKey, string ProjectKey, string Status);

    private sealed record PasswordRow(string Name, string Hash, bool MustChange, bool IsAdmin);

    private sealed record BoardRow(Guid ProjectId, string Level, string Settings);
}
