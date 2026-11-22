using Dapper;
using Npgsql;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Retros.Analysis;

// C5: the tables an aggregate retro analysis is stored in, and the fix types retro_action accepts. The refusals are Postgres's.
[TestFixture]
public class RetrosAnalysisSchemaTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Migration_WhenApplied_ShouldCreateTheFourTables()
    {
        await using var connection = await _database.OpenAsync();

        var columns = (await connection.QueryAsync<(string Table, string Column)>(
            """
            SELECT table_name, column_name FROM information_schema.columns
            WHERE table_name IN ('retro_analysis', 'retro_analysis_point', 'retro_pattern', 'retro_pattern_fix')
            """)).ToLookup(row => row.Table, row => row.Column);

        columns["retro_analysis"].ShouldBe(
            ["id", "project_id", "scope_kind", "feature_id", "sprint_id", "range_from", "range_to", "created_by_user_id", "agent_name", "embedding_model", "rewrite_model", "report_markdown", "points_in", "actions_new", "actions_added", "created_at", "archived_at"],
            ignoreOrder: true);
        columns["retro_analysis_point"].ShouldBe(["analysis_id", "point_id", "outcome", "folded_into_point_id", "generic_text", "pattern_id"], ignoreOrder: true);
        columns["retro_pattern"].ShouldBe(["id", "analysis_id", "ref", "kind", "title", "lesson", "asks", "centroid", "continues_pattern_id", "rank"], ignoreOrder: true);
        columns["retro_pattern_fix"].ShouldBe(["id", "pattern_id", "title", "fix_type", "enforcement", "enforced_by", "reason", "action_work_item_id", "rank"], ignoreOrder: true);

        var checks = (await connection.QueryAsync<string>(
            """
            SELECT pg_get_constraintdef(oid) FROM pg_constraint
            WHERE contype = 'c' AND conrelid IN ('retro_analysis'::regclass, 'retro_analysis_point'::regclass, 'retro_pattern'::regclass, 'retro_pattern_fix'::regclass)
            """)).ToList();
        string.Join("\n", checks).ShouldContain("'noise'");
        foreach (var value in new[] { "'feature'", "'range'", "'oneOff'", "'unclustered'", "'wentWell'", "'notCaught'", "'discarded'", "'structure'" })
        {
            checks.ShouldContain(check => check.Contains(value, StringComparison.Ordinal), value);
        }
    }

    [Test]
    public async Task Migration_WhenApplied_ShouldIndexTheReadPaths()
    {
        await using var connection = await _database.OpenAsync();

        var indexes = (await connection.QueryAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE tablename IN ('retro_analysis', 'retro_analysis_point', 'retro_pattern', 'retro_pattern_fix')")).ToList();

        indexes.ShouldContain(index => index.Contains("(project_id, created_at DESC)", StringComparison.Ordinal));
        indexes.ShouldContain(index => index.Contains("retro_analysis_point USING btree (point_id)", StringComparison.Ordinal));
        indexes.ShouldContain(index => index.Contains("retro_pattern USING btree (analysis_id)", StringComparison.Ordinal));
        indexes.ShouldContain(index => index.Contains("retro_pattern_fix USING btree (pattern_id)", StringComparison.Ordinal));
    }

    [TestCase("fix")]
    [TestCase("structure")]
    public async Task FixType_WhenFixOrStructure_ShouldBeAccepted(string fixType)
    {
        await using var connection = await _database.OpenAsync();
        var item = await connection.QueryFirstAsync<Guid>(
            "SELECT id FROM work_item WHERE project_id = @project AND id NOT IN (SELECT work_item_id FROM retro_action) LIMIT 1", new { project = SeedIds.Projects.Ship });

        await connection.ExecuteAsync(
            "INSERT INTO retro_action (work_item_id, project_id, fix_type) VALUES (@item, @project, @fixType)", new { item, project = SeedIds.Projects.Ship, fixType });

        (await connection.QuerySingleAsync<string>("SELECT fix_type FROM retro_action WHERE work_item_id = @item", new { item })).ShouldBe(fixType);
    }

    [Test]
    public async Task FixType_WhenUnknown_ShouldBeRefusedByTheCheck()
    {
        await using var connection = await _database.OpenAsync();
        var item = await connection.QueryFirstAsync<Guid>(
            "SELECT id FROM work_item WHERE project_id = @project AND id NOT IN (SELECT work_item_id FROM retro_action) LIMIT 1", new { project = SeedIds.Projects.Ship });

        var thrown = await Should.ThrowAsync<PostgresException>(() => connection.ExecuteAsync(
            "INSERT INTO retro_action (work_item_id, project_id, fix_type) VALUES (@item, @project, 'magic')", new { item, project = SeedIds.Projects.Ship }));

        thrown.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }
}
