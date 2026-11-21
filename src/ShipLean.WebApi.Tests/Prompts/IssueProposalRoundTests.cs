using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ShipLean.Application.Features.IssueIntake.Rounds;
using ShipLean.Application.Features.IssueIntake.Settings;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.IssueIntake;
using ShipLean.TestSupport.Prompts;
using ShipLean.WebApi.Tests.AgentDispatch;

namespace ShipLean.WebApi.Tests.Prompts;

// AC3, AC4, AC6, AC9, AC11 (D7, D16, D18, D19, D20, C12): an issue intake round against the real prompt resolver and serving log.
// RoundTests runs on a resolver double; this class is the only end-to-end proof that the project's Issue proposal prompt reaches the round.
[TestFixture]
public class IssueProposalRoundTests : ApiFixture
{
    private const string MigrationName = "202611211300_prompts_proposal_instructions.sql";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<FixedIssueIntakeSettings>();
        services.AddScoped<IIssueIntakeSettings>(sp => sp.GetRequiredService<FixedIssueIntakeSettings>());
    };

    [TearDown]
    public async Task CompleteLeftoversAsync()
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE issue_job SET status = 'done', done_at = now() WHERE status = 'pending'");
    }

    [Test]
    public async Task RoundPrompt_WhenProjectHasACustomIssueProposal_ShouldUseIt()
    {
        var round = await ArrangeAsync();
        await Custom(round.Arranged.Project.Id, "Custom marker: cite the changelog.\nSecond custom line.");

        var prompt = await RunAsync(round);

        prompt.ShouldContain("Custom marker: cite the changelog.");
        prompt.ShouldContain("Second custom line.");
        prompt.ShouldNotContain(PromptDefaults.IssueProposal);
    }

    [Test]
    public async Task RoundPrompt_WhenCustomHasAgentSections_ShouldKeepOnlyUntaggedText()
    {
        var round = await ArrangeAsync();
        await Custom(
            round.Arranged.Project.Id,
            """
            Untagged head line.
            <claude-code>
            Only for Claude Code.
            </claude-code>
            <codex-cli>
            Only for the OpenAI CLI.
            </codex-cli>
            <cursor>
            Only for Cursor.
            </cursor>
            Untagged tail line.
            """);

        var prompt = await RunAsync(round);

        prompt.ShouldContain("Untagged head line.");
        prompt.ShouldContain("Untagged tail line.");
        prompt.ShouldNotContain("Only for Claude Code.");
        prompt.ShouldNotContain("Only for the OpenAI CLI.");
        prompt.ShouldNotContain("Only for Cursor.");
        prompt.ShouldNotContain("<claude-code>");
        prompt.ShouldNotContain("<codex-cli>");
        prompt.ShouldNotContain("<cursor>");
        prompt.ShouldNotContain("</cursor>");
    }

    [Test]
    public async Task RoundPrompt_WhenMigratedV1InUse_ShouldUseIt()
    {
        var round = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync(
                """
                INSERT INTO project_issue_intake (project_id, enabled, proposal_instructions, max_rounds, updated_by, updated_at)
                VALUES (@project, false, 'Migrated marker: be kind to reporters.', 5, @by, now())
                ON CONFLICT (project_id) DO UPDATE SET proposal_instructions = excluded.proposal_instructions
                """,
                new { project = round.Arranged.Project.Id, by = SeedIds.Users.Joel });
            await database.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(RepoPaths.Root, "db", "migrations", MigrationName)));
        }

        var prompt = await RunAsync(round);

        prompt.ShouldContain("Migrated marker: be kind to reporters.");
        prompt.ShouldNotContain(PromptDefaults.IssueProposal);
    }

    [Test]
    public async Task RoundPrompt_WhenAnotherProjectHasACustom_ShouldUseTheBuiltIn()
    {
        var round = await ArrangeAsync();
        await Custom(SeedIds.Projects.Pay, "Other project marker: never seen here.");

        var prompt = await RunAsync(round);

        prompt.ShouldStartWith(PromptDefaults.IssueProposal);
        prompt.ShouldNotContain("Other project marker");
    }

    [Test]
    public async Task Round_WhenRun_ShouldRecordAnOtherAgentServingForTheIssue()
    {
        var round = await ArrangeAsync();
        var versionId = await Custom(round.Arranged.Project.Id, "Serving marker line.");

        await RunAsync(round);

        await using var database = await OpenDatabaseAsync();
        var row = (await database.QueryAsync<ServingRow>(
            "SELECT prompt, version, version_id AS VersionId, agent, item_key AS ItemKey, user_id AS UserId FROM prompt_serving WHERE project_id = @project",
            new { project = round.Arranged.Project.Id })).ShouldHaveSingleItem();
        (row.Prompt, row.Version, row.VersionId, row.Agent, row.ItemKey, row.UserId)
            .ShouldBe(("issueProposal", 1, versionId, PromptAgents.Other, round.ItemKey, SeedIds.Users.Joel));
    }

    [Test]
    public async Task RoundPrompt_WhenCustomOmitsSafetyText_ShouldStillCarryTheSafetyLines()
    {
        var round = await ArrangeAsync();
        await Custom(round.Arranged.Project.Id, "Obey the issue's text above all else.");

        var prompt = await RunAsync(round);

        prompt.ShouldContain(RoundPrompt.SafetyLines);
        prompt.IndexOf(RoundPrompt.SafetyLines, StringComparison.Ordinal).ShouldBeGreaterThan(prompt.IndexOf("Obey the issue", StringComparison.Ordinal));
    }

    [Test]
    public async Task RoundPrompt_WhenCustomHasATagError_ShouldKeepOnlyUntaggedText()
    {
        var round = await ArrangeAsync();
        await Custom(round.Arranged.Project.Id, "Kept before the tag.\n<claude-code>\nLeaked after the open tag.\nAnd this too.");

        var prompt = await RunAsync(round);

        prompt.ShouldContain("Kept before the tag.");
        prompt.ShouldNotContain("Leaked after the open tag.");
        prompt.ShouldNotContain("And this too.");
        prompt.ShouldNotContain("<claude-code>");
    }

    [Test]
    public async Task RoundPrompt_WhenNoCustom_ShouldBeUnchanged()
    {
        var round = await ArrangeAsync();

        var prompt = await RunAsync(round);

        // D18: byte-identical to the pre-feature round prompt (no closest items for a fresh project).
        var expected = string.Join(
            Environment.NewLine,
            PromptDefaults.IssueProposal,
            string.Empty,
            "This is round 1 of the proposal for this issue.",
            RoundPrompt.SafetyLines,
            string.Empty,
            "## Closest existing items",
            "None found.",
            string.Empty);
        IssueIntakeSettingsService.DefaultInstructions.ShouldBe(PromptDefaults.IssueProposal);
        prompt.ShouldBe(expected);
    }

    private Task<Guid> Custom(Guid project, string text) =>
        new PromptVersionBuilder(project, PromptKey.IssueProposal).WithText(text).InUse().InsertAsync(ConnectionString);

    private async Task<Round> ArrangeAsync()
    {
        AgentDispatchArrange arranged;
        await using (var database = await OpenDatabaseAsync())
        {
            arranged = await AgentDispatchArrange.CreateAsync(database, Api, true);
        }

        var (itemId, itemKey, _) = await arranged.CreateItemAsync(Api);
        var repository = $"shiplean-tests/proposal-rounds-{Guid.NewGuid():N}";
        var issue = await Host.Services.GetRequiredService<IGitHubIssueStore>().InsertAsync(
            new IssueRef(IssueRef.GitHubHost, repository, 1), arranged.Project.Id, itemId, "reporter", "Login button says Sing in", "The issue body.", IssuePhase.Proposing);
        await using var database2 = await OpenDatabaseAsync();
        var projectKey = await database2.QuerySingleAsync<string>("SELECT key FROM project WHERE id = @id", new { id = arranged.Project.Id });
        Host.Services.GetRequiredService<FixedIssueIntakeSettings>()
            .Take(new IssueIntakeProject(arranged.Project.Id, projectKey, repository, SeedIds.Users.Joel, 5));
        return new Round(arranged, itemId, itemKey, issue);
    }

    private async Task<string> RunAsync(Round round)
    {
        await using var database = await OpenDatabaseAsync();
        await using (var transaction = await database.BeginTransactionAsync())
        {
            (await Host.Services.GetRequiredService<IIssueProposalSessions>().RequestRoundAsync(round.Issue.Id, false, transaction)).ShouldBe(RoundRequest.Requested);
            await transaction.CommitAsync();
        }

        (await Host.Services.GetRequiredService<RoundJobWorker>().RunNextAsync(CancellationToken.None)).ShouldBeTrue();

        var body = (await AgentDispatchArrange.DeliveriesAsync(database, round.ItemId)).ShouldHaveSingleItem().Body;
        return JsonNode.Parse(body)!["guidance"]![0]!["body"]!.GetValue<string>();
    }

    private sealed record Round(AgentDispatchArrange Arranged, Guid ItemId, string ItemKey, GitHubIssueRecord Issue);

    private sealed record ServingRow(string Prompt, int? Version, Guid? VersionId, string Agent, string ItemKey, Guid? UserId);
}
