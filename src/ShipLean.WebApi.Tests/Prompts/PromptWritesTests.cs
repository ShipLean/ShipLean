using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Prompts;
using ShipLean.WebApi.Features.Prompts;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Prompts;

// C6 over the real routes and tables. Every test arranges a project of its own with Joel as its admin. The built-in of Brief is replaced by a changed
// copy (a new release), so only the update tests use Brief.
[TestFixture]
public class PromptWritesTests : ApiFixture
{
    private const string ChangedBrief = "A newer built-in brief.";
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IBuiltInPrompts>();
        services.AddSingleton<IBuiltInPrompts>(_ => new ChangedBuiltInPrompts(new BuiltInPrompts(), new Dictionary<PromptKey, string> { [PromptKey.Brief] = ChangedBrief }));
    };

    private async Task<Guid> ArrangeProjectAsync()
    {
        await using var database = await OpenDatabaseAsync();
        return (await ArrangedProject.CreateAsync(database, (SeedIds.Users.Joel, ProjectRole.Admin))).Id;
    }

    private async Task<HttpResponseMessage> SaveAsync(Guid project, string prompt, string text, string note = "A change.", string source = "editor")
        => await Api.PostJsonAsync($"api/projects/{project}/prompts/{prompt}/versions", new { text, note, source });

    private async Task<PromptSavedDto> SavedAsync(Guid project, string prompt, string text, string source = "editor")
    {
        using var response = await SaveAsync(project, prompt, text, source: source);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<PromptSavedDto>(body, Wire)!;
    }

    private static async Task<ApiError> ErrorOfAsync(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.ShouldBeFalse(await response.Content.ReadAsStringAsync());
        return await ApiClient.ReadApiErrorAsync(response);
    }

    private async Task<(int Versions, int? InUse, string? Kept)> RowsAsync(Guid project, string prompt)
    {
        await using var database = await OpenDatabaseAsync();
        var versions = await database.QuerySingleAsync<int>("SELECT count(*)::int FROM prompt_version WHERE project_id = @project AND prompt = @prompt", new { project, prompt });
        var pointer = await database.QuerySingleOrDefaultAsync<(int? Number, string? Kept)?>(
            """
            SELECT v.number, p.kept_built_in_hash FROM project_prompt p LEFT JOIN prompt_version v ON v.id = p.in_use_version_id
            WHERE p.project_id = @project AND p.prompt = @prompt
            """,
            new { project, prompt });
        return (versions, pointer?.Number, pointer?.Kept);
    }

    private async Task SeedOldBaseAsync(Guid project, PromptKey key, string text = "Mine on the old base.")
        => await new PromptVersionBuilder(project, key).WithText(text).WithBuiltInText("The old built-in.").WithCreatedBy(SeedIds.Users.Joel).InUse().InsertAsync(ConnectionString);

    [Test]
    public async Task Save_WhenAdmin_ShouldMakeV1ThenV2()
    {
        var project = await ArrangeProjectAsync();

        var first = await SavedAsync(project, "guide", "First text.");
        using var second = await SaveAsync(project, "guide", "Second text.", "Second note.");

        first.ShouldBe(new PromptSavedDto(PromptKey.Guide, PromptStatus.Custom, 1));
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonSerializer.Deserialize<PromptSavedDto>(await second.Content.ReadAsStringAsync(), Wire)!.InUseVersion.ShouldBe(2);
        (await RowsAsync(project, "guide")).ShouldBe((2, 2, null));
        await using var database = await OpenDatabaseAsync();
        var row = await database.QuerySingleAsync<(string Note, Guid By)>("SELECT note, created_by FROM prompt_version WHERE project_id = @project AND number = 2", new { project });
        row.ShouldBe(("Second note.", SeedIds.Users.Joel));
    }

    [Test]
    public async Task Save_WhenOver64KB_ShouldBeValidationOnText()
    {
        var project = await ArrangeProjectAsync();

        using var response = await SaveAsync(project, "guide", new string('é', 32_769));

        var error = await ErrorOfAsync(response);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("text");
    }

    [Test]
    public async Task Save_WhenExactly64KB_ShouldSave()
    {
        var project = await ArrangeProjectAsync();

        var saved = await SavedAsync(project, "guide", new string('é', 32_768));

        saved.InUseVersion.ShouldBe(1);
    }

    [Test]
    public async Task Save_WhenAPlaceholderIsRemoved_ShouldStillSave()
    {
        var project = await ArrangeProjectAsync();

        var saved = await SavedAsync(project, "brief", "A brief with no task key at all.");

        saved.InUseVersion.ShouldBe(1);
    }

    [Test]
    public async Task Save_WhenATagIsUnclosed_ShouldBeValidationOnText()
    {
        var project = await ArrangeProjectAsync();

        using var response = await SaveAsync(project, "guide", "Intro\n<claude-code>\nOnly for one agent");

        var error = await ErrorOfAsync(response);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("text");
    }

    [Test]
    public async Task Save_WhenATagIsMisspelt_ShouldStillSave()
    {
        var project = await ArrangeProjectAsync();

        var saved = await SavedAsync(project, "guide", "Intro\n<claude-cod>\nOops\n</claude-cod>");

        saved.InUseVersion.ShouldBe(1);
    }

    [Test]
    public async Task Save_WhenNoteIsBlank_ShouldBeValidationOnNote()
    {
        var project = await ArrangeProjectAsync();

        using var response = await SaveAsync(project, "guide", "Some text.", note: "   ");

        var error = await ErrorOfAsync(response);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("note");
    }

    [Test]
    public async Task Save_WhenTextUnchanged_ShouldBeValidationOnText()
    {
        var project = await ArrangeProjectAsync();
        await SavedAsync(project, "guide", "Same text.");

        using var response = await SaveAsync(project, "guide", "Same text.");

        var error = await ErrorOfAsync(response);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("text");
    }

    [Test]
    public async Task Save_WhenTextEqualsTheBuiltInAndNoCustom_ShouldBeValidationOnText()
    {
        var project = await ArrangeProjectAsync();

        using var response = await SaveAsync(project, "guide", new BuiltInPrompts().Text(PromptKey.Guide));

        var error = await ErrorOfAsync(response);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("text");
    }

    [Test]
    public async Task Save_WhenCrLf_ShouldStoreLfAndMeasureAfter()
    {
        var project = await ArrangeProjectAsync();
        var lines = string.Join("\r\n", Enumerable.Repeat(new string('a', 31), 2048));

        var saved = await SavedAsync(project, "guide", lines);

        saved.InUseVersion.ShouldBe(1);
        await using var database = await OpenDatabaseAsync();
        var text = await database.QuerySingleAsync<string>("SELECT text FROM prompt_version WHERE project_id = @project", new { project });
        text.ShouldNotContain('\r');
    }

    [Test]
    public async Task Save_WhenSourceIsMigrated_ShouldBeValidationOnSource()
    {
        var project = await ArrangeProjectAsync();

        using var response = await SaveAsync(project, "guide", "Some text.", source: "migrated");

        var error = await ErrorOfAsync(response);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("source");
    }

    [Test]
    public async Task Save_WhenFromBuiltInUpdate_ShouldBaseOnTheCurrentBuiltIn()
    {
        var project = await ArrangeProjectAsync();
        await SeedOldBaseAsync(project, PromptKey.Brief);

        var saved = await SavedAsync(project, "brief", "Merged text.", "builtInUpdate");

        saved.ShouldBe(new PromptSavedDto(PromptKey.Brief, PromptStatus.Custom, 2));
        await using var database = await OpenDatabaseAsync();
        (await database.QuerySingleAsync<string>("SELECT base_hash FROM prompt_version WHERE project_id = @project AND number = 2", new { project })).ShouldBe(PromptHash.Of(ChangedBrief));
    }

    [Test]
    public async Task Save_WhenFromEditorOnAnOldBase_ShouldKeepThatBase()
    {
        var project = await ArrangeProjectAsync();
        await SeedOldBaseAsync(project, PromptKey.Brief);

        var saved = await SavedAsync(project, "brief", "Edited text.");

        saved.ShouldBe(new PromptSavedDto(PromptKey.Brief, PromptStatus.BuiltInUpdated, 2));
        await using var database = await OpenDatabaseAsync();
        (await database.QuerySingleAsync<string>("SELECT base_hash FROM prompt_version WHERE project_id = @project AND number = 2", new { project })).ShouldBe(PromptHash.Of("The old built-in."));
    }

    [Test]
    public async Task KeepMine_WhenChangePending_ShouldStoreTheCurrentHash()
    {
        var project = await ArrangeProjectAsync();
        await SeedOldBaseAsync(project, PromptKey.Brief);

        using var response = await Api.PostJsonAsync($"api/projects/{project}/prompts/brief/update/keep");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await RowsAsync(project, "brief")).Kept.ShouldBe(PromptHash.Of(ChangedBrief));
        using var again = await Api.PostJsonAsync($"api/projects/{project}/prompts/brief/update/keep");
        var error = await ErrorOfAsync(again);
        error.Code.ShouldBe(ApiErrorCodes.Conflict);
        error.Field.ShouldBeNull();
    }

    [Test]
    public async Task KeepMine_WhenNoChangePending_ShouldBeConflict()
    {
        var project = await ArrangeProjectAsync();
        await SavedAsync(project, "guide", "Mine.");

        using var response = await Api.PostJsonAsync($"api/projects/{project}/prompts/guide/update/keep");

        var error = await ErrorOfAsync(response);
        error.Code.ShouldBe(ApiErrorCodes.Conflict);
        error.Field.ShouldBeNull();
    }

    [Test]
    public async Task UseBuiltIn_WhenCustomInUse_ShouldKeepEveryVersion()
    {
        var project = await ArrangeProjectAsync();
        await SavedAsync(project, "guide", "One.");
        await SavedAsync(project, "guide", "Two.");

        using var response = await Api.PostJsonAsync($"api/projects/{project}/prompts/guide/use-built-in");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonSerializer.Deserialize<PromptSavedDto>(await response.Content.ReadAsStringAsync(), Wire).ShouldBe(new PromptSavedDto(PromptKey.Guide, PromptStatus.UsingBuiltIn, null));
        (await RowsAsync(project, "guide")).ShouldBe((2, null, null));
    }

    [Test]
    public async Task UseBuiltIn_WhenAlreadyBuiltIn_ShouldBeConflict()
    {
        var project = await ArrangeProjectAsync();

        using var response = await Api.PostJsonAsync($"api/projects/{project}/prompts/guide/use-built-in");

        var error = await ErrorOfAsync(response);
        error.Code.ShouldBe(ApiErrorCodes.Conflict);
        error.Field.ShouldBeNull();
    }

    [Test]
    public async Task UseVersion_WhenGiven_ShouldPutItBackInUse()
    {
        var project = await ArrangeProjectAsync();
        await SavedAsync(project, "guide", "One.");
        await SavedAsync(project, "guide", "Two.");

        using var response = await Api.PostJsonAsync($"api/projects/{project}/prompts/guide/versions/1/use");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonSerializer.Deserialize<PromptSavedDto>(await response.Content.ReadAsStringAsync(), Wire).ShouldBe(new PromptSavedDto(PromptKey.Guide, PromptStatus.Custom, 1));
        (await RowsAsync(project, "guide")).ShouldBe((2, 1, null));
    }

    [Test]
    public async Task UseVersion_WhenUnknown_ShouldBeNotFound()
    {
        var project = await ArrangeProjectAsync();
        await SavedAsync(project, "guide", "One.");

        using var response = await Api.PostJsonAsync($"api/projects/{project}/prompts/guide/versions/9/use");

        var error = await ErrorOfAsync(response);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Field.ShouldBeNull();
    }

    [Test]
    public async Task UseVersion_WhenNumberExistsOnlyInAnotherProject_ShouldBeNotFound()
    {
        var project = await ArrangeProjectAsync();
        var other = await ArrangeProjectAsync();
        await SavedAsync(other, "guide", "Theirs.");

        using var response = await Api.PostJsonAsync($"api/projects/{project}/prompts/guide/versions/1/use");

        var error = await ErrorOfAsync(response);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Field.ShouldBeNull();
    }

    [Test]
    public async Task Save_WhenTwoProjects_ShouldNumberEachFromOne()
    {
        var first = await ArrangeProjectAsync();
        var second = await ArrangeProjectAsync();

        var inFirst = await SavedAsync(first, "guide", "One.");
        var inSecond = await SavedAsync(second, "guide", "Other.");

        (inFirst.InUseVersion, inSecond.InUseVersion).ShouldBe((1, 1));
    }

    [Test]
    public async Task Save_WhenTwoAtOnce_ShouldTakeDistinctNumbers()
    {
        var project = await ArrangeProjectAsync();

        var saved = await Task.WhenAll(SavedAsync(project, "guide", "Text A."), SavedAsync(project, "guide", "Text B."));

        saved.Select(s => s.InUseVersion).Order().ShouldBe([1, 2]);
    }

    [Test]
    public async Task Save_WhenTwoWaitOnTheLockWithACustomInUse_ShouldKeepTheOldBaseAndNumberDistinctly()
    {
        var project = await ArrangeProjectAsync();
        await SeedOldBaseAsync(project, PromptKey.Brief);
        await using var holder = await OpenDatabaseAsync();
        await using var transaction = await holder.BeginTransactionAsync();
        await holder.ExecuteAsync(
            "SELECT 1 FROM project_prompt WHERE project_id = @project AND prompt = 'brief' FOR UPDATE", new { project }, transaction);

        var first = SavedAsync(project, "brief", "Edited while waiting.");
        var second = SavedAsync(project, "brief", "Edited too, while waiting.");
        await WaitForWaitersAsync(2, first, second);
        await transaction.CommitAsync();
        var saved = await Task.WhenAll(first, second);

        saved.Select(s => s.InUseVersion).Order().ShouldBe([2, 3]);
        saved.Select(s => s.Status).ShouldAllBe(status => status == PromptStatus.BuiltInUpdated);
    }

    private async Task WaitForWaitersAsync(int count, params Task[] writes)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var database = await OpenDatabaseAsync();
        while (await database.QuerySingleAsync<int>("SELECT count(*)::int FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND pid <> pg_backend_pid() AND query ILIKE '%FROM project_prompt%FOR UPDATE%'") < count)
        {
            if (writes.Any(write => write.IsCompleted) || timeout.IsCancellationRequested)
            {
                throw new InvalidOperationException("A write finished or the lock wait timed out before both writes were waiting on the lock.");
            }

            await Task.Yield();
        }
    }
}
