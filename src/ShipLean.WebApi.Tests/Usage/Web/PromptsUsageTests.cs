using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Prompts;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Prompts;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.Usage.Web;

// C9 over the real routes: one event per action, each with its project and the prompt (the camelCase key). Joel administers SHIP.
[TestFixture]
public class PromptsUsageTests : ApiFixture
{
    private static string Route(string tail) => $"api/projects/{SeedIds.Projects.Ship}/{tail}";

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    private async Task PostAsync(string tail, object? body = null)
    {
        using var response = await Api.PostJsonAsync(Route(tail), body);
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
    }

    private UsageEvent Only(string source) => Recorder.Events.Where(e => e.Source == source).ShouldHaveSingleItem();

    [Test]
    public async Task PromptsOpened_WhenReported_ShouldTrackOpenedForTheProject()
    {
        await PostAsync("usage/prompts-opened");

        var tracked = Only("api:ProjectUsage.PromptsOpened");
        tracked.Name.ShouldBe(UsageEventNames.PromptsOpened);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Properties.GetValueOrDefault(UsageProps.Prompt).ShouldBe(null);
    }

    [Test]
    public async Task PromptCustomizeOpened_WhenReported_ShouldTrackThePrompt()
    {
        await PostAsync("usage/prompt-customize-opened", new { prompt = "planReviewer" });

        var tracked = Only("api:ProjectUsage.PromptCustomizeOpened");
        tracked.Name.ShouldBe(UsageEventNames.PromptCustomizeOpened);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Properties.GetValueOrDefault(UsageProps.Prompt).ShouldBe("planReviewer");
    }

    [Test]
    public async Task PromptsSearchPicked_WhenMember_ShouldRecordTheEvent()
    {
        using var response = await Api.PostJsonAsync(Route("usage/prompts-search-picked"), new { prompt = "brief" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Only("api:ProjectUsage.PromptsSearchPicked");
        tracked.Name.ShouldBe(UsageEventNames.PromptsSearchPicked);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        tracked.Properties.GetValueOrDefault(UsageProps.Prompt).ShouldBe("brief");
    }

    [Test]
    public async Task PromptsSearchPicked_WhenUnknownPrompt_ShouldReturnValidation()
    {
        using var response = await Api.PostJsonAsync(Route("usage/prompts-search-picked"), new { prompt = "noSuchPrompt" });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task PromptsSearchPicked_WhenNotMember_ShouldReturnNotFound()
    {
        using var response = await Api.PostJsonAsync($"api/projects/{SeedIds.Projects.Part}/usage/prompts-search-picked", new { prompt = "brief" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task SaveVersion_WhenFromTheEditor_ShouldTrackSaved()
    {
        await PostAsync("prompts/guide/versions", new { text = "Mine.", note = "A change.", source = "editor" });

        var tracked = Only("api:ProjectPrompts.SaveVersion");
        tracked.Name.ShouldBe(UsageEventNames.PromptSaved);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Properties.GetValueOrDefault(UsageProps.Prompt).ShouldBe("guide");
    }

    [Test]
    public async Task SaveVersion_WhenFromABuiltInUpdate_ShouldTrackUpdateApplied()
    {
        await PostAsync("prompts/codeReviewer/versions", new { text = "Merged.", note = "Applied the update.", source = "builtInUpdate" });

        var tracked = Only("api:ProjectPrompts.SaveVersion");
        tracked.Name.ShouldBe(UsageEventNames.PromptUpdateApplied);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Properties.GetValueOrDefault(UsageProps.Prompt).ShouldBe("codeReviewer");
    }

    [Test]
    public async Task UseBuiltIn_WhenDone_ShouldTrackBuiltInUsed()
    {
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Work).InUse().InsertAsync(ConnectionString);

        await PostAsync("prompts/work/use-built-in");

        var tracked = Only("api:ProjectPrompts.UseBuiltIn");
        tracked.Name.ShouldBe(UsageEventNames.PromptBuiltInUsed);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Properties.GetValueOrDefault(UsageProps.Prompt).ShouldBe("work");
    }

    [Test]
    public async Task UseVersion_WhenDone_ShouldTrackVersionRestored()
    {
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Brief).WithNumber(1).InsertAsync(ConnectionString);

        await PostAsync("prompts/brief/versions/1/use");

        var tracked = Only("api:ProjectPrompts.UseVersion");
        tracked.Name.ShouldBe(UsageEventNames.PromptVersionRestored);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Properties.GetValueOrDefault(UsageProps.Prompt).ShouldBe("brief");
    }

    [Test]
    public async Task KeepMine_WhenDone_ShouldTrackUpdateKept()
    {
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Fix).WithBuiltInText("The old built-in.").InUse().InsertAsync(ConnectionString);

        await PostAsync("prompts/fix/update/keep");

        var tracked = Only("api:ProjectPrompts.KeepMine");
        tracked.Name.ShouldBe(UsageEventNames.PromptUpdateKept);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Properties.GetValueOrDefault(UsageProps.Prompt).ShouldBe("fix");
    }

    [Test]
    public async Task Page_WhenRead_ShouldTrackNothing()
    {
        using var page = await Api.GetAsync(Route("prompts"));
        using var history = await Api.GetAsync(Route("prompts/guide/history"));
        using var assembled = await Api.GetAsync(Route("prompts/guide/assembled?agent=other"));

        page.IsSuccessStatusCode.ShouldBeTrue();
        history.IsSuccessStatusCode.ShouldBeTrue();
        assembled.IsSuccessStatusCode.ShouldBeTrue();
        Recorder.Events.Where(e => e.Source.StartsWith("api:ProjectPrompts.", StringComparison.Ordinal)).ShouldBeEmpty();
    }
}
