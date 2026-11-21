using Dapper;
using Npgsql;
using ShipLean.Contracts.Prompts;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Prompts;

namespace ShipLean.WebApi.Tests.Prompts;

// C7: the checks and keys the database holds, so no code path can break them. Rows go in by the builders; the refusals are Postgres's.
[TestFixture]
public class PromptsSchemaTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task PromptVersion_WhenOver64KB_ShouldBeRefusedByTheCheck()
    {
        var builder = new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Work).WithText(new string('x', 65_537));

        var thrown = await Should.ThrowAsync<PostgresException>(() => builder.InsertAsync(_database.ConnectionString));

        thrown.ConstraintName.ShouldNotBeNull().ShouldContain("text");
    }

    [Test]
    public async Task ProjectPrompt_WhenInUseVersionIsAnotherProjects_ShouldBeRefused()
    {
        var versionId = await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Fix).InsertAsync(_database.ConnectionString);
        await using var connection = await _database.OpenAsync();

        var thrown = await Should.ThrowAsync<PostgresException>(() => connection.ExecuteAsync(
            "INSERT INTO project_prompt (project_id, prompt, in_use_version_id, updated_at) VALUES (@project, 'fix', @versionId, now())",
            new { project = SeedIds.Projects.Pay, versionId }));

        thrown.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Test]
    public async Task PromptServing_WhenVersionIsAnotherProjects_ShouldBeRefused()
    {
        var versionId = await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Retro).InsertAsync(_database.ConnectionString);
        var builder = new PromptServingBuilder(SeedIds.Projects.Pay, PromptKey.Retro).WithVersion(versionId, 1);

        var thrown = await Should.ThrowAsync<PostgresException>(() => builder.InsertAsync(_database.ConnectionString));

        thrown.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Test]
    public async Task PromptServing_WhenAgentUnknown_ShouldBeRefused()
    {
        var builder = new PromptServingBuilder(SeedIds.Projects.Ship, PromptKey.Brief).WithAgent("emacs");

        var thrown = await Should.ThrowAsync<PostgresException>(() => builder.InsertAsync(_database.ConnectionString));

        thrown.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Test]
    public async Task PromptVersionBuilder_WhenUsed_ShouldMakeTheVersionInUse()
    {
        var versionId = await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Report).WithText("Mine.").InUse().InsertAsync(_database.ConnectionString);
        await using var connection = await _database.OpenAsync();

        var inUse = await connection.QuerySingleAsync<Guid?>(
            "SELECT in_use_version_id FROM project_prompt WHERE project_id = @project AND prompt = 'report'", new { project = SeedIds.Projects.Ship });

        inUse.ShouldBe(versionId);
    }
}
