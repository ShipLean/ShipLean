using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Prompts;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Prompts;

// C3 over the real tables: the preview runs the save's rules in the save's order (note, text, expected, then "Nothing changed"), diffs against
// the text in use and writes nothing. Every test arranges a project of its own.
[TestFixture]
public class PromptSavePreviewTests : ApiFixture
{
    private static readonly PromptBase BuiltIn = new(null);

    private async Task<Guid> ArrangeProjectAsync()
    {
        await using var database = await OpenDatabaseAsync();
        return (await ArrangedProject.CreateAsync(database, (SeedIds.Users.Joel, ProjectRole.Admin))).Id;
    }

    private async Task SeedAsync(Guid project, int number, string text, bool inUse)
    {
        var builder = new PromptVersionBuilder(project, PromptKey.Guide).WithNumber(number).WithText(text).WithCreatedBy(SeedIds.Users.Joel);
        await (inUse ? builder.InUse() : builder).InsertAsync(ConnectionString);
    }

    private async Task<PromptSavePreviewDto> PreviewAsync(Guid project, string text, PromptBase expected, string note = "A change.")
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPromptSavePreview>().PreviewAsync(project, PromptKey.Guide, text, note, expected);
    }

    private async Task<ApiException> RefusedAsync(Guid project, string text, PromptBase expected, string note = "A change.")
    {
        using var scope = Host.Services.CreateScope();
        var preview = scope.ServiceProvider.GetRequiredService<IPromptSavePreview>();
        return await Should.ThrowAsync<ApiException>(() => preview.PreviewAsync(project, PromptKey.Guide, text, note, expected));
    }

    private async Task<int> VersionsAsync(Guid project)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<int>("SELECT count(*)::int FROM prompt_version WHERE project_id = @project", new { project });
    }

    private string BuiltInText()
    {
        using var scope = Host.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IBuiltInPrompts>().Text(PromptKey.Guide).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    [Test]
    public async Task PreviewAsync_WhenBuiltInInUse_ShouldDiffAgainstTheBuiltInAndOfferV1()
    {
        var project = await ArrangeProjectAsync();
        var text = BuiltInText() + "\nOne more line.\n";

        var preview = await PreviewAsync(project, text, BuiltIn);

        preview.Key.ShouldBe(PromptKey.Guide);
        preview.InUseVersion.ShouldBeNull();
        preview.NextVersion.ShouldBe(1);
        preview.Patch.ShouldContain("+One more line.");
        preview.Patch.ShouldNotContain("\n-");
        preview.Warnings.ShouldBeEmpty();
    }

    [Test]
    public async Task PreviewAsync_WhenAnOlderVersionInUse_ShouldDiffAgainstItAndNumberAfterTheHighest()
    {
        var project = await ArrangeProjectAsync();
        await SeedAsync(project, 1, "Line one.\nLine two.\n", inUse: true);
        await SeedAsync(project, 2, "Something else.\n", inUse: false);

        var preview = await PreviewAsync(project, "Line one.\nLine 2.\n", new PromptBase(1));

        preview.InUseVersion.ShouldBe(1);
        preview.NextVersion.ShouldBe(3);
        preview.Patch.ShouldBe("@@ -1,2 +1,2 @@\n Line one.\n-Line two.\n+Line 2.\n");
    }

    [Test]
    public async Task PreviewAsync_WhenCrLf_ShouldMeasureTheNormalisedUtf8Bytes()
    {
        var project = await ArrangeProjectAsync();

        var preview = await PreviewAsync(project, "Café\r\nend", BuiltIn);

        preview.Bytes.ShouldBe(9);
    }

    [Test]
    public async Task PreviewAsync_WhenAPlaceholderIsUnknown_ShouldWarn()
    {
        var project = await ArrangeProjectAsync();

        var preview = await PreviewAsync(project, "Text with {{nope}}.", BuiltIn);

        preview.Warnings.ShouldContain(w => w.Kind == PromptWarningKind.PlaceholderUnknown && w.Placeholder == "{{nope}}");
    }

    [Test]
    public async Task PreviewAsync_WhenExpectedIsStale_ShouldConflictNamingTheVersionInUse()
    {
        var project = await ArrangeProjectAsync();
        await SeedAsync(project, 1, "Mine.", inUse: true);

        var error = await RefusedAsync(project, "New.", BuiltIn);

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
        error.Message.ShouldBe("The version in use changed since you read it: it is now v1. Read the prompt again with prompts, then save.");
    }

    [Test]
    public async Task PreviewAsync_WhenExpectedNamesAVersionButTheBuiltInIsInUse_ShouldConflictNamingTheBuiltIn()
    {
        var project = await ArrangeProjectAsync();

        var error = await RefusedAsync(project, "New.", new PromptBase(2));

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
        error.Message.ShouldBe("The version in use changed since you read it: it is now the built-in. Read the prompt again with prompts, then save.");
    }

    [Test]
    public async Task PreviewAsync_WhenTextUnchanged_ShouldBeValidationOnText()
    {
        var project = await ArrangeProjectAsync();
        await SeedAsync(project, 1, "Mine.", inUse: true);

        var error = await RefusedAsync(project, "Mine.", new PromptBase(1));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("text");
        error.Message.ShouldBe("Nothing changed from the version in use.");
    }

    [Test]
    public async Task PreviewAsync_WhenUnchangedAndStale_ShouldReportStaleFirst()
    {
        var project = await ArrangeProjectAsync();
        await SeedAsync(project, 1, "Mine.", inUse: true);

        var error = await RefusedAsync(project, "Mine.", BuiltIn);

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task PreviewAsync_WhenNoteBlankAndTextEmptyAndStale_ShouldRefuseOnNoteFirst()
    {
        var project = await ArrangeProjectAsync();
        await SeedAsync(project, 1, "Mine.", inUse: true);

        var error = await RefusedAsync(project, " ", BuiltIn, note: " ");

        error.Field.ShouldBe("note");
    }

    [Test]
    public async Task PreviewAsync_WhenTextEmptyAndStale_ShouldRefuseOnTextFirst()
    {
        var project = await ArrangeProjectAsync();
        await SeedAsync(project, 1, "Mine.", inUse: true);

        var error = await RefusedAsync(project, " ", BuiltIn);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("text");
    }

    [Test]
    public async Task PreviewAsync_WhenValid_ShouldWriteNothing()
    {
        var project = await ArrangeProjectAsync();

        await PreviewAsync(project, "New.", BuiltIn);

        (await VersionsAsync(project)).ShouldBe(0);
    }
}
