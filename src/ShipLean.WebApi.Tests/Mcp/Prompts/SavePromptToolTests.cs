using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Prompts;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Prompts;

// save_prompt end to end over /mcp: the preview's checks, diff and warnings, the apply that saves exactly what was previewed, and the stale
// refusals. Joel administers SHIP; Priya is a member, not an admin. Tests in this class share one database, so each reads what is in use
// before it saves and uses text of its own.
[TestFixture]
public class SavePromptToolTests : McpFixture
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    protected override bool WithProbeTools => false;

    private RecordingUsageEvents Usage => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [TearDown]
    public void ActAsNobody() => Host.ActAs(null);

    private static string Unique(string label) => $"{label} {Guid.NewGuid():N}\n";

    private static string ErrorCode(McpReply reply) => reply.Structured().GetProperty("error").GetProperty("code").GetString()!;

    private static string? ErrorField(McpReply reply) => reply.Structured().GetProperty("error").GetProperty("field").GetString();

    private static string ErrorMessage(McpReply reply) => reply.Structured().GetProperty("error").GetProperty("message").GetString()!;

    private static string Args(string prompt, string text, string note, object basedOn, string? plan = null)
    {
        var args = new Dictionary<string, object> { ["project"] = "SHIP", ["prompt"] = prompt, ["text"] = text, ["note"] = note, ["based_on"] = basedOn };
        if (plan is not null)
        {
            args["plan"] = plan;
        }

        return JsonSerializer.Serialize(args);
    }

    private async Task<McpReply> CallAsync(string arguments, TestUser? user = null)
        => await CallToolAsync(await TokenAsync(user ?? TestUser.Joel, ReadWritePlan), "save_prompt", arguments);

    private async Task<JsonElement> OkAsync(string arguments)
    {
        var reply = await CallAsync(arguments);
        reply.IsToolError().ShouldBeFalse(reply.Body);
        return reply.Structured();
    }

    // The version in use: a number, or "built-in".
    private async Task<object> BasedOnAsync(string prompt)
    {
        await using var database = await OpenDatabaseAsync();
        var number = await database.QuerySingleOrDefaultAsync<int?>(
            "SELECT v.number FROM project_prompt p JOIN prompt_version v ON v.id = p.in_use_version_id WHERE p.project_id = @project AND p.prompt = @prompt",
            new { project = SeedIds.Projects.Ship, prompt });
        return number is { } n ? n : "built-in";
    }

    private async Task<int> VersionsAsync(string prompt)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM prompt_version WHERE project_id = @project AND prompt = @prompt", new { project = SeedIds.Projects.Ship, prompt });
    }

    private async Task<(int Number, string Text, string Note, string Source, string? Agent)> NewestAsync(string prompt)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<(int, string, string, string, string?)>(
            "SELECT number, text, note, source, agent_name FROM prompt_version WHERE project_id = @project AND prompt = @prompt ORDER BY number DESC LIMIT 1",
            new { project = SeedIds.Projects.Ship, prompt });
    }

    // An admin saving on the web, through the same IPromptWrites.
    private async Task<int> AdminSavesAsync(string text)
    {
        Host.ActAs(SeedIds.Users.Joel);
        using var scope = Host.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<IPromptWrites>()
            .SaveAsync(SeedIds.Projects.Ship, PromptKey.Guide, new PromptSaveDto(text, "On the web.", PromptVersionSource.Editor));
        return saved.InUseVersion!.Value;
    }

    private async Task<(string Text, object BasedOn, string Plan)> PreviewedAsync(string prompt = "guide", string note = "A change.")
    {
        var text = Unique("Previewed");
        var basedOn = await BasedOnAsync(prompt);
        var plan = (await OkAsync(Args(prompt, text, note, basedOn))).GetProperty("plan").GetString()!;
        return (text, basedOn, plan);
    }

    [Test]
    public async Task SavePrompt_WhenPreviewed_ShouldShowPatchAndWarningsAndWriteNothing()
    {
        var before = await VersionsAsync("guide");
        var text = Unique("Previewed with {{nope}}");

        var reply = await CallAsync(Args("guide", text, "A change.", await BasedOnAsync("guide")));

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var preview = reply.Structured();
        preview.GetProperty("applied").GetBoolean().ShouldBeFalse();
        preview.GetProperty("key").GetString().ShouldBe("guide");
        preview.GetProperty("next_version").GetInt32().ShouldBe(before + 1);
        preview.GetProperty("patch").GetString().ShouldNotBeNull().ShouldContain($"+{text.TrimEnd('\n')}");
        preview.GetProperty("bytes").GetInt32().ShouldBe(System.Text.Encoding.UTF8.GetByteCount(text));
        var warning = preview.GetProperty("warnings").EnumerateArray().Single();
        warning.GetProperty("kind").GetString().ShouldBe("placeholderUnknown");
        warning.GetProperty("placeholder").GetString().ShouldBe("{{nope}}");
        preview.GetProperty("plan").GetString().ShouldNotBeNullOrEmpty();
        preview.GetProperty("expires_in_minutes").GetInt32().ShouldBe(15);
        reply.ResultText().ShouldStartWith($"Preview: would save guide v{before + 1}");
        (await VersionsAsync("guide")).ShouldBe(before);
    }

    [Test]
    public async Task SavePrompt_WhenAppliedWithThePlan_ShouldSaveExactlyThePreviewedTextAsMcpWithAgent()
    {
        var text = $"Line one {Guid.NewGuid():N}\r\nLine two\r\n";
        var basedOn = await BasedOnAsync("guide");
        var preview = await OkAsync(Args("guide", text, " Agent change. ", basedOn));

        var applied = await OkAsync(Args("guide", text, " Agent change. ", basedOn, preview.GetProperty("plan").GetString()));

        applied.GetProperty("applied").GetBoolean().ShouldBeTrue();
        var version = applied.GetProperty("version").GetInt32();
        version.ShouldBe(preview.GetProperty("next_version").GetInt32());
        applied.GetProperty("status").GetString().ShouldBe("custom");
        applied.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        var newest = await NewestAsync("guide");
        newest.Number.ShouldBe(version);
        newest.Text.ShouldBe(text.Replace("\r\n", "\n", StringComparison.Ordinal));
        newest.Note.ShouldBe("Agent change.");
        newest.Source.ShouldBe("mcp");
        newest.Agent.ShouldNotBeNull().ShouldStartWith("Test Agent");
        (await BasedOnAsync("guide")).ShouldBe(version);
    }

    [Test]
    public async Task SavePrompt_WhenArgumentsChangedAfterPreview_ShouldRefuse()
    {
        var (text, basedOn, plan) = await PreviewedAsync();
        var before = await VersionsAsync("guide");

        var otherText = await CallAsync(Args("guide", text + "More.", "A change.", basedOn, plan));
        var otherNote = await CallAsync(Args("guide", text, "Another note.", basedOn, plan));
        var otherBase = await CallAsync(Args("guide", text, "A change.", basedOn is int n ? n + 1 : 1, plan));

        foreach (var reply in new[] { otherText, otherNote, otherBase })
        {
            ErrorCode(reply).ShouldBe("validation");
            ErrorField(reply).ShouldBe("plan");
            ErrorMessage(reply).ShouldBe("The arguments changed since the preview. Preview again.");
        }

        (await VersionsAsync("guide")).ShouldBe(before);
    }

    [Test]
    public async Task SavePrompt_WhenNotAdmin_ShouldRefuseAndWriteNothing()
    {
        var (text, basedOn, plan) = await PreviewedAsync();
        var before = await VersionsAsync("guide");

        var preview = await CallAsync(Args("guide", text, "A change.", basedOn), TestUser.Priya);
        var apply = await CallAsync(Args("guide", text, "A change.", basedOn, plan), TestUser.Priya);

        ErrorCode(preview).ShouldBe("forbidden");
        ErrorCode(apply).ShouldBe("forbidden");
        (await VersionsAsync("guide")).ShouldBe(before);
    }

    [Test]
    public async Task SavePrompt_WhenEmpty_ShouldRefuse()
    {
        var reply = await CallAsync(Args("guide", "  \n ", "A change.", await BasedOnAsync("guide")));

        ErrorCode(reply).ShouldBe("validation");
        ErrorField(reply).ShouldBe("text");
        ErrorMessage(reply).ShouldBe("The prompt text can't be empty.");
    }

    [Test]
    public async Task SavePrompt_WhenOver64Kb_ShouldRefuse()
    {
        var reply = await CallAsync(Args("guide", new string('a', 65_537), "A change.", await BasedOnAsync("guide")));

        ErrorCode(reply).ShouldBe("validation");
        ErrorField(reply).ShouldBe("text");
        ErrorMessage(reply).ShouldBe("The prompt text is at most 64 KB.");
    }

    [Test]
    public async Task SavePrompt_WhenSectionUnclosed_ShouldRefuse()
    {
        var reply = await CallAsync(Args("guide", "Intro.\n<claude-code>\nOnly for one.", "A change.", await BasedOnAsync("guide")));

        ErrorCode(reply).ShouldBe("validation");
        ErrorField(reply).ShouldBe("text");
        ErrorMessage(reply).ShouldBe("Line 2: <claude-code> is never closed.");
    }

    [Test]
    public async Task SavePrompt_WhenPlaceholderMissing_ShouldWarnAndStillSave()
    {
        var (text, basedOn, plan) = await PreviewedAsync("brief");

        var applied = await OkAsync(Args("brief", text, "A change.", basedOn, plan));

        applied.GetProperty("applied").GetBoolean().ShouldBeTrue();
        var missing = applied.GetProperty("warnings").EnumerateArray().Where(w => w.GetProperty("kind").GetString() == "placeholderMissing").ToList();
        missing.Select(w => w.GetProperty("placeholder").GetString()).ShouldBe(PlanPlaceholders.Brief);
        missing[0].GetProperty("message").GetString().ShouldBe("{{common_brief}} is missing, so agents won't get that part.");
        (await NewestAsync("brief")).Text.ShouldBe(text);
    }

    [Test]
    public async Task SavePrompt_WhenBasedOnIsStale_ShouldRefuseAtPreview()
    {
        var inUse = await BasedOnAsync("guide");

        var reply = await CallAsync(Args("guide", Unique("Stale"), "A change.", inUse is int n ? n + 50 : 50));

        ErrorCode(reply).ShouldBe("conflict");
        var now = inUse is int number ? $"v{number}" : "the built-in";
        ErrorMessage(reply).ShouldBe($"The version in use changed since you read it: it is now {now}. Read the prompt again with prompts, then save.");
    }

    [Test]
    public async Task SavePrompt_WhenVersionChangedBetweenPreviewAndApply_ShouldRefuseAtApply()
    {
        var (text, basedOn, plan) = await PreviewedAsync();
        var webVersion = await AdminSavesAsync(Unique("Saved on the web"));

        var reply = await CallAsync(Args("guide", text, "A change.", basedOn, plan));

        ErrorCode(reply).ShouldBe("conflict");
        ErrorMessage(reply).ShouldContain($"it is now v{webVersion}.");
        (await NewestAsync("guide")).Number.ShouldBe(webVersion);
    }

    [Test]
    public async Task SavePrompt_WhenOtherWriterSavedTheSameText_ShouldRefuseAsStale()
    {
        var (text, basedOn, plan) = await PreviewedAsync();
        var webVersion = await AdminSavesAsync(text);

        var reply = await CallAsync(Args("guide", text, "A change.", basedOn, plan));

        ErrorCode(reply).ShouldBe("conflict");
        ErrorMessage(reply).ShouldContain($"it is now v{webVersion}.");
    }

    [Test]
    public async Task SavePrompt_WhenTextUnchanged_ShouldRefuse()
    {
        var text = Unique("Already in use");
        var version = await AdminSavesAsync(text);

        var reply = await CallAsync(Args("guide", text, "A change.", version));

        ErrorCode(reply).ShouldBe("validation");
        ErrorField(reply).ShouldBe("text");
        ErrorMessage(reply).ShouldBe("Nothing changed from the version in use.");
    }

    [Test]
    public async Task SavePrompt_WhenNoteEmpty_ShouldSave()
    {
        var (text, basedOn, plan) = await PreviewedAsync(note: "");

        var applied = await OkAsync(Args("guide", text, "", basedOn, plan));

        applied.GetProperty("applied").GetBoolean().ShouldBeTrue();
        var newest = await NewestAsync("guide");
        newest.Number.ShouldBe(applied.GetProperty("version").GetInt32());
        newest.Note.ShouldBe("");
    }

    [Test]
    public async Task SavePrompt_WhenNoteTooLong_ShouldRefuseOnNote()
    {
        var reply = await CallAsync(Args("guide", Unique("Text"), new string('n', 201), await BasedOnAsync("guide")));

        ErrorCode(reply).ShouldBe("validation");
        ErrorField(reply).ShouldBe("note");
        ErrorMessage(reply).ShouldBe("The note is at most 200 characters.");
    }

    [Test]
    public async Task SavePrompt_WhenPlanInvalid_ShouldBeValidationOnPlan()
    {
        var reply = await CallAsync(Args("guide", Unique("Text"), "A change.", await BasedOnAsync("guide"), "not-a-plan"));

        ErrorCode(reply).ShouldBe("validation");
        ErrorField(reply).ShouldBe("plan");
    }

    [Test]
    public async Task SavePrompt_WhenPlanFromAnotherPrompt_ShouldBeValidationOnPlan()
    {
        var (text, basedOn, plan) = await PreviewedAsync();
        var before = await VersionsAsync("work");

        var reply = await CallAsync(Args("work", text, "A change.", basedOn, plan));

        ErrorCode(reply).ShouldBe("validation");
        ErrorField(reply).ShouldBe("plan");
        (await VersionsAsync("work")).ShouldBe(before);
    }

    [Test]
    public async Task SavePrompt_WhenBasedOnInvalid_ShouldBeValidationOnBasedOn()
    {
        var reply = await CallAsync(Args("guide", Unique("Text"), "A change.", "latest"));

        ErrorCode(reply).ShouldBe("validation");
        ErrorField(reply).ShouldBe("based_on");
    }

    [Test]
    public async Task SavePrompt_WhenApplied_ShouldEmitPromptSavedOnMcpSurface()
    {
        var (text, basedOn, plan) = await PreviewedAsync();
        Usage.Clear();

        await OkAsync(Args("guide", text, "A change.", basedOn, plan));

        var saved = await Usage.WaitForAsync(e => e.Name == UsageEventNames.PromptSaved && e.Source == "tool:save_prompt", Wait);
        saved.Surface.ShouldBe(UsageSurface.Mcp);
        saved.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        saved.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        saved.Outcome.ShouldBe(UsageOutcome.Ok);
        saved.Properties[UsageProps.Prompt].ShouldBe("guide");
    }
}
