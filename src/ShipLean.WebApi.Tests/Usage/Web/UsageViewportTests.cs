using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.Usage.Web;

// K2, the server half: the three web seams (action filter, screen views, self-emitted actions) stamp viewport from the
// request's ViewportHeader, and leave it out when the header is missing or not one of the three values.
[TestFixture]
public class UsageViewportTests : ApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    private async Task PostAsync(string path, object body, string? viewport)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(ClientHeader.Name, ApiClient.ClientId);
        if (viewport is not null)
        {
            request.Headers.Add(ViewportHeader.Name, viewport);
        }
        request.Content = JsonContent.Create(body, options: JsonSerializerOptions.Web);
        using var response = await Client.SendAsync(request);
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
    }

    private Task CreateStoryAsync(string title, string? viewport)
        => PostAsync("api/work-items", new JsonObject
        {
            ["projectId"] = SeedIds.Projects.Ship,
            ["type"] = "story",
            ["title"] = title,
            ["parentId"] = null,
            ["sprintId"] = null,
            ["changes"] = null,
        }, viewport);

    private UsageEvent OnlyCreate() => Recorder.Events.Where(e => e.Source == "api:WorkItems.Create").ShouldHaveSingleItem();

    [Test]
    public async Task WebAction_WhenViewportHeaderIsPhone_ShouldStampViewport()
    {
        await CreateStoryAsync("Created on a phone", UsageViewports.Phone);

        OnlyCreate().Properties[UsageProps.Viewport].ShouldBe(UsageViewports.Phone);
    }

    [Test]
    public async Task WebAction_WhenViewportHeaderIsMissing_ShouldLeaveViewportOut()
    {
        await CreateStoryAsync("Created without a viewport", null);

        OnlyCreate().Properties.ShouldNotContainKey(UsageProps.Viewport);
    }

    [TestCase("Phone")]
    [TestCase("wide")]
    public async Task WebAction_WhenViewportHeaderIsUnknown_ShouldLeaveViewportOut(string header)
    {
        await CreateStoryAsync("Created on an unknown viewport", header);

        OnlyCreate().Properties.ShouldNotContainKey(UsageProps.Viewport);
    }

    [Test]
    public async Task ScreenView_WhenViewportHeaderIsTablet_ShouldStampViewport()
    {
        await PostAsync("api/usage/screen", new ScreenViewDto("profile", null), UsageViewports.Tablet);

        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.ScreenViewed);
        tracked.Properties[UsageProps.Screen].ShouldBe("profile");
        tracked.Properties[UsageProps.Viewport].ShouldBe(UsageViewports.Tablet);
    }

    [Test]
    public async Task SelfEmitted_WhenViewportHeaderIsDesktop_ShouldStampViewport()
    {
        await PostAsync("api/projects", new ProjectCreate("Viewport create", "VPT", null, [], [1, 2, 3, 4, 5], null), UsageViewports.Desktop);

        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.ProjectCreated);
        tracked.Source.ShouldBe("api:ProjectCreation.Create");
        tracked.Properties[UsageProps.Viewport].ShouldBe(UsageViewports.Desktop);
    }
}
