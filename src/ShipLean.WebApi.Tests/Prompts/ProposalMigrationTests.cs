using System.Text.RegularExpressions;
using Dapper;
using ShipLean.Contracts.Prompts;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Prompts;

// D7 / P9: a project's saved Proposal instructions become its Issue proposal custom v1. The script is run again on the class's database
// (the fresh database already ran it once, over no saved text), so each test saves its text first and then re-runs it.
[TestFixture]
public partial class ProposalMigrationTests
{
    private const string ScriptName = "202611211300_prompts_proposal_instructions.sql";
    private const string MovedNote = "Moved from Integrations › Proposal instructions";

    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task ProposalMigration_ShouldCarryASelfConsistentBuiltIn()
    {
        var script = await ReadScriptAsync();
        var hash = HashLiteral().Match(script).Groups[1].Value;
        // The text equalled IssueIntakeSettingsService.DefaultInstructions when written; it is not pinned to the live const, because a released migration can't change.
        var text = TextLiteral().Match(script).Groups[1].Value;
        await using var connection = await _database.OpenAsync();

        var stored = await connection.QuerySingleAsync<string>("SELECT text FROM prompt_built_in WHERE prompt = 'issueProposal' AND hash = @hash", new { hash });

        PromptHash.Of(text).ShouldBe(hash);
        stored.ShouldBe(text);
    }

    [Test]
    public async Task ProposalMigration_WhenInstructionsSaved_ShouldBecomeCustomV1()
    {
        var savedAt = TimeProvider.System.GetUtcNow().AddDays(-3);
        await SaveAsync(SeedIds.Projects.Ship, "Write it kindly.\r\nCite the code.\r\n", SeedIds.Users.Joel, savedAt);

        await RunScriptAsync();

        await using var connection = await _database.OpenAsync();
        var version = await connection.QuerySingleAsync<VersionRow>(
            """
            SELECT v.number, v.text, v.note, v.source, v.created_by AS CreatedBy, v.created_at AS CreatedAt, (pp.in_use_version_id = v.id) AS InUse
            FROM prompt_version v JOIN project_prompt pp ON pp.project_id = v.project_id AND pp.prompt = v.prompt
            WHERE v.project_id = @project AND v.prompt = 'issueProposal'
            """,
            new { project = SeedIds.Projects.Ship });
        version.Number.ShouldBe(1);
        version.Text.ShouldBe("Write it kindly.\nCite the code.");
        version.Note.ShouldBe(MovedNote);
        version.Source.ShouldBe("migrated");
        version.CreatedBy.ShouldBe(SeedIds.Users.Joel);
        version.CreatedAt.ShouldBe(savedAt, TimeSpan.FromMilliseconds(1));
        version.InUse.ShouldBeTrue();
    }

    [Test]
    public async Task ProposalMigration_WhenEmpty_ShouldMakeNoVersion()
    {
        await SaveAsync(SeedIds.Projects.Pay, "  \r\n ", SeedIds.Users.Joel, TimeProvider.System.GetUtcNow());

        await RunScriptAsync();

        await using var connection = await _database.OpenAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM prompt_version WHERE project_id = @project AND prompt = 'issueProposal'", new { project = SeedIds.Projects.Pay })).ShouldBe(0);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM project_prompt WHERE project_id = @project AND prompt = 'issueProposal'", new { project = SeedIds.Projects.Pay })).ShouldBe(0);
    }

    [Test]
    public async Task ProposalMigration_WhenRunTwice_ShouldNotDuplicate()
    {
        await SaveAsync(SeedIds.Projects.Mob, "Be brief.", SeedIds.Users.Joel, TimeProvider.System.GetUtcNow());

        await RunScriptAsync();
        await RunScriptAsync();

        await using var connection = await _database.OpenAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM prompt_version WHERE project_id = @project AND prompt = 'issueProposal'", new { project = SeedIds.Projects.Mob })).ShouldBe(1);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM prompt_built_in WHERE prompt = 'issueProposal'")).ShouldBe(1);
    }

    private async Task SaveAsync(Guid project, string text, Guid by, DateTimeOffset at)
    {
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO project_issue_intake (project_id, enabled, proposal_instructions, max_rounds, updated_by, updated_at)
            VALUES (@project, false, @text, 5, @by, @at)
            """,
            new { project, text, by, at });
    }

    private async Task RunScriptAsync()
    {
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync(await ReadScriptAsync());
    }

    private static Task<string> ReadScriptAsync() => File.ReadAllTextAsync(Path.Combine(RepoPaths.Root, "db", "migrations", ScriptName));

    [GeneratedRegex(@"VALUES \('issueProposal', '([0-9a-f]{64})'")]
    private static partial Regex HashLiteral();

    [GeneratedRegex(@"\$\$(.*?)\$\$", RegexOptions.Singleline)]
    private static partial Regex TextLiteral();

    private sealed record VersionRow(int Number, string Text, string Note, string Source, Guid? CreatedBy, DateTimeOffset CreatedAt, bool InUse);
}
