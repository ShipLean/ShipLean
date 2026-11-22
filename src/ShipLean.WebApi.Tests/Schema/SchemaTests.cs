using Dapper;
using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.LocalEnvironment.Database;

namespace ShipLean.WebApi.Tests.Schema;

[TestFixture]
public class SchemaTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Schema_WhenApplied_ShouldHaveEveryIssueOneTable()
    {
        string[] expected =
        [
            "agent_activity", "agent_connection", "agent_delivery", "agent_delivery_attempt", "agent_session", "app_user", "ask_event", "attachment", "backup_run", "backup_settings", "board", "code_comment", "code_commit", "code_repository", "comment", "comment_ask_tag", "comment_mention", "data_protection_key", "decision", "device_session", "device_sign_in_code", "feature_digest_seen", "github_issue", "github_issue_comment", "github_issue_effect", "github_outbox", "holiday_country", "import_error", "import_job", "import_log",
            "import_mapping", "inspection_project_settings", "inspection_run_links", "instance_settings", "issue_job", "jira_connection", "link_state", "mcp_capture_plan_apply", "oauth_authorization", "oauth_client", "oauth_code",
            "oauth_request", "oauth_token", "project", "project_demo_settings", "project_issue_intake", "project_member", "project_prompt", "project_youtube_channel", "prompt_built_in", "prompt_serving", "prompt_version", "public_holiday", "question", "retro_action", "retro_action_point", "retro_analysis", "retro_analysis_point", "retro_failure_log", "retro_pattern", "retro_pattern_fix", "retro_point", "retro_point_support", "retro_theme", "retro_theme_merge_proposal", "retrospective", "retrospective_source", "review_event", "revision", "search_doc", "sprint",
            "sprint_capacity", "state", "tool_payload", "transfer_token", "user_preference", "webhook_delivery", "work_item", "work_item_commit", "work_item_link",
        ];
        await using var connection = await _database.OpenAsync();

        var tables = await connection.QueryAsync<string>(
            "SELECT table_name::text FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name <> 'schemaversions' ORDER BY 1");

        tables.ToArray().ShouldBe(expected);
    }

    [TestCase("epic", "epic", false)]
    [TestCase("epic", "feature", false)]
    [TestCase("feature", "epic", true)]
    [TestCase("feature", "feature", true)]
    [TestCase("feature", "story", false)]
    [TestCase("story", "feature", true)]
    [TestCase("story", "epic", false)]
    [TestCase("story", "story", false)]
    [TestCase("story", "task", false)]
    [TestCase("bug", "feature", true)]
    [TestCase("bug", "story", false)]
    [TestCase("task", "story", true)]
    [TestCase("task", "bug", true)]
    [TestCase("task", "feature", false)]
    [TestCase("task", "task", false)]
    public async Task WorkItem_WhenParentTypeNotAllowed_ShouldBeRejected(string type, string parentType, bool allowed)
    {
        await using var connection = await _database.OpenAsync();
        var parent = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType(parentType).InsertAsync(connection);
        var child = new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).WithParent(parent);

        if (allowed)
        {
            await child.InsertAsync(connection);
        }
        else
        {
            var ex = await Should.ThrowAsync<PostgresException>(async () => await child.InsertAsync(connection));
            ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        }
    }

    [Test]
    public async Task WorkItem_WhenParentTypeDoesNotMatchTheParent_ShouldBeRejectedByTheForeignKey()
    {
        await using var connection = await _database.OpenAsync();
        var parent = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").InsertAsync(connection);
        var child = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithParent(parent).InsertAsync(connection);

        var ex = await Should.ThrowAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("UPDATE work_item SET parent_type = 'epic' WHERE id = @child", new { child }));

        ex.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Test]
    public async Task WorkItem_WhenParentTypeSetWithoutParent_ShouldBeRejectedByTheCheck()
    {
        await using var connection = await _database.OpenAsync();
        var parent = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").InsertAsync(connection);
        var child = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithParent(parent).InsertAsync(connection);

        var ex = await Should.ThrowAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("UPDATE work_item SET parent_id = NULL WHERE id = @child", new { child }));

        ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Test]
    public async Task WorkItem_WhenParentIsOptional_ShouldBeAccepted()
    {
        await using var connection = await _database.OpenAsync();

        var ids = new List<Guid>();
        foreach (var type in new[] { "epic", "feature", "story", "bug", "task" })
        {
            ids.Add(await new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).InsertAsync(connection));
        }

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE id = ANY(@ids) AND parent_id IS NULL", new { ids })).ShouldBe(5);
    }

    [Test]
    public async Task WorkItem_WhenStateBelongsToAnotherType_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();

        var ex = await Should.ThrowAsync<PostgresException>(async () =>
            await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithState(SeedIds.States.Ship.TaskNew).InsertAsync(connection));

        ex.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Test]
    public async Task ProjectKey_WhenNotTwoToFourUppercaseLetters_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();

        foreach (var key in new[] { "A", "ABCDE", "ab", "AB1" })
        {
            var ex = await Should.ThrowAsync<PostgresException>(async () => await connection.ExecuteAsync(
                "INSERT INTO project (id, key, name) VALUES (gen_random_uuid(), @key, 'x')", new { key }));
            ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        }
    }

    // Mutations: make the column NOT NULL; drop the CHECK; widen the pattern so "Not a zone" passes.
    [Test]
    public async Task AppUserTimeZone_WhenShapedWrong_ShouldBeRejectedAndTheColumnStayNullable()
    {
        await using var connection = await _database.OpenAsync();
        var id = await new AppUserBuilder().WithEmail("zone-shape@example.test").InsertAsync(connection);

        (await connection.ExecuteScalarAsync<string>(
            "SELECT is_nullable FROM information_schema.columns WHERE table_name = 'app_user' AND column_name = 'time_zone'")).ShouldBe("YES");
        (await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM pg_constraint WHERE conname = 'app_user_time_zone_check'")).ShouldBe(1);
        foreach (var zone in new[] { "Not a zone", "Asia/Bangkok/", new string('a', 65) })
        {
            var ex = await Should.ThrowAsync<PostgresException>(async () => await connection.ExecuteAsync(
                "UPDATE app_user SET time_zone = @zone WHERE id = @id", new { zone, id }));
            ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        }
        await connection.ExecuteAsync("UPDATE app_user SET time_zone = 'Asia/Bangkok' WHERE id = @id", new { id });
    }

    // SHIP-1398 K1. Mutations: make the column NOT NULL; type it as timestamp without zone.
    [Test]
    public async Task WorkItemLinkCommittedAt_WhenApplied_ShouldBeANullableTimestamptz()
    {
        await using var connection = await _database.OpenAsync();

        var column = await connection.QuerySingleAsync<(string DataType, string IsNullable)>(
            "SELECT data_type, is_nullable FROM information_schema.columns WHERE table_name = 'work_item_link' AND column_name = 'committed_at'");

        column.DataType.ShouldBe("timestamp with time zone");
        column.IsNullable.ShouldBe("YES");
    }

    // SHIP-1398 QK1. Mutations: default found_before_release to true; leave causedBy out of the CHECK; drop blockedBy from the
    // re-added CHECK; change a default weight.
    [Test]
    public async Task InspectionQuality_WhenApplied_ShouldAddBugFieldLinkTypeAndWeights()
    {
        await using var connection = await _database.OpenAsync();
        var bug = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bug").InsertAsync(connection);
        var task = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);

        (await connection.ExecuteScalarAsync<bool>("SELECT found_before_release FROM work_item WHERE id = @bug", new { bug })).ShouldBeFalse();
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, target_id, link_type) VALUES (gen_random_uuid(), @bug, @task, 'causedBy')", new { bug, task });
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, target_id, link_type) VALUES (gen_random_uuid(), @bug, @task, 'blockedBy')", new { bug, task });
        var ex = await Should.ThrowAsync<PostgresException>(async () => await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, target_id, link_type) VALUES (gen_random_uuid(), @bug, @task, 'causes')", new { bug, task }));
        ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        (await connection.ExecuteScalarAsync<string>(
            "SELECT bug_severity_weights::text FROM project WHERE id = @id", new { id = SeedIds.Projects.Ship })).ShouldBe("""{"1": 8, "2": 4, "3": 2, "4": 1}""");
    }
}
