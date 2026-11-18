using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.Usage.RetroThemeSheet;

// The retro insights Themes tab's sheet report: POST api/projects/{id}/usage/retro-theme-sheet-opened with which sheet opened, for a member
// of the project. Joel is a member of Ship and not of Part.
[TestFixture]
public class RetroThemeSheetUsageTests : ApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private static string Opened(Guid projectId) => $"api/projects/{projectId}/usage/retro-theme-sheet-opened";

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    [TestCase("theme")]
    [TestCase("unthemed")]
    public async Task ThemeSheetOpened_WhenMember_ShouldEmitTheSheet(string sheet)
    {
        using var response = await Api.PostJsonAsync(Opened(SeedIds.Projects.Ship), new RetroThemeSheetOpenedDto(sheet));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.RetroThemeSheetOpened);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        tracked.Properties[UsageProps.Sheet].ShouldBe(sheet);
    }

    [TestCase("range")]
    [TestCase("Theme")]
    [TestCase("")]
    public async Task ThemeSheetOpened_WhenSheetIsUnknown_ShouldReturnValidationAndTrackNothing(string sheet)
    {
        using var response = await Api.PostJsonAsync(Opened(SeedIds.Projects.Ship), new RetroThemeSheetOpenedDto(sheet));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("sheet");
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task ThemeSheetOpened_WhenNotMember_ShouldReturnNotFoundAndTrackNothing()
    {
        using var response = await Api.PostJsonAsync(Opened(SeedIds.Projects.Part), new RetroThemeSheetOpenedDto("theme"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Recorder.Events.ShouldBeEmpty();
    }
}
