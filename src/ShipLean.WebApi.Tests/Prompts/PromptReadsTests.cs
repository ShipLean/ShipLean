using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Prompts;
using ShipLean.WebApi.Features.Prompts;

namespace ShipLean.WebApi.Tests.Prompts;

// The real IPromptReads over HTTP, arranged with PromptVersionBuilder only. Joel administers SHIP; Priya is a member there; PAY is another project.
[TestFixture]
public class PromptReadsTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private static readonly PromptKey[] StageOrder =
        [PromptKey.IssueProposal, PromptKey.Work, PromptKey.Guide, PromptKey.PlanReviewer, PromptKey.Lead, PromptKey.Brief, PromptKey.Report,
         PromptKey.CodeReviewer, PromptKey.Fix, PromptKey.Retro, PromptKey.SprintDemo];

    private readonly BuiltInPrompts _builtIns = new();

    private static string Route(Guid projectId, string tail = "") => $"api/projects/{projectId}/prompts{tail}";

    // The label every seeded timestamp gets: all are seeded at the moment of the test, and the viewer's zone is UTC.
    private static DateTimeOffset Ago(int days) => TimeProvider.System.GetUtcNow().ToOffset(TimeSpan.Zero).AddDays(-days);

    // The label a viewer in UTC sees: the day and month, with the year when it is not this year's.
    private static string Day(int days)
    {
        var at = Ago(days);
        return at.ToString(at.Year == TimeProvider.System.GetUtcNow().Year ? "d MMM" : "d MMM yyyy", CultureInfo.InvariantCulture);
    }

    // Puts the built-in with this text in prompt_built_in as first seen that many days ago (the startup snapshot's own row is moved).
    private async Task FirstSeenAsync(PromptKey key, string text, int days)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO prompt_built_in (prompt, hash, text, first_seen_at) VALUES (@wire, @hash, @text, @at)
            ON CONFLICT (prompt, hash) DO UPDATE SET first_seen_at = excluded.first_seen_at
            """,
            new { wire = PromptKeys.ToWire(key), hash = PromptHash.Of(text), text, at = Ago(days).UtcDateTime });
    }

    private string Current(PromptKey key) => _builtIns.Text(key).Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<PromptsPageDto> PageAsync(Guid projectId, TestUser? actingAs = null)
    {
        using var response = await (actingAs is null ? Api : Api.ActingAs(actingAs)).GetAsync(Route(projectId));
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(text);
        return JsonSerializer.Deserialize<PromptsPageDto>(text, Wire)!;
    }

    private static PromptDto Find(PromptsPageDto page, PromptKey key) => page.Stages.SelectMany(s => s.Prompts).Single(p => p.Key == key);

    private async Task<T> GetAsync<T>(string route)
    {
        using var response = await Api.GetAsync(route);
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }

    private Task<Guid> SeedAsync(PromptVersionBuilder builder) => builder.InsertAsync(ConnectionString);

    [Test]
    public async Task Page_WhenNoCustom_ShouldListElevenPromptsInStageOrder()
    {
        var page = await PageAsync(SeedIds.Projects.Empt);

        var prompts = page.Stages.SelectMany(s => s.Prompts).ToList();
        prompts.Select(p => p.Key).ShouldBe(StageOrder);
        page.Stages.Select(s => s.Name).ShouldBe(_builtIns.Stages.Select(s => s.Name));
        prompts.ShouldAllBe(p => p.Status == PromptStatus.BuiltIn && p.Custom == null && p.Update == null && p.YourChangesPatch == null);
        prompts.ShouldAllBe(p => p.WhenRuns != "" && p.ServedBy != "" && p.ReadBy != "");
        Find(page, PromptKey.Brief).Placeholders.ShouldBe(PlanPlaceholders.Brief);
        page.CustomCount.ShouldBe(0);
    }

    [Test]
    public async Task Page_WhenMember_ShouldNotBeEditable()
    {
        var page = await PageAsync(SeedIds.Projects.Ship, TestUser.Priya);

        page.CanEdit.ShouldBeFalse();
    }

    [Test]
    public async Task Page_WhenAdmin_ShouldBeEditableAndNameTheAdmins()
    {
        await using var connection = await OpenDatabaseAsync();
        var joel = await connection.QuerySingleAsync<string>("SELECT name FROM app_user WHERE id = @id", new { id = SeedIds.Users.Joel });

        var page = await PageAsync(SeedIds.Projects.Ship);

        page.CanEdit.ShouldBeTrue();
        page.AdminNames.ShouldContain(joel);
    }

    [Test]
    public async Task Page_WhenCustomV2InUse_ShouldShowV2WithAuthorNoteAndCount()
    {
        await FirstSeenAsync(PromptKey.Guide, Current(PromptKey.Guide), 9);
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Pay, PromptKey.Guide).WithText("first").WithNote("one").WithCreatedBy(SeedIds.Users.Joel).WithCreatedAt(Ago(5)));
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Pay, PromptKey.Guide).WithText("second").WithNote("two").WithCreatedBy(SeedIds.Users.Joel).WithCreatedAt(Ago(3)).InUse());
        await using var connection = await OpenDatabaseAsync();
        var joel = await connection.QuerySingleAsync<string>("SELECT name FROM app_user WHERE id = @id", new { id = SeedIds.Users.Joel });

        var page = await PageAsync(SeedIds.Projects.Pay);

        var guide = Find(page, PromptKey.Guide);
        guide.Status.ShouldBe(PromptStatus.Custom);
        guide.Custom.ShouldNotBeNull();
        guide.Custom.Number.ShouldBe(2);
        guide.Custom.Text.ShouldBe("second");
        guide.Custom.InUse.ShouldBeTrue();
        guide.Custom.By.ShouldBe(joel);
        guide.Custom.Note.ShouldBe("two");
        guide.Custom.AtLabel.ShouldBe(Day(3));
        guide.Custom.BaseLabel.ShouldBe($"built-in of {Day(9)}");
        guide.Custom.BaseText.ShouldBe(_builtIns.Text(PromptKey.Guide).Replace("\r\n", "\n", StringComparison.Ordinal));
        guide.YourChangesPatch.ShouldNotBeNull();
        page.CustomCount.ShouldBe(1);
    }

    [Test]
    public async Task Page_WhenCreatedByIsNull_ShouldNameShipLeanAsTheAuthor()
    {
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Data, PromptKey.Fix).WithSource(PromptVersionSource.Migrated).WithCreatedBy(null).InUse());

        var page = await PageAsync(SeedIds.Projects.Data);

        Find(page, PromptKey.Fix).Custom!.By.ShouldBe("ShipLean");
    }

    [Test]
    public async Task Page_WhenAnotherProjectHasCustoms_ShouldShowOnlyBuiltIns()
    {
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Ops, PromptKey.Work).WithText("ops only").InUse());

        var page = await PageAsync(SeedIds.Projects.Empt);

        page.Stages.SelectMany(s => s.Prompts).ShouldAllBe(p => p.Status == PromptStatus.BuiltIn && p.Custom == null);
        page.CustomCount.ShouldBe(0);
    }

    [Test]
    public async Task Page_WhenBuiltInChangedAfterTheCustom_ShouldFlagBuiltInUpdated()
    {
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Data, PromptKey.Retro).WithBuiltInText("An older retro built-in.").WithText("Mine.").WithCreatedAt(Ago(10)).InUse());
        await FirstSeenAsync(PromptKey.Retro, "An older retro built-in.", 20);
        await FirstSeenAsync(PromptKey.Retro, Current(PromptKey.Retro), 4);

        var page = await PageAsync(SeedIds.Projects.Data);

        var retro = Find(page, PromptKey.Retro);
        retro.Status.ShouldBe(PromptStatus.BuiltInUpdated);
        retro.Update.ShouldNotBeNull();
        retro.Update.BaseLabel.ShouldBe($"built-in of {Day(20)}");
        retro.Update.ChangedLabel.ShouldBe($"{Day(4)} built-in");
        retro.Custom!.AtLabel.ShouldBe(Day(10));
    }

    [Test]
    public async Task Page_WhenKeptMine_ShouldHideTheFlag()
    {
        var current = new BuiltInPrompts().Hash(PromptKey.Report);
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Data, PromptKey.Report).WithBuiltInText("An older report built-in.").WithText("Mine.")
            .WithKeptBuiltInHash(current).InUse());

        var page = await PageAsync(SeedIds.Projects.Data);

        var report = Find(page, PromptKey.Report);
        report.Status.ShouldBe(PromptStatus.Custom);
        report.Update.ShouldBeNull();
    }

    [Test]
    public async Task Page_WhenVersionsButNoneInUse_ShouldBeUsingBuiltIn()
    {
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.SprintDemo).WithText("old").WithNumber(1));
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.SprintDemo).WithText("newer").WithNumber(2));

        var page = await PageAsync(SeedIds.Projects.Ship);

        var demo = Find(page, PromptKey.SprintDemo);
        demo.Status.ShouldBe(PromptStatus.UsingBuiltIn);
        demo.Custom.ShouldNotBeNull();
        demo.Custom.Number.ShouldBe(2);
        demo.Custom.InUse.ShouldBeFalse();
        demo.Update.ShouldBeNull();
    }

    [Test]
    public async Task History_ShouldListEveryVersionNewestFirst()
    {
        await FirstSeenAsync(PromptKey.Lead, Current(PromptKey.Lead), 8);
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Mob, PromptKey.Lead).WithText("v1 text").WithNote("n1").WithNumber(1).WithCreatedAt(Ago(5)));
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Mob, PromptKey.Lead).WithText("v2 text").WithNote("n2").WithNumber(2)
            .WithSource(PromptVersionSource.BuiltInUpdate).WithCreatedAt(Ago(2)).InUse());

        var history = await GetAsync<PromptHistoryDto>(Route(SeedIds.Projects.Mob, "/lead/history"));

        history.Key.ShouldBe(PromptKey.Lead);
        history.Versions.Select(v => v.Number).ShouldBe([2, 1]);
        history.Versions.Select(v => v.InUse).ShouldBe([true, false]);
        history.Versions.Select(v => v.Source).ShouldBe([PromptVersionSource.BuiltInUpdate, PromptVersionSource.Editor]);
        history.Versions.Select(v => v.Text).ShouldBe(["v2 text", "v1 text"]);
        history.Versions.Select(v => v.AtLabel).ShouldBe([Day(2), Day(5)]);
        history.BaseLabel.ShouldBe($"built-in of {Day(8)}");
    }

    [Test]
    public async Task History_WhenNoVersions_ShouldHaveNoBaseLabel()
    {
        var history = await GetAsync<PromptHistoryDto>(Route(SeedIds.Projects.Data, "/brief/history"));

        history.Versions.ShouldBeEmpty();
        history.BaseLabel.ShouldBeNull();
    }

    [Test]
    public async Task Assembled_WhenClaudeCode_ShouldLayerInOrderAndKeepOnlyItsSection()
    {
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Mob, PromptKey.Brief)
            .WithText("Plain line.\n<claude-code>\nClaude only.\n</claude-code>\n<codex-cli>\nOther CLI only.\n</codex-cli>\nLast line.").InUse());

        var assembled = await GetAsync<PromptAssembledDto>(Route(SeedIds.Projects.Mob, "/brief/assembled?agent=claude-code"));

        assembled.Layers.Select(l => l.Kind).ShouldBe(
            [PromptLayerKind.ServerInstructions, PromptLayerKind.Prompt, PromptLayerKind.AgentSections, PromptLayerKind.WaysOfWorking, PromptLayerKind.ClientNotes]);
        assembled.Layers[0].Text.ShouldBe(_builtIns.ServerInstructions);
        assembled.Layers[0].Note.ShouldBe("Sent when the agent connects. Same for every project.");
        assembled.Layers[2].Note.ShouldBe("1 section for Claude Code; the rest are dropped.");
        assembled.Layers[1].Title.ShouldBe("Task brief, custom v1");
        assembled.Layers[1].Text.ShouldBe("Plain line.\nLast line.");
        assembled.Layers[2].Title.ShouldBe("Agent-specific sections");
        assembled.Layers[2].Text.ShouldBe("Claude only.");
        assembled.Text.ShouldBe("Plain line.\nClaude only.\nLast line.");
        assembled.Layers.ShouldAllBe(l => l.Text == null || !l.Text.Contains("Other CLI only.", StringComparison.Ordinal));
        assembled.Text.ShouldNotContain("Other CLI only.");
    }

    [Test]
    public async Task Assembled_WhenAgentHasNoSection_ShouldSayNoSections()
    {
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Data, PromptKey.Work).WithText("Plain.\n<claude-code>\nClaude only.\n</claude-code>").InUse());

        var assembled = await GetAsync<PromptAssembledDto>(Route(SeedIds.Projects.Data, "/work/assembled?agent=cursor"));

        var sections = assembled.Layers.Single(l => l.Kind == PromptLayerKind.AgentSections);
        sections.Text.ShouldBeNull();
        sections.Note.ShouldBe("No sections for Cursor");
        assembled.Text.ShouldBe("Plain.");
    }

    [Test]
    public async Task Assembled_WhenOther_ShouldDropEveryTaggedSection()
    {
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Ops, PromptKey.Guide)
            .WithText("Plain.\n<claude-code>\nClaude only.\n</claude-code>\n<cursor>\nCursor only.\n</cursor>").InUse());

        var assembled = await GetAsync<PromptAssembledDto>(Route(SeedIds.Projects.Ops, "/guide/assembled?agent=other"));

        assembled.Layers.ShouldNotContain(l => l.Kind == PromptLayerKind.AgentSections);
        assembled.Text.ShouldBe("Plain.");
    }

    [Test]
    public async Task Assembled_WhenBuiltIn_ShouldMarkLayersFourAndFivePlanned()
    {
        var assembled = await GetAsync<PromptAssembledDto>(Route(SeedIds.Projects.Mob, "/guide/assembled?agent=claude-code"));

        assembled.Layers[1].Title.ShouldBe("Planning and breakdown, built-in");
        var planned = assembled.Layers.Where(l => l.Planned).ToList();
        planned.Select(l => l.Kind).ShouldBe([PromptLayerKind.WaysOfWorking, PromptLayerKind.ClientNotes]);
        planned.Select(l => l.Note).ShouldBe(["Added after the prompt once the project has them.", "Per-client notes, added last."]);
        assembled.Layers[4].Title.ShouldBe("Notes for the connecting agent");
        assembled.Text.ShouldBe(_builtIns.Text(PromptKey.Guide).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Test]
    public async Task Assembled_WhenTagErrorInUse_ShouldGiveEveryAgentOnlyUntaggedText()
    {
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Mob, PromptKey.PlanReviewer).WithText("Plain.\n<claude-code>\nNever closed.").InUse());

        var claude = await GetAsync<PromptAssembledDto>(Route(SeedIds.Projects.Mob, "/planReviewer/assembled?agent=claude-code"));
        var other = await GetAsync<PromptAssembledDto>(Route(SeedIds.Projects.Mob, "/planReviewer/assembled?agent=other"));

        claude.Text.ShouldBe("Plain.");
        other.Text.ShouldBe("Plain.");
    }

    [Test]
    public async Task Page_WhenInUseTextHasATagError_ShouldCountSectionErrors()
    {
        await SeedAsync(new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.CodeReviewer).WithText("Plain.\n<claude-code>\nNever closed.").InUse());

        var page = await PageAsync(SeedIds.Projects.Ship);

        Find(page, PromptKey.CodeReviewer).SectionErrors.ShouldBe(1);
        Find(page, PromptKey.Work).SectionErrors.ShouldBe(0);
    }
}
